using System.Buffers.Binary;

namespace MiniDbEngine.Storage
{
	public class Page
	{
		public const int PageSize = 4096;

		//header takes 17 bytes(4 bytes Id, 1 byte Type, 2 bytes FreeSpace, 2 bytes RecordCount, 4 bytes Parent, 4 bytes rightPtr)
		public const int HeaderSize = 17;

		private readonly byte[] _buffer;

		public Page(byte[] buffer)
		{
			if (buffer == null || buffer.Length < PageSize)
				throw new ArgumentException($"Buffer size must be at least {PageSize} bytes.");

			_buffer = buffer;
		}

		public Page()
		{
			_buffer = new byte[PageSize];
			FreeSpaceOffset = PageSize; //initially, free space starts at the very end of the page
			RecordCount = 0;
		}

		public int PageId
		{
			get => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(0, 4));
			set => BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(0, 4), value);
		}

		public byte PageType
		{
			get => _buffer[4];
			set => _buffer[4] = value;
		}

		public ushort FreeSpaceOffset
		{
			get => BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(5, 2));
			set => BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(5, 2), value);
		}

		//total count of key-value pairs stored in this page
		public ushort RecordCount
		{
			get => BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(7, 2));
			set => BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(7, 2), value);
		}

		public int ParentId
		{
			get => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(9, 4));
			set => BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(9, 4), value);
		}

		public int RightPointer
		{
			get => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(13, 4));
			set => BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(13, 4), value);
		}

		public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, PageSize);

		/// <summary>
		/// Inserts a new key-value pair into the page. Returns false if theres not enough space
		/// </summary>
		public bool InsertRecord(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			ushort keyLen = (ushort)key.Length;
			ushort valLen = (ushort)value.Length;
			ushort cellSize = (ushort)(4 + keyLen + valLen);

			int slotArrayEnd = HeaderSize + (RecordCount * 2);
			if (slotArrayEnd + cellSize + 2 > FreeSpaceOffset)
			{
				return false; // page is full (triggers B-Tree page split)
			}

			// 1. find the correct sorted insertion point using binarySearch
			bool exists = BinarySearch(key, out int insertIndex);
			if (exists)
			{
				throw new InvalidOperationException("Key already exists. Duplicates are not allowed.");
			}

			// 2. allocate space from the end of the free space for the actual cell data
			FreeSpaceOffset -= cellSize;
			int cellOffset = FreeSpaceOffset;

			Span<byte> cellSpan = _buffer.AsSpan(cellOffset, cellSize);
			BinaryPrimitives.WriteUInt16LittleEndian(cellSpan.Slice(0, 2), keyLen);
			BinaryPrimitives.WriteUInt16LittleEndian(cellSpan.Slice(2, 2), valLen);
			key.CopyTo(cellSpan.Slice(4, keyLen));
			value.CopyTo(cellSpan.Slice(4 + keyLen, valLen));

			// 3. shift existing slots to the right to make room for the new slot pointer
			if (insertIndex < RecordCount)
			{
				int shiftStartOffset = HeaderSize + (insertIndex * 2);
				int shiftLength = (RecordCount - insertIndex) * 2;

				Array.Copy(
					sourceArray: _buffer, sourceIndex: shiftStartOffset,
					destinationArray: _buffer, destinationIndex: shiftStartOffset + 2,
					length: shiftLength);
			}

			// 4. write the starting offset of the new cell into its correct sorted slot
			int newSlotOffset = HeaderSize + (insertIndex * 2);
			BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(newSlotOffset, 2), (ushort)cellOffset);

			// 5. increment the record count
			RecordCount++;

			return true;
		}

		/// <summary>
		/// Retrieves a key-value pair from the page using its slot index.
		/// Uses ReadOnlySpan to prevent memory allocations.
		/// </summary>
		public void GetRecord(int slotIndex, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
		{
			if (slotIndex < 0 || slotIndex >= RecordCount)
				throw new ArgumentOutOfRangeException(nameof(slotIndex), "Invalid slot index.");

			// 1. find the offset of the cell from the SlotArray
			int slotOffset = HeaderSize + (slotIndex * 2);
			ushort cellOffset = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(slotOffset, 2));

			//2. read lengths and data from the cell
			ReadOnlySpan<byte> cellSpan = _buffer.AsSpan(cellOffset);

			ushort keyLen = BinaryPrimitives.ReadUInt16LittleEndian(cellSpan.Slice(0, 2));
			ushort valLen = BinaryPrimitives.ReadUInt16LittleEndian(cellSpan.Slice(2, 2));

			key = cellSpan.Slice(4, keyLen);
			value = cellSpan.Slice(4 + keyLen, valLen);
		}

		/// <summary>
		/// Performs  a binary search on the sorted slot array to find the exact match or the insertion point for a key.
		/// Returns true if the key is found, setting 'slotIndex' to its position.
		/// Returns false if not found, setting 'slotIndex' to the index where it should be inserted to maintain order.
		/// </summary>
		public bool BinarySearch(ReadOnlySpan<byte> targetKey, out int slotIndex)
		{
			int left = 0;
			int right = RecordCount - 1;

			while (left <= right)
			{
				int mid = left + (right - left) / 2;

				//extract the key at the mid slot
				GetRecord(mid, out ReadOnlySpan<byte> midKey, out _);

				int cmp = midKey.SequenceCompareTo(targetKey);

				if (cmp == 0)
				{
					slotIndex = mid;
					return true; //exact match found
				}
				else if (cmp < 0)
				{
					left = mid + 1; //midKey is smaller than targetKey
				}
				else
				{
					right = mid - 1; //midKey is larger than targetKey
				}
			}

			slotIndex = left;
			return false;
		}

		/// <summary>
		/// Logically deletes a record by removing its pointer from the slot array.
		/// A production engine would run a page compaction algorithm to reclaim this space.
		/// </summary>
		public bool DeleteRecord(ReadOnlySpan<byte> key)
		{
			if (!BinarySearch(key, out int index))
			{
				return false; //key not found
			}

			if (index < RecordCount - 1)
			{
				int shiftStartOffset = HeaderSize + ((index + 1) * 2);
				int destOffset = HeaderSize + (index * 2);
				int shiftLength = (RecordCount - 1 - index) * 2;

				Array.Copy(
					sourceArray: _buffer, sourceIndex: shiftStartOffset,
					destinationArray: _buffer, destinationIndex: destOffset,
					length: shiftLength);
			}

			RecordCount--;
			return true;
		}

		public void WriteTo(Stream stream)
		{
			stream.Write(AsSpan());
		}

		public static Page ReadFrom(Stream stream)
		{
			byte[] buffer = new byte[PageSize];

			stream.ReadExactly(buffer, 0, PageSize);
			return new Page(buffer);
		}

		public void Clear()
		{
			FreeSpaceOffset = PageSize;
			RecordCount = 0;
		}
	}
}
