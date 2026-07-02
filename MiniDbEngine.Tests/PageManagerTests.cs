using MiniDbEngine.Storage;

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
	}
}
