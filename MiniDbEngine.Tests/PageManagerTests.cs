using MiniDbEngine.Storage;
using System.Buffers.Binary;
using System.Text;

namespace MiniDbEngine.Tests
{
	public class PageManagerTests
	{
		[Fact]
		public void Write_And_Read_Page_Should_Persist_Data()
		{
			string dbPath = "test_persistence.db";

			if (File.Exists(dbPath))
			{
				File.Delete(dbPath);
			}

			using (PageManager pm = new PageManager(dbPath))
			{
				Page page = pm.AllocatePage(pageType: 1);
				page.PageType = 99;
				pm.WritePage(page);
			}

			using (PageManager pm2 = new PageManager(dbPath))
			{
				Page readPage = pm2.ReadPage(pageId: 0);

				Assert.Equal(0, readPage.PageId);
				Assert.Equal(99, readPage.PageType);
				Assert.Equal(Page.PageSize, readPage.FreeSpaceOffset);
			}

			//cleanup
			if (File.Exists(dbPath))
			{
				File.Delete(dbPath);
			}
		}

		[Fact]
		public void Insert_And_Get_Record_Should_Work_Correctly()
		{
			Page page = new();

			byte[] keyToInsert = Encoding.UTF8.GetBytes("user:1000");
			byte[] valueToInsert = Encoding.UTF8.GetBytes("testUser1");

			bool isInserted = page.InsertRecord(keyToInsert, valueToInsert);

			Assert.True(isInserted);
			Assert.Equal(1, page.RecordCount);

			page.GetRecord(0, out ReadOnlySpan<byte> retrievedKey, out ReadOnlySpan<byte> retrievedValue);

			Assert.True(retrievedKey.SequenceEqual(keyToInsert));
			Assert.True(retrievedValue.SequenceEqual(valueToInsert));
		}

		[Fact]
		public void Crash_Recovery_Should_Restore_Uncommitted_Wal_Records()
		{
			string dbPath = "crash_test.db";
			string walPath = dbPath + ".wal";

			if (File.Exists(dbPath)) File.Delete(dbPath);
			if (File.Exists(walPath)) File.Delete(walPath);

			byte[] key = Encoding.UTF8.GetBytes("crash_key");
			byte[] value = Encoding.UTF8.GetBytes("recovered_value");

			using (FileStream fs = new FileStream(walPath, FileMode.Create))
			{
				byte[] header = new byte[5];
				header[0] = 1; // OpCode 1 = put
				BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(1, 2), (ushort)key.Length);
				BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(3, 2), (ushort)value.Length);

				fs.Write(header);
				fs.Write(key);
				fs.Write(value);
			} //file is closed, simulating power loss before B-Tree could update

			// 2. Start the database engine. It must detect the unprocessed WAL file,
			using (PageManager pm = new PageManager(dbPath))
			using (WalManager wal = new WalManager(dbPath))
			{
				BTree db = new BTree(pm, wal);

				// 3. Verify the data is now safely inside the B-Tree structure
				bool found = db.Get(key, out byte[] recoveredData);
				Assert.True(found);
				Assert.True(recoveredData.SequenceEqual(value));

				// 4. Verify the engine successfully compacted/cleared the WAL file
				Assert.Equal(0, new FileInfo(walPath).Length);
			}

			if (File.Exists(dbPath)) File.Delete(dbPath);
			if (File.Exists(walPath)) File.Delete(walPath);
		}
	}
}
