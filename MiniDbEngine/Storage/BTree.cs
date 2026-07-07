using System.Buffers.Binary;

namespace MiniDbEngine.Storage
{
	public class BTree
	{
		private readonly PageManager _pm;
		private readonly WalManager _wal;
		private int _rootPageId;

		public BTree(PageManager pm, WalManager wal)
		{
			_pm = pm;
			_wal = wal;
			InitializeMetaPage();
			RecoverFromWal();
		}

		//page 0 is always the Meta Page. it acts as the entry point and stores the RootPageId
		private void InitializeMetaPage()
		{
			try
			{
				Page metaPage = _pm.ReadPage(0);
				_rootPageId = metaPage.ParentId;
			}
			catch (ArgumentOutOfRangeException)
			{
				Page metaPage = _pm.AllocatePage(3); // Type 3 = Meta
				Page rootLeaf = _pm.AllocatePage(1); // Type 1 = Leaf

				_rootPageId = rootLeaf.PageId;
				metaPage.ParentId = _rootPageId;
				_pm.WritePage(metaPage);
			}
		}

		public bool Get(ReadOnlySpan<byte> key, out byte[] value)
		{
			Page current = _pm.ReadPage(_rootPageId);

			//traverse down the tree until we hit a leaf page (type 1)
			while (current.PageType != 1)
			{
				int childId = GetChildPageId(current, key);
				current = _pm.ReadPage(childId);
			}

			if (current.BinarySearch(key, out int index))
			{
				current.GetRecord(index, out _, out ReadOnlySpan<byte> valSpan);
				value = valSpan.ToArray();
				return true;
			}

			value = Array.Empty<byte>();
			return false;
		}

		public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			//1. Write ahead logging
			_wal.LogPut(key, value);

			//2. Actual B-Tree insertion
			InternalPut(key, value);
		}

		private void InternalPut(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Page current = _pm.ReadPage(_rootPageId);
			Stack<Page> path = new();

			while (current.PageType != 1)
			{
				path.Push(current);
				int childId = GetChildPageId(current, key);
				current = _pm.ReadPage(childId);
			}

			if (current.InsertRecord(key, value))
			{
				_pm.WritePage(current);
				return;
			}

			SplitNode(current, key, value, path);
		}

		public void Delete(ReadOnlySpan<byte> key)
		{
			// 1. Write ahead logging for durability
			_wal.LogDelete(key);

			// 2. Actual B-Tree deletion
			InternalDelete(key);
		}

		private void InternalDelete(ReadOnlySpan<byte> key)
		{
			Page current = _pm.ReadPage(_rootPageId);

			while (current.PageType != 1)
			{
				int childId = GetChildPageId(current, key);
				current = _pm.ReadPage(childId);
			}

			if (current.DeleteRecord(key))
			{
				_pm.WritePage(current);
			}
		}

		/// <summary>
		/// Performs a Range Scan by traversing the leaf nodes via the RightPointer linked list.
		/// This is an O(log N + K) operation, showcasing the true power of a B+Tree.
		/// </summary>
		public IEnumerable<KeyValuePair<byte[], byte[]>> Scan(ReadOnlySpan<byte> startKey, ReadOnlySpan<byte> endKey)
		{
			Page current = _pm.ReadPage(_rootPageId);

			while (current.PageType != 1)
			{
				int childId = GetChildPageId(current, startKey);
				current = _pm.ReadPage(childId);
			}

			bool stop = false;
			while (current != null && !stop)
			{
				for (int i = 0; i < current.RecordCount; i++)
				{
					current.GetRecord(i, out ReadOnlySpan<byte> k, out ReadOnlySpan<byte> v);

					if (k.SequenceCompareTo(startKey) < 0) continue;

					if (k.SequenceCompareTo(endKey) > 0)
					{
						stop = true;
						break;
					}

					yield return new KeyValuePair<byte[], byte[]>(k.ToArray(), v.ToArray());
				}

				if (stop || current.RightPointer == 0)
					break;

				//jump to the next sibling page instantly
				current = _pm.ReadPage(current.RightPointer);
			}
		}

		private int GetChildPageId(Page internalPage, ReadOnlySpan<byte> key)
		{
			bool exactMatch = internalPage.BinarySearch(key, out int index);
			if (exactMatch)
			{
				internalPage.GetRecord(index, out _, out ReadOnlySpan<byte> valSpan);
				return BinaryPrimitives.ReadInt32LittleEndian(valSpan);
			}

			if (index == 0)
				return internalPage.RightPointer;

			internalPage.GetRecord(index - 1, out _, out ReadOnlySpan<byte> valSpanInner);
			return BinaryPrimitives.ReadInt32LittleEndian(valSpanInner);
		}

