using System.Buffers;
using System.Buffers.Binary;

namespace Jbi.TcpLib.Serialization;

/// <summary>
/// Capable of writing data to a raw byte representation.
/// </summary>
public sealed class BinarySerializer : IDisposable
{
	private readonly IMemoryOwner<byte> _memoryOwner;
	private readonly Endianness _endianness;
	private int _currentPosition;

	/// <summary>
	/// Initializes a new instance of the <see cref="BinarySerializer"/> class.
	/// </summary>
	/// <param name="size">The initial size of the buffer.</param>
	/// <param name="endianness">The byte order to use for writing numeric values.</param>
	public BinarySerializer(Endianness endianness, int size = 1024)
	{
		_memoryOwner = MemoryPool<byte>.Shared.Rent(size);
		_endianness = endianness;
	}

	/// <summary>
	/// Appends a 16-bit signed integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(short value)
	{
		EnsureCapacity(sizeof(short));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteInt16LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteInt16BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(short);
	}

	/// <summary>
	/// Appends a 16-bit unsigned integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(ushort value)
	{
		EnsureCapacity(sizeof(ushort));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteUInt16LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteUInt16BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(ushort);
	}

	/// <summary>
	/// Appends a 32-bit signed integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(int value)
	{
		EnsureCapacity(sizeof(int));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteInt32LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteInt32BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(int);
	}

	/// <summary>
	/// Appends a 32-bit unsigned integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(uint value)
	{
		EnsureCapacity(sizeof(uint));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteUInt32BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(uint);
	}

	/// <summary>
	/// Appends a 64-bit signed integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(long value)
	{
		EnsureCapacity(sizeof(long));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteInt64LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteInt64BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(long);
	}

	/// <summary>
	/// Appends a 64-bit unsigned integer to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(ulong value)
	{
		EnsureCapacity(sizeof(ulong));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteUInt64LittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteUInt64BigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(ulong);
	}

	/// <summary>
	/// Appends a single-precision floating-point value to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(float value)
	{
		EnsureCapacity(sizeof(float));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteSingleLittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteSingleBigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(float);
	}

	/// <summary>
	/// Appends a double-precision floating-point value to the buffer.
	/// </summary>
	/// <param name="value">The value to append.</param>
	public void Append(double value)
	{
		EnsureCapacity(sizeof(double));
		if (_endianness == Endianness.LittleEndian)
		{
			BinaryPrimitives.WriteDoubleLittleEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		else
		{
			BinaryPrimitives.WriteDoubleBigEndian(_memoryOwner.Memory.Span[_currentPosition..], value);
		}
		_currentPosition += sizeof(double);
	}

	/// <summary>
	/// Appends a string to the buffer using ASCII encoding.
	/// </summary>
	/// <param name="span">The string to append.</param>
	public void Append(ReadOnlySpan<char> span)
	{
		EnsureCapacity(span.Length);
		
		foreach (var c in span)
		{
			_memoryOwner.Memory.Span[_currentPosition] = (byte)c;
			_currentPosition++;
		}
	}

	/// <summary>
	/// Append a value using its own binary serializer
	/// </summary>
	/// <param name="value">The value to serialize</param>
	/// <param name="serializer">The serialize to use for serialization</param>
	/// <typeparam name="T">Type of the value</typeparam>
	public void Append<T>(T value, IBinarySerializer<T> serializer)
	{
		serializer.Serialize(value, this);
	}

	/// <summary>
	/// Gets the memory containing the written data.
	/// </summary>
	/// <returns>A <see cref="PooledMemory{T}"/> containing the data.</returns>
	public PooledMemory<byte> GetMemory()
	{
		var memory = MemoryPool<byte>.Shared.Rent(_currentPosition);
		_memoryOwner.Memory[.._currentPosition].CopyTo(memory.Memory);
		return new PooledMemory<byte>(memory, memory.Memory[.._currentPosition]);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_memoryOwner.Dispose();
	}

	private void EnsureCapacity(int requiredSize)
	{
		if (_currentPosition + requiredSize > _memoryOwner.Memory.Length)
			throw new InvalidOperationException(
				$"There is not enough capacitiy to append '{requiredSize}' of bytes into the memory");
	}
}