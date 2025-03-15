using System.Text;

namespace Jbi.TcpLib.Handler;

/// <summary>
/// Searches for messages by using delimiters. There are two possibilities to use delimiters.
/// There might be a single delimiter - Messages Are Read until the first delimiter
/// </summary>
/// <remarks>
/// single delimiter = HelloWorld\0This is a test!\0 - Here will be two messages read.
/// multiple delimiter = {start}HelloWorld{end}{start}This is a test{end}
/// </remarks>
public sealed class DelimiterMessageHandler : IMessageHandler, IDisposable
{
	private readonly Buffer _buffer;

	private readonly byte[]? _delimititerStartBytes;

	private readonly byte[] _delimiterEndBytes;

	public int Length => _buffer.Position;

	public DelimiterMessageHandler(int bufferSize, Encoding encoding, string delimiter)
	{
		_buffer = new Buffer(bufferSize);
		_delimititerStartBytes = null;
		_delimiterEndBytes = encoding.GetBytes(delimiter);
	}

	public DelimiterMessageHandler(int bufferSize, Encoding encoding, string startDelimiter, string endDelimiter)
	{
		_buffer = new Buffer(bufferSize);
		_delimititerStartBytes = encoding.GetBytes(startDelimiter);
		_delimiterEndBytes = encoding.GetBytes(endDelimiter);
	}

	/// <inheritdoc />
	public void AppendBytes(params ReadOnlySpan<byte> bytes)
	{
		if (!_buffer.HasCapacity(bytes.Length))
		{
			throw new InvalidOperationException($"The buffer can not hold '{bytes.Length}' bytes");
		}
		
		_buffer.Write(bytes);
	}

	/// <inheritdoc />
	public IReadOnlyCollection<PooledMemory<byte>> CheckForMessages()
	{
		List<PooledMemory<byte>> messages = [];
		do
		{
			var message = _delimititerStartBytes is null ? ReadMessageSingleDelimiter() : ReadMessageMultipleDelimiter();

			if (!message.HasValue)
			{
				break;
			}
			
			messages.Add(message.Value);
		} while (true);

		return messages;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_buffer.Dispose();
	}

	/// <summary>
	/// Read the data that is in front of the first delimiter
	/// </summary>
	/// <returns></returns>
	private PooledMemory<byte>? ReadMessageSingleDelimiter()
	{
		if (_buffer.Memory.Length < _delimiterEndBytes.Length)
		{
			// There is not enough data in the buffer to hold a full delimiter
			return null;
		}
		
		var delimiterIndex = _buffer.Memory.Span.IndexOf(_delimiterEndBytes);
		switch (delimiterIndex)
		{
			case -1: // No delimiter found
				return null;
			case 0: // delimiter before any data
				_buffer.RemoveLeading(_delimiterEndBytes.Length);
				return null;
			default: // 
				var data =  _buffer.Read(delimiterIndex);
				_buffer.RemoveLeading(_delimiterEndBytes.Length);
				return data;
		}
	}

	/// <summary>
	/// Read the data that is between the start and the end delimite. Remove any left over data snippets.
	/// </summary>
	/// <returns></returns>
	private PooledMemory<byte>? ReadMessageMultipleDelimiter()
	{
		while (true)
		{
			// end delimiter is a sign, that there might be valid data
			var endDelimiterIndex = _buffer.Memory.Span.IndexOf(_delimiterEndBytes);
			if (endDelimiterIndex == -1) 
				return null; // There is no end delimiter
			
			var startDelimiterIndex = _buffer.Memory.Span.IndexOf(_delimititerStartBytes);
			if (startDelimiterIndex == -1)
			{
				// There is an end delimiter but no start delimiter - something must be wrong
				// Delete everything before and also the ending delimiter itself
				_buffer.RemoveLeading(endDelimiterIndex + _delimiterEndBytes.Length);
				continue; // Check again
			}

			if (startDelimiterIndex > endDelimiterIndex)
			{
				// There is a start delimiter, but it is located after the end delimiter
				_buffer.RemoveLeading(startDelimiterIndex);
				continue; // Check again
			}

			if (startDelimiterIndex > 0)
			{
				// It seems that there is some data before the start indicator - abcdef<start>....<end> - remove abcdef
				_buffer.RemoveLeading(startDelimiterIndex);
			}

			// We got a well formatted message
			// <start>abcdef<end>
			_buffer.RemoveLeading(_delimititerStartBytes!.Length);
			
			// abcdef<end>
			var content = _buffer.Read(endDelimiterIndex + 1 - _delimititerStartBytes.Length);

			// <end>
			_buffer.RemoveLeading(_delimiterEndBytes.Length);

			return content;
		}
	}

	/// <inheritdoc />
	public void Reset()
	{
		_buffer.Clear();
	}
}