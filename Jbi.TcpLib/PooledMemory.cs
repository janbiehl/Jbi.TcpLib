using System.Buffers;

namespace Jbi.TcpLib;

/// <summary>
/// Wrapper around pooled memory. The whole purpose is to indicate consumers of an API to dispose
/// memory portions the received to free the memory.
/// </summary>
/// <param name="memoryOwner">Owns the memory block of the payload</param>
/// <param name="memory">The payload data</param>
/// <typeparam name="T">Type of memory</typeparam>
public readonly struct PooledMemory<T>(IMemoryOwner<T> memoryOwner, ReadOnlyMemory<T> memory)
	: IDisposable
{
	private readonly IMemoryOwner<T> _memoryOwner = memoryOwner;
	public ReadOnlyMemory<T> Memory { get; } = memory;
	public ReadOnlySpan<T> Span => Memory.Span;

	/// <inheritdoc />
	public void Dispose()
	{
		_memoryOwner.Dispose();
	}
}