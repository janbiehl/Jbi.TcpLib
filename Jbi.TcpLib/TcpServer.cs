using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using ClientRead = (System.Guid ClientId,  System.ReadOnlyMemory<byte> Data);

namespace Jbi.TcpLib;

public sealed class TcpServer(IPEndPoint endPoint, int concurrentClients = 10)
	: IDisposable, IAsyncDisposable
{
	private readonly IPEndPoint _endpoint = endPoint;
	private readonly int _concurrentClients = concurrentClients;
	private readonly TcpListener _tcpListener = new (endPoint);

	private readonly ConcurrentDictionary<Guid, (System.Net.Sockets.TcpClient client, Task clientHandler)> _clients =
		new ();

	private readonly Channel<ClientRead> _receivedChannel = Channel.CreateBounded<ClientRead>(
		new BoundedChannelOptions(100)
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.Wait
		});

	private bool _isListening;
	private Task? _executeTask;
	private CancellationTokenSource? _stoppingCts;

	public Encoding Encoding { get; set; } = Encoding.UTF8;

	public IPAddress Address => _endpoint.Address;

	public int Port => _endpoint.Port;

	public ICollection<Guid> ClientIds => _clients.Keys;


	/// <summary>
	/// Start listening for incoming connections. This method does return immediately,
	/// but there will be a task running in background. This Task will accept connections and handle the clients
	/// in the background.
	/// </summary>
	/// <param name="cancellationToken">Cancel long-running operations, also all running background tasks</param>
	/// <returns>A task that returns immediately</returns>
	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(StartAsync)}");
		if (_isListening)
		{
			throw new InvalidOperationException("Server is already listening");
		}

		_isListening = true;
		_tcpListener.Start();

#pragma warning disable S2930
		_stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore S2930

		_executeTask = HandleConnectionsAsync(_stoppingCts.Token);

		if (_executeTask.IsCompleted)
		{
			return _executeTask;
		}

		Metrics.RegisterServerInstance();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Stop listening for new connections. Also stop all tasks that are running in background
	/// </summary>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(StopAsync)}");
		
		if (_isListening)
		{
			return;
		}

		_isListening = false;

		if (_executeTask is null)
		{
			return;
		}

		Metrics.UnregisterServerInstance();

		try
		{
			await _stoppingCts!.CancelAsync();
		}
		finally
		{
			await _executeTask.WaitAsync(cancellationToken)
				.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}

	/// <summary>
	/// Read all received data chunks as an async enumerable. The client id can be used to identify which client has sent the data.
	/// </summary>
	/// <param name="cancellationToken">Cancel long-running operation</param>
	/// <returns>A tuple containing the received data chunk and a client id</returns>
	public async IAsyncEnumerable<ClientRead> ReadDataAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(ReadDataAsync)}");
		await foreach (var valueTuple in _receivedChannel.Reader.ReadAllAsync(cancellationToken))
		{
			yield return valueTuple;
		}
	}

	/// <summary>
	/// Send raw data to the remote partner
	/// </summary>
	/// <param name="clientId">The id for the client to write to</param>
	/// <param name="data">The raw data to send</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	/// <returns>A task for the operation</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public ValueTask SendDataAsync(Guid clientId, ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(SendDataAsync)}");
		
		if (!_clients.TryGetValue(clientId, out var client))
		{
			throw new InvalidOperationException("Client did not exist");
		}

		// No using statement here, as we want to keep the stream open
		var stream = client.client.GetStream();
		return stream.WriteAsync(data, cancellationToken);
	}

	/// <summary>
	/// Send a string to the remote partner
	/// </summary>
	/// <param name="clientId">The id for the client to write to</param>
	/// <param name="data">String to send to the remote part</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	public async ValueTask SendDataAsync(Guid clientId, string data, CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(SendDataAsync)}");
		
		var requiredLength = Encoding.GetByteCount(data);
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(requiredLength);
		var encodedLength = Encoding.GetBytes(data, memoryOwner.Memory.Span);
		await SendDataAsync(clientId, memoryOwner.Memory[..encodedLength], cancellationToken);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_stoppingCts?.Cancel();
		_tcpListener.Dispose();

		_executeTask?.Wait(TimeSpan.FromSeconds(5));
		foreach (var (_, clientHandler) in _clients.Values)
		{
#pragma warning disable S4462
			clientHandler.Wait(TimeSpan.FromSeconds(5));
#pragma warning restore S4462
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_stoppingCts is not null)
		{
			await _stoppingCts.CancelAsync();
		}

		_tcpListener.Dispose();

		List<Task> tasks = [];
		if (_executeTask is not null)
		{
			tasks.Add(_executeTask.WaitAsync(TimeSpan.FromSeconds(5)));
		}

		tasks.AddRange(_clients.Values.Select(x => x.clientHandler.WaitAsync(TimeSpan.FromSeconds(5))));

		await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
	}

	/// <summary>
	/// Background task that is responsible for accepting new clients.
	/// </summary>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	private async Task HandleConnectionsAsync(CancellationToken cancellationToken)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(HandleConnectionsAsync)}");
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_clients.Count >= _concurrentClients)
				{
					// We cant handle any more clients
					await Task.Delay(100, cancellationToken);
					continue;
				}

				var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
				var clientId = Guid.NewGuid();
				var clientHandlerTask =
					HandleTcpClientAsync(clientId, tcpClient, _receivedChannel.Writer, cancellationToken);

				// We do not need to store the client, as the handler went down immediately
				if (clientHandlerTask.IsCompleted)
				{
					continue;
				}

				Metrics.RegisterServerClientInstance();
				_clients.TryAdd(clientId, (tcpClient, clientHandlerTask));
			}
		}
		catch (OperationCanceledException)
		{
			// Ignore this as this is expected
		}
	}

	/// <summary>
	/// Background task that is responsible for reading data from a client
	/// </summary>
	/// <param name="clientId">The clients id</param>
	/// <param name="tcpClient">The client itself</param>
	/// <param name="dataChannel">Received data will be written here</param>
	/// <param name="cancellationToken">Cancel long-running operation</param>
	private async Task HandleTcpClientAsync(Guid clientId, System.Net.Sockets.TcpClient tcpClient,
		ChannelWriter<ClientRead> dataChannel, CancellationToken cancellationToken)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(HandleTcpClientAsync)}");
		var stream = tcpClient.GetStream();
		using var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);

		try
		{
			int bytesRead;
			while ((bytesRead = await stream.ReadAsync(memoryOwner.Memory, cancellationToken)) > 0)
			{
				await dataChannel.WriteAsync((clientId, memoryOwner.Memory[..bytesRead].ToArray()), cancellationToken);
			}
		}
		finally
		{
			ReleaseClient(clientId);
		}
	}

	/// <summary>
	/// Free up client resources
	/// </summary>
	/// <param name="clientId">The client to be released</param>
	private void ReleaseClient(Guid clientId)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServer)}.{nameof(ReleaseClient)}");
		if (!_clients.TryRemove(clientId, out var clientHandler))
			return;

		Metrics.UnregisterServerClientInstance();
		clientHandler.client.Dispose();
	}
}