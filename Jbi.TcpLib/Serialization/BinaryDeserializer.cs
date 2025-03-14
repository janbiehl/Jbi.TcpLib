using System.Buffers.Binary;
using System.Text;

namespace Jbi.TcpLib.Serialization;

/// <summary>
/// Capable of reading data from a raw byte representation.
/// </summary>
/// <remarks>
/// This class provides methods to read various data types (strings, integers, floats, etc.) from a byte buffer,
/// taking into account the specified endianness.
/// </remarks>
public sealed class BinaryDeserializer
{
    private readonly Endianness _endianness;
    private readonly ReadOnlyMemory<byte> _buffer;
    private int _currentPosition;

    /// <summary>
    /// Capable of reading data from a raw byte representation.
    /// </summary>
    /// <remarks>
    /// This class provides methods to read various data types (strings, integers, floats, etc.) from a byte buffer,
    /// taking into account the specified endianness.
    /// </remarks>
    /// <param name="endianness">The byte order that we expect the data to have.</param>
    /// <param name="buffer">The buffer we want to read from.</param>
    public BinaryDeserializer(ReadOnlyMemory<byte> buffer, Endianness endianness)
    {
	    _endianness = endianness;
	    _buffer = buffer;
    }

    /// <summary>
    /// Skip the number of bytes from the current position.
    /// </summary>
    /// <param name="count">The number of bytes to skip.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative or zero.</exception>
    public void Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _currentPosition += count;
    }

    /// <summary>
    /// Read a string from buffer.
    /// </summary>
    /// <param name="amount">Number of bytes to read.</param>
    /// <param name="encoding">Encoding to use to retrieve the string.</param>
    /// <returns>The retrieved string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when encoding is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when amount is negative or zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public string ReadString(int amount, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        EnsureLength((uint)amount);

        var value = encoding.GetString(_buffer.Span.Slice(_currentPosition, amount));
        _currentPosition += amount;
        return value;
    }

    /// <summary>
    /// Reads a 16-bit signed integer from the buffer.
    /// </summary>
    /// <returns>The 16-bit signed integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public short ReadShort()
    {
        EnsureLength(sizeof(short));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadShortLittleEndian(),
            Endianness.BigEndian => ReadShortBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer from the buffer.
    /// </summary>
    /// <returns>The 16-bit unsigned integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public ushort ReadUShort()
    {
        EnsureLength(sizeof(ushort));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadUShortLittleEndian(),
            Endianness.BigEndian => ReadUShortBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a 32-bit signed integer from the buffer.
    /// </summary>
    /// <returns>The 32-bit signed integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public int ReadInt()
    {
        EnsureLength(sizeof(int));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadIntLittleEndian(),
            Endianness.BigEndian => ReadIntBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from the buffer.
    /// </summary>
    /// <returns>The 32-bit unsigned integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public uint ReadUInt()
    {
        EnsureLength(sizeof(uint));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadUIntLittleEndian(),
            Endianness.BigEndian => ReadUIntBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a 64-bit signed integer from the buffer.
    /// </summary>
    /// <returns>The 64-bit signed integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public long ReadLong()
    {
        EnsureLength(sizeof(long));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadLongLittleEndian(),
            Endianness.BigEndian => ReadLongBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer from the buffer.
    /// </summary>
    /// <returns>The 64-bit unsigned integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public ulong ReadULong()
    {
        EnsureLength(sizeof(ulong));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadULongLittleEndian(),
            Endianness.BigEndian => ReadULongBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a single-precision floating-point value from the buffer.
    /// </summary>
    /// <returns>The single-precision floating-point value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public float ReadFloat()
    {
        EnsureLength(sizeof(float));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadFloatLittleEndian(),
            Endianness.BigEndian => ReadFloatBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Reads a double-precision floating-point value from the buffer.
    /// </summary>
    /// <returns>The double-precision floating-point value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer does not contain enough data.</exception>
    public double ReadDouble()
    {
        EnsureLength(sizeof(double));

        return _endianness switch
        {
            Endianness.LittleEndian => ReadDoubleLittleEndian(),
            Endianness.BigEndian => ReadDoubleBigEndian(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Read a complex value from the buffer, using a specilised deserializer
    /// </summary>
    /// <param name="deserializer">The deserializer to use</param>
    /// <typeparam name="T">The type of value to read</typeparam>
    /// <returns>The value read</returns>
    public T Read<T>(IBinaryDeserializer<T> deserializer)
    {
	    // No need to advance the position here. As the method uses us. 
	    return deserializer.Deserialize(this);
    }

    private short ReadShortLittleEndian()
    {
        var value = BinaryPrimitives.ReadInt16LittleEndian(_buffer[_currentPosition..].Span);
        _currentPosition += sizeof(short);
        return value;
    }

    private short ReadShortBigEndian()
    {
        var value = BinaryPrimitives.ReadInt16BigEndian(_buffer[_currentPosition..].Span);
        _currentPosition += sizeof(short);
        return value;
    }

    private ushort ReadUShortLittleEndian()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer[_currentPosition..].Span);
        _currentPosition += sizeof(ushort);
        return value;
    }

    private ushort ReadUShortBigEndian()
    {
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[_currentPosition..].Span);
        _currentPosition += sizeof(ushort);
        return value;
    }

    private int ReadIntLittleEndian()
	{
		var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(int);
		return value;
	}

	private int ReadIntBigEndian()
	{
		var value = BinaryPrimitives.ReadInt32BigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(int);
		return value;
	}

	private uint ReadUIntLittleEndian()
	{
		var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(uint);
		return value;
	}

	private uint ReadUIntBigEndian()
	{
		var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(uint);
		return value;
	}

	private long ReadLongLittleEndian()
	{
		var value = BinaryPrimitives.ReadInt64LittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(long);
		return value;
	}

	private long ReadLongBigEndian()
	{
		var value = BinaryPrimitives.ReadInt64BigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(long);
		return value;
	}

	private ulong ReadULongLittleEndian()
	{
		var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(ulong);
		return value;
	}

	private ulong ReadULongBigEndian()
	{
		var value = BinaryPrimitives.ReadUInt64BigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(ulong);
		return value;
	}

	private float ReadFloatLittleEndian()
	{
		var value = BinaryPrimitives.ReadSingleLittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(float);
		return value;
	}

	private float ReadFloatBigEndian()
	{
		var value = BinaryPrimitives.ReadSingleBigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(float);
		return value;
	}

	private double ReadDoubleLittleEndian()
	{
		var value = BinaryPrimitives.ReadDoubleLittleEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(double);
		return value;
	}

	private double ReadDoubleBigEndian()
	{
		var value = BinaryPrimitives.ReadDoubleBigEndian(_buffer[_currentPosition..].Span);
		_currentPosition += sizeof(double);
		return value;
	}

	private void EnsureLength(uint requiredLength)
	{
		if (_currentPosition + requiredLength > _buffer.Length)
			throw new InvalidOperationException(
				$"The buffer does not contain enough data to read '{requiredLength}' bytes");
	}
}