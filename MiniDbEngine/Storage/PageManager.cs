namespace MiniDbEngine.Storage
{
	public class PageManager : IDisposable
	{
		private readonly FileStream _fileStream;
		private int _nextPageId;

		public PageManager(string filePath)
		{
			FileStreamOptions options = new FileStreamOptions
			{
				Mode = FileMode.OpenOrCreate,
				Access = FileAccess.ReadWrite,
				Share = FileShare.None,
				BufferSize = 4096,
				Options = FileOptions.RandomAccess
			};

			_fileStream = new FileStream(filePath, options);

			long fileSize = _fileStream.Length;

			if (fileSize % Page.PageSize != 0)
				throw new InvalidDataException("Database file size is corrupted(not a multiple of PageSize).");

			_nextPageId = (int)(fileSize / Page.PageSize);
		}

		public Page ReadPage(int pageId)
		{
			long offset = (long)pageId * Page.PageSize;

			if (offset >= _fileStream.Length)
				throw new ArgumentOutOfRangeException(nameof(pageId), "Page does not exist.");

			_fileStream.Seek(offset, SeekOrigin.Begin);
			return Page.ReadFrom(_fileStream);
		}

		public void WritePage(Page page)
		{
			long offset = (long)page.PageId * Page.PageSize;

			_fileStream.Seek(offset, SeekOrigin.Begin);
			page.WriteTo(_fileStream);
		}

		public Page AllocatePage(byte pageType)
		{
			int newPageId = _nextPageId++;
			Page page = new Page();
			page.PageId = newPageId;
			page.PageType = pageType;
			page.FreeSpaceOffset = Page.PageSize;

			_fileStream.Seek(0, SeekOrigin.End);
			page.WriteTo(_fileStream);

			return page;
		}

		public void SyncToDisk()
		{
			_fileStream.Flush(flushToDisk: true);
		}

		private readonly bool _disposed = false;

		public void Dispose()
		{
			if (!_disposed)
			{
				if (_fileStream != null)
				{
					_fileStream.Flush(flushToDisk: true);
					_fileStream.Dispose();
				}
			}
		}
	}
}
