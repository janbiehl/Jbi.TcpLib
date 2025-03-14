using System.Buffers;

namespace Jbi.TcpLib;

/// <summary>
/// Buffer that can be filled in sequence.
/// </summary>
/// <param name="size">The required buffer size</param>
/// <summary>
/// Represents a fixed-size buffer that can be written to and read from.
/// This class uses memory pooling for efficient memory management.
/// </summary>
public sealed class Buffer(int size) : IDisposable
{
    private readonly IMemoryOwner<byte> _buffer = MemoryPool<byte>.Shared.Rent(size);

    /// <summary>
    /// Gets a memory view of the valid data within the buffer.
    /// </summary>
    public Memory<byte> Memory => _buffer.Memory[..Position]; // Only expose valid data

    /// <summary>
    /// Gets the current position within the buffer.  This represents the end of the valid data.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Gets the total size of the buffer (allocated memory).
    /// </summary>
    public int Size { get; } = size;

    /// <summary>
    /// Checks that the buffer has the capacity to hold more data
    /// </summary>
    /// <param name="amount">The amount of bytes to check</param>
    /// <returns>True when there is enough capacity remaining</returns>
    public bool HasCapacity(int amount) => Position + amount <= Size;

    /// <summary>
    /// Reset the buffer to an empty state
    /// </summary>
    public void Clear()
    {
        Position = 0;
        _buffer.Memory.Span.Clear();
    }

    /// <summary>
    /// Remove the leading amount of bytes from the buffer.
    /// </summary>
    /// <param name="amount">The number of bytes to remove from the left</param>
    public void RemoveLeading(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        
        if (amount > Position)
        {
            throw new InvalidOperationException("There aren't enough bytes to remove");
        }

        Memory[amount..].CopyTo(_buffer.Memory);
        Position -= amount;
    }
    
    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="span">The data to write.</param>
    /// <exception cref="InvalidOperationException">Thrown if the buffer is full.</exception>
    public void Write(ReadOnlySpan<byte> span)
    {
        if (!HasCapacity(span.Length))
        {
            throw new InvalidOperationException("Buffer has no capacity");
        }
        
        span.CopyTo(_buffer.Memory[Position..].Span);
        Position += span.Length;
    }

    /// <summary>
    /// Reads data from the buffer. The read data is removed from the buffer.
    /// </summary>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>A <see cref="PooledMemory{T}"/> containing the read data. The caller is responsible for disposing of this instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative or zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown if not enough data is available in the buffer.</exception>
    public PooledMemory<byte> Read(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (Position < count)
        {
            throw new InvalidOperationException("Not enough data is stored in the buffer");
        }

        var rentedMemory = MemoryPool<byte>.Shared.Rent(count);

        _buffer.Memory[..count].CopyTo(rentedMemory.Memory);

        //Efficiently shift the remaining data
        var remainingData = _buffer.Memory[count..];
        remainingData.CopyTo(_buffer.Memory);
        
        Position -= count; // adjust the cursor

        _buffer.Memory[Position..].Span.Clear(); // Clear the non used memory
        
        return new PooledMemory<byte>(rentedMemory, rentedMemory.Memory[..count]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _buffer.Dispose();
    }
}