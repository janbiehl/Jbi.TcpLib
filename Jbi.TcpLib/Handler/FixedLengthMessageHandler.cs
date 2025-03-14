namespace Jbi.TcpLib.Handler;

public sealed class FixedLengthMessageHandler 
	: IMessageHandler
{
	private readonly int _messageLength;
	private readonly Buffer _buffer;

	/// <inheritdoc />
	public int Length => _buffer.Position;

	public FixedLengthMessageHandler(int bufferSize, int messageLength)
	{
		if (messageLength > bufferSize)
		{
			throw new ArgumentOutOfRangeException(nameof(messageLength), messageLength,
				"The message length may not be larger than the buffer size");
		}
		
		_messageLength = messageLength;
		_buffer = new Buffer(bufferSize);
	}

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
			if (_buffer.Memory.Length < _messageLength)
			{
				messageFound = false;
				continue; // Leave the loop as there is no more data
			}
			
			var message = _buffer.Read(_messageLength);
			messages.Add(message);
			messageFound = true;
		} while (messageFound);

		return messages;
	}

	
	public void Reset()
	{
		_buffer.Clear();
	}
}