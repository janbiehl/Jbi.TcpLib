using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Jbi.TcpLib;

/// <summary>
/// Represents a simple TCP server. It is able to accept one client at a time, and read/write data from/to it.
/// It has no interpreting logic, so it is up to the user to interpret the raw data that was received.
/// Also the user has to convert the data into raw data before sending it. 
/// </summary>
/// <param name="endPoint"></param>
public sealed class TcpServer(IPEndPoint endPoint) : IDisposable
{
	/// <summary>
	/// The listener that is used to listen for incoming connections
	/// </summary>
	private readonly TcpListener _listener = new(endPoint);
	
	/// <summary>
	/// The cancellation token source that is used to cancel the background operations, when the server is stopped
	/// </summary>
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	
	/// <summary>
	/// The client that is connected to the server or null, if no client is connected
	/// </summary>
	private System.Net.Sockets.TcpClient? _client;

	/// <summary>
	/// Starts the server, so that it is able to accept incoming connections
	/// </summary>
	public void Start() => _listener.Start();

	/// <summary>
	/// Waits for a client to connect to the server
	/// </summary>
	/// <param name="cancellationToken">Used to cancel the long running operations</param>
	/// <exception cref="InvalidOperationException">There is already a client connected</exception>
	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (_client is not null)
			throw new InvalidOperationException("Client is already connected");

#if NET8_0_OR_GREATER
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
		_client = await _listener.AcceptTcpClientAsync(tokenSource.Token);
#else
		_client = await _listener.AcceptTcpClientAsync();
#endif
	}

	/// <summary>
	/// Reads data from the currently connected client asynchronously
	/// Here we are using the <see cref="IAsyncEnumerable{T}"/> interface, so that we can use the await foreach statement.
	/// So the caller has a async foreach loop, that is able to read data from the client asynchronously as it was read without the need of callbacks or stuff.
	/// </summary>
	/// <param name="cancellationToken">Used to cancel the long running operations</param>
	/// <returns>A async enumerable that always provides the latest data blocks</returns>
	/// <exception cref="InvalidOperationException">There is no client connected</exception>
	public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (_client is null)
			throw new InvalidOperationException("Client is not connected");

		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
		// Rent some memory from the memory pool (avoid allocations)
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);
		
		// No using statement here, because we do not want to dispose the stream here
		var stream = _client.GetStream();
		
		// Read data until the cancellation token is requested
		while (!tokenSource.IsCancellationRequested)
		{
			// Read data from the stream asynchronously
			var bytesRead = await stream.ReadAsync(memoryOwner.Memory, tokenSource.Token);

			// The connection is closed
			if (bytesRead == 0)
				break;
			
			yield return memoryOwner.Memory[..bytesRead];
		}
	}

	public ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
	{
		if (_client is null)
			throw new InvalidOperationException("Client is not connected");
		
		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		return stream.WriteAsync(data, cancellationToken);
	}

	public async ValueTask SendDataAsync(IRawConvertable data, CancellationToken cancellationToken = default)
	{
		if (_client is null)
			throw new InvalidOperationException("Client is not connected");
		
		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		
		// Rent some memory from the memory pool (avoid allocations)
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(data.RequiredBufferSize);
		
		// Convert the data into raw data
		var sendBuffer = data.ConvertToRawData(memoryOwner.Memory);
		
		await stream.WriteAsync(sendBuffer, cancellationToken);
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public async ValueTask StopAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
#if NET8_0_OR_GREATER
		await _cancellationTokenSource.CancelAsync();
#else
		_cancellationTokenSource.Cancel();
#endif
		
		_cancellationTokenSource.Dispose();
		
		// Reset the client
		_client?.Dispose();
		_client = null;
		
		_listener.Stop();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_client?.Dispose();
		_listener.Stop();
	}
}