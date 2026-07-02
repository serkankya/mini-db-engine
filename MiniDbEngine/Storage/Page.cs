using System.Buffers.Binary;

namespace MiniDbEngine.Storage
{
	public class Page
	{
		public const int PageSize = 4096;
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
			FreeSpaceOffset = PageSize;
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

		public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, PageSize);

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
	}
}
