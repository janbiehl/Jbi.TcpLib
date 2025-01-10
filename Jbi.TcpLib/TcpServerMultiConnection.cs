using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Jbi.TcpLib;

/// <summary>
/// Represents a TCP server. It is able to accept multiple clients at a time. The clients are identified by unique ids.
/// To write data to a client the user may provide the id of the client to write to.
/// </summary>
internal sealed class TcpServerMultiConnection(IPEndPoint endPoint) : IDisposable, IAsyncDisposable
{
	private readonly TcpListener _listener = new (endPoint);

	private readonly CancellationTokenSource _cancellationTokenSource = new ();

	private readonly ConcurrentDictionary<Guid, System.Net.Sockets.TcpClient> _clients = new ();

	public Encoding Encoding { get; set; } = Encoding.UTF8;

	private Task? _listeningTask;

	public void Start()
	{
		_listener.Start();
		_listeningTask = ListenAsync(_cancellationTokenSource.Token);
	}

	private async Task ListenAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var client = await _listener.AcceptTcpClientAsync(cancellationToken);
			RegisterClient(client);
		}
	}

	public async IAsyncEnumerable<(Guid ClientId, ReadOnlyMemory<byte> data)> ReadDataAsync(int bufferSize = 1024,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(bufferSize);

		using var tokenSource =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

		while (!tokenSource.IsCancellationRequested)
		{
			foreach (var (clientId, client) in _clients)
			{
				// No using here, as we want to keep the stream open
				var stream = client.GetStream();

				var bytesRead = await stream.ReadAsync(memoryOwner.Memory, tokenSource.Token);
				yield return (clientId, memoryOwner.Memory[..bytesRead]);
			}
		}
	}

	private static async Task SendDataCoreAsync(System.Net.Sockets.TcpClient client, ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken = default)
	{
		// No using statement here, as we want to keep the stream open for later use
		var stream = client.GetStream();
		await stream.WriteAsync(data, cancellationToken);
	}
	
	public async Task<bool> SendDataAsync(Guid clientId, ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken = default)
	{
		if (!_clients.TryGetValue(clientId, out var client))
		{
			return false;
		}

		await SendDataCoreAsync(client, data, cancellationToken);
		return true;
	}

	public async Task<bool> SendDataAsync(Guid clientId, string data, CancellationToken cancellationToken = default)
	{
		if (!_clients.TryGetValue(clientId, out var client))
		{
			return false;
		}
		
		var requiredLength = Encoding.GetByteCount(data);
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(requiredLength);
		var encodedLength = Encoding.GetBytes(data, memoryOwner.Memory.Span);
		await SendDataCoreAsync(client, memoryOwner.Memory[..encodedLength], cancellationToken);
		return true;
	}
	
	public async Task<bool> SendDataAsync(Guid clientId, IRawConvertable data, CancellationToken cancellationToken = default)
	{
		if (!_clients.TryGetValue(clientId, out var client))
		{
			return false;
		}
		
		// Rent some memory from the memory pool (avoid allocations)
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(data.RequiredBufferSize);
		
		// Convert the data into raw data
		var sendBuffer = data.ConvertToRawData(memoryOwner.Memory);

		await SendDataCoreAsync(client, sendBuffer, cancellationToken);
		return true;
	}

	public void Stop()
	{
		if (!_cancellationTokenSource.IsCancellationRequested)
		{
			_cancellationTokenSource.Cancel();
		}
		
		_cancellationTokenSource.Dispose();
		_listeningTask?.Dispose();
		
		foreach (var (_, client) in _clients)
		{
			client.Dispose();
		}
		_clients.Clear();
		_listener.Dispose();
	}
	
	public async Task StopAsync()
	{
		if (!_cancellationTokenSource.IsCancellationRequested)
		{
			await _cancellationTokenSource.CancelAsync();
		}
		
		_cancellationTokenSource.Dispose();
		_listeningTask?.Dispose();

		foreach (var (_, client) in _clients)
		{
			client.Dispose();
		}
		_clients.Clear();
		_listener.Dispose();
	}

	private void RegisterClient(System.Net.Sockets.TcpClient client)
	{
		var clientId = Guid.NewGuid();
		_clients.AddOrUpdate(clientId, client, (_, _) => client);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Stop();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await StopAsync();
	}
}