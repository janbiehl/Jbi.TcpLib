using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Jbi.TcpLib;

/// <summary>
/// A tcp client that is able to connect to a tcp server. It can receive data from the opposite and also
/// send data to it.
/// </summary>
/// <param name="endPoint">The remote endpoint for the server</param>
public sealed class TcpClient(IPEndPoint endPoint) : IDisposable
{
	/// <summary>
	/// The client 
	/// </summary>
	private System.Net.Sockets.TcpClient? _client;
	
	/// <summary>
	/// It is used to cancel long-running operations 
	/// </summary>
	private readonly CancellationTokenSource _cancellationTokenSource = new();

	/// <summary>
	/// Encoding to be used when writing strings to the wire 
	/// </summary>
	public Encoding Encoding { get; set; } = Encoding.UTF8;
	
	/// <summary>
	/// Connect to the remote endpoint. Connect must be called before another operation is possible
	/// </summary>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	/// <exception cref="InvalidOperationException"></exception>
	public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(ConnectAsync)}");
		
		if (_client is not null && _client.Connected)
			throw new InvalidOperationException("Client is already setup, close the connection first");

		_client?.Dispose(); // release the existing client
		_client = new System.Net.Sockets.TcpClient();

		await _client.ConnectAsync(endPoint, cancellationToken);
	}

	/// <summary>
	/// Read all data chunks that are received by the client in an async way
	/// </summary>
	/// <param name="bufferSize">Defines the maximum chunk size of raw data</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	/// <returns>async enumerable memory chunks</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public async IAsyncEnumerable<PooledMemory<byte>> ReadDataAsync(int bufferSize = 1024, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(ReadDataAsync)}");
		
		if (_client is null || !_client.Connected)
			throw new InvalidOperationException("Client is not connected");

		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
		
		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		
		while (!tokenSource.IsCancellationRequested)
		{
			using var innerActivity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(ReadDataAsync)}Inner");
			
			// Rent some memory from the memory pool (avoid allocations)
			// We are not disposing here -> The consumer is responsible of doing so
			var memoryOwner = MemoryPool<byte>.Shared.Rent(bufferSize);
			
			// Read data from the stream asynchronously
			var bytesRead = await stream.ReadAsync(memoryOwner.Memory, tokenSource.Token);

			// The connection is closed
			if (bytesRead == 0)
				break;
			
			yield return new PooledMemory<byte>(memoryOwner, memoryOwner.Memory[..bytesRead]);
		}
	}

	/// <summary>
	/// Send raw data to the remote partner
	/// </summary>
	/// <param name="data">The raw data to send</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	/// <returns>A task for the operation</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(SendDataAsync)}");
		
		if (_client is null || !_client.Connected)
			throw new InvalidOperationException("Client is not yet connected");
		
		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		return stream.WriteAsync(data, cancellationToken);
	}

	/// <summary>
	/// Send a string to the remote partner
	/// </summary>
	/// <param name="data">String to send to the remote part</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	public async Task SendDataAsync(string data, CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(SendDataAsync)}");
		
		var requiredLength = Encoding.GetByteCount(data);
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(requiredLength);
		var encodedLength = Encoding.GetBytes(data, memoryOwner.Memory.Span);
		await SendDataAsync(memoryOwner.Memory[..encodedLength], cancellationToken);
	}
	
	/// <summary>
	/// Close the client connection
	/// </summary>
	public void Disconnect()
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpClient)}.{nameof(Disconnect)}");
		_client?.Close();
		_client?.Dispose();
		_client = null;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_cancellationTokenSource.IsCancellationRequested)
		{
			_cancellationTokenSource.Cancel();
		}
		
		_cancellationTokenSource.Dispose();
		_client?.Dispose();
	}
}