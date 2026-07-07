using System.Buffers.Binary;

namespace MiniDbEngine.Storage
{
	public class WalManager : IDisposable
	{
		private readonly FileStream _walStream;
		public string FilePath { get; }

		public WalManager(string dbFilePath)
		{
			FilePath = dbFilePath + ".wal";

			_walStream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
		}

		///<summary>
		/// Logs an insert operation to the disk
		/// Must be called before modifying the B-Tree pages.
		/// </summary>
		public void LogPut(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			//OpCode = 1 byte, KeyLen = 2 bytes, ValLen = 2 bytes, 
			byte[] header = new byte[5];

			header[0] = 1; //OpCode 1 = Insert/Put
			BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(1, 2), (ushort)(key.Length));
			BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(3, 2), (ushort)(value.Length));

			_walStream.Write(header);
			_walStream.Write(key);
			_walStream.Write(value);

			//force the OS to physically write the data to the disk platter
			_walStream.Flush(flushToDisk: true);
		}

		///<summary>
		/// Logs a delete operation to the disk.
		/// </summary>
		public void LogDelete(ReadOnlySpan<byte> key)
		{
			// OpCode = 1 byte, KeyLen = 2 bytes
			byte[] header = new byte[3];

			header[0] = 2; // OpCode 2 = Delete
			BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(1, 2), (ushort)(key.Length));

			_walStream.Write(header);
			_walStream.Write(key);

			_walStream.Flush(flushToDisk: true);
		}

		///<summary>
		/// Empties the log file. Called after the B-tree has been safely written to disk. 
		/// </summary>
		public void Clear()
		{
			_walStream.SetLength(0);
		}

		private bool _disposed = false;

		public void Dispose()
		{
			if (!_disposed)
			{
				if (_walStream != null)
				{
					_walStream.Flush(flushToDisk: true);
					_walStream.Dispose();
				}
				_disposed = true;
			}
		}
	}
}