		private void SplitNode(Page page, ReadOnlySpan<byte> newKey, ReadOnlySpan<byte> newValue, Stack<Page> path)
		{
			// 1. extract all existing records + the new record, and sort them in memory
			List<KeyValuePair<byte[], byte[]>> allRecords = new List<KeyValuePair<byte[], byte[]>>();
			for (int i = 0; i < page.RecordCount; i++)
			{
				page.GetRecord(i, out ReadOnlySpan<byte> k, out ReadOnlySpan<byte> v);
				allRecords.Add(new KeyValuePair<byte[], byte[]>(k.ToArray(), v.ToArray()));
			}
			allRecords.Add(new KeyValuePair<byte[], byte[]>(newKey.ToArray(), newValue.ToArray()));
			allRecords.Sort((a, b) => a.Key.AsSpan().SequenceCompareTo(b.Key));

			// 2. clear the old page and allocate a new sibling page
			page.Clear();
			Page newPage = _pm.AllocatePage(page.PageType);
			newPage.ParentId = page.ParentId;

			int midIndex = allRecords.Count / 2;

			// my B+Tree logic : leaf nodes keep the median value on the right, internal nodes push it straight up
			int startIndexRight = page.PageType == 1 ? midIndex : midIndex + 1;

			for (int i = 0; i < midIndex; i++)
				page.InsertRecord(allRecords[i].Key, allRecords[i].Value);

			for (int i = startIndexRight; i < allRecords.Count; i++)
				newPage.InsertRecord(allRecords[i].Key, allRecords[i].Value);

			byte[] splitKey = allRecords[midIndex].Key;

			// 3. adjust pointers
			if (page.PageType == 1)
			{
				newPage.RightPointer = page.RightPointer;
				page.RightPointer = newPage.PageId;
			}
			else
			{
				newPage.RightPointer = BinaryPrimitives.ReadInt32LittleEndian(allRecords[midIndex].Value);
			}

			_pm.WritePage(page);
			_pm.WritePage(newPage);

			// 4. push the median value up to the parent (recursive split handling)
			byte[] rightPageIdBytes = new byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(rightPageIdBytes, newPage.PageId);

			if (path.Count == 0) //create new one
			{
				Page newRoot = _pm.AllocatePage(2); // type 2 = Internal
				newRoot.RightPointer = page.PageId;
				newRoot.InsertRecord(splitKey, rightPageIdBytes);

				page.ParentId = newRoot.PageId;
				newPage.ParentId = newRoot.PageId;

				_pm.WritePage(page);
				_pm.WritePage(newPage);
				_pm.WritePage(newRoot);

				_rootPageId = newRoot.PageId;
				Page metaPage = _pm.ReadPage(0);
				metaPage.ParentId = _rootPageId;
				_pm.WritePage(metaPage);
			}
			else
			{
				Page parent = path.Pop();
				if (parent.InsertRecord(splitKey, rightPageIdBytes))
				{
					_pm.WritePage(parent);
				}
				else
				{
					SplitNode(parent, splitKey, rightPageIdBytes, path);
				}
			}
		}

		private void RecoverFromWal()
		{
			if (!File.Exists(_wal.FilePath))
				return;

			using FileStream walStream = new FileStream(_wal.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

			if (walStream.Length == 0)
				return;

			Console.WriteLine("Crash detected. Recovering from WAL...");

			byte[] header = new byte[5];
			while (walStream.Position < walStream.Length)
			{
				int read = walStream.Read(header, 0, 1);
				if (read < 1) break;

				byte opCode = header[0];

				if (opCode == 1) // PUT Operation
				{
					walStream.ReadExactly(header, 1, 4); //read KeyLen (2) + ValLen (2)
					ushort keyLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(1, 2));
					ushort valLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(3, 2));

					byte[] key = new byte[keyLen];
					byte[] value = new byte[valLen];
					walStream.ReadExactly(key, 0, keyLen);
					walStream.ReadExactly(value, 0, valLen);

					InternalPut(key, value);
				}
				else if (opCode == 2) // DELETE Operation
				{
					walStream.ReadExactly(header, 1, 2); //read KeyLen (2)
					ushort keyLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(1, 2));

					byte[] key = new byte[keyLen];
					walStream.ReadExactly(key, 0, keyLen);

					InternalDelete(key);
				}
			}

			_pm.SyncToDisk();
			_wal.Clear();
			Console.WriteLine("Recovery complete.");
		}
	}
}