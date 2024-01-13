using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;

namespace Jbi.TcpLib;

public sealed class TcpClient(IPEndPoint endPoint) : IDisposable
{
	private System.Net.Sockets.TcpClient? _client;
	private readonly CancellationTokenSource _cancellationTokenSource = new();

	public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (_client is not null)
			throw new InvalidOperationException("Client is already setup, close the connection first");

		_client = new System.Net.Sockets.TcpClient();

#if NET8_0_OR_GREATER
		await _client.ConnectAsync(endPoint, cancellationToken);
#else
		await _client.ConnectAsync(endPoint.Address, endPoint.Port);
#endif

	}

	public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadDataAsync(int bufferSize = 1024, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (_client is null || !_client.Connected)
			throw new InvalidOperationException("Client is not connected");

		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
		
		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		
		// Rent some memory from the memory pool (avoid allocations)
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(bufferSize);
		
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
		if (_client is null || !_client.Connected)
			throw new InvalidOperationException("Client is not connected");
		
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);

		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		return stream.WriteAsync(data, tokenSource.Token);
	}
	
	public async ValueTask SendDataAsync(IRawConvertable data, CancellationToken cancellationToken = default)
	{
		if (_client is null)
			throw new InvalidOperationException("Client is not connected");
		
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);

		// No using statement here, because we want to keep the stream open
		var stream = _client.GetStream();
		
		// Rent some memory from the memory pool (avoid allocations)
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(data.RequiredBufferSize);
		
		// Convert the data into raw data
		var sendBuffer = data.ConvertToRawData(memoryOwner.Memory);
		
		await stream.WriteAsync(sendBuffer, tokenSource.Token);
	}


	public void Disconnect()
	{
		_client?.Close();
		_client = null;
	}
	
	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		_client?.Dispose();
	}
}