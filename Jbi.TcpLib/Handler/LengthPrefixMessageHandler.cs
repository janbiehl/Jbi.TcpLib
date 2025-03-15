using System.Buffers.Binary;
using Jbi.TcpLib.Serialization;

namespace Jbi.TcpLib.Handler;

public sealed class LengthPrefixMessageHandler(int bufferSize, LengthPrefixMessageHandler.PrefixType prefixType, Endianness endianness)
	: IMessageHandler, IDisposable
{
	private readonly Buffer _buffer = new (bufferSize);
	private readonly PrefixType _prefixType = prefixType;
	private readonly Endianness _endianness = endianness;

	/// <inheritdoc />
	public int Length => _buffer.Position;

	public void AppendBytes(ReadOnlySpan<byte> bytes)
	{
		if (!_buffer.HasCapacity(bytes.Length))
		{
			throw new InvalidOperationException($"The buffer can not hold '{bytes.Length}' bytes");
		}
		
		_buffer.Write(bytes);
	}

	public IReadOnlyCollection<PooledMemory<byte>> CheckForMessages()
	{
		List<PooledMemory<byte>> messages = [];
		bool messageFound;
		do
		{
			var messageLengthByteAmount = GetNumberOfBytesForMessageLength();

			if (_buffer.Memory.Length < messageLengthByteAmount)
				break;
			
			var messageLength = GetMessageLength();
		
			if (messageLength > _buffer.Size)
			{
				throw new InvalidOperationException("The message size is to large. Increase the buffer size or reduce the message length");
			}
		
			if (_buffer.Memory.Length < messageLengthByteAmount + messageLength)
			{
				messageFound = false;
				continue; // Leave the loop as there is no more data
			}

			// Beyond here we know that there is a full message received
			switch (_prefixType)
			{
				case PrefixType.Short:
					_buffer.RemoveLeading(sizeof(short));
					break;
				case PrefixType.Int:
					_buffer.RemoveLeading(sizeof(int));
					break;
				case PrefixType.Long:
					_buffer.RemoveLeading(sizeof(long));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			var message = _buffer.Read((int) messageLength);
			messages.Add(message);
			messageFound = true;
		} while (messageFound);

		return messages;
	}

	public void Reset()
	{
		_buffer.Clear();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_buffer.Dispose();
	}

	private long GetMessageLength()
	{
		return _prefixType switch
		{
			PrefixType.Short => _endianness == Endianness.BigEndian ? BinaryPrimitives.ReadInt16BigEndian(_buffer.Memory.Span) : BinaryPrimitives.ReadInt16LittleEndian(_buffer.Memory.Span),
			PrefixType.Int => _endianness == Endianness.BigEndian ? BinaryPrimitives.ReadInt32BigEndian(_buffer.Memory.Span) : BinaryPrimitives.ReadInt32LittleEndian(_buffer.Memory.Span),
			PrefixType.Long => _endianness == Endianness.BigEndian ? BinaryPrimitives.ReadInt64BigEndian(_buffer.Memory.Span) : BinaryPrimitives.ReadInt64LittleEndian(_buffer.Memory.Span),
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	private int GetNumberOfBytesForMessageLength()
	{
		return _prefixType switch
		{
			PrefixType.Short => 2,
			PrefixType.Int => 4,
			PrefixType.Long => 8,
			_ => throw new ArgumentOutOfRangeException()
		};
	}
	
	public enum PrefixType
	{
		/// <summary>
		/// 16 bit integer value 
		/// </summary>
		Short,
		/// <summary>
		/// 32 bit integer value
		/// </summary>
		Int,
		/// <summary>
		/// 64 bit integer value
		/// </summary>
		Long
	}
}