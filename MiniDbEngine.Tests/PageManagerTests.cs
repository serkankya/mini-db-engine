using MiniDbEngine.Storage;
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
	}
}
