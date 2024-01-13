namespace Jbi.TcpLib;

public interface IRawConvertable
{
	/// <summary>
	/// The amount of bytes that are required to store the object in binary form.
	/// </summary>
	int RequiredBufferSize { get; }
	
	/// <summary>
	/// Converts the object to a binary representation.
	/// </summary>
	/// <param name="buffer">The binary data will be written into this buffer</param>
	ReadOnlyMemory<byte> ConvertToRawData(Memory<byte> buffer);
}

public interface IRawConvertable<out T> : IRawConvertable
{
	/// <summary>
	/// Converts the binary representation to an object.
	/// </summary>
	/// <param name="data">The binary data</param>
	/// <returns>The object</returns>
	T ConvertFromRawData(ReadOnlySpan<byte> data);
}
