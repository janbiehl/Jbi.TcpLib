using System.Net;
using System.Runtime.CompilerServices;

namespace Jbi.TcpLib;

/// <summary>
/// Represents a simple TCP server. It is able to accept one client at a time, and read/write data from/to it.
/// It has no interpreting logic, so it is up to the user to interpret the raw data that was received,
/// also the user has to convert the data into raw data before sending it. 
/// </summary>
/// <param name="endPoint">Endpoint to listen to</param>
public sealed class TcpServerSimple(IPEndPoint endPoint) 
	: IDisposable
{
	private readonly TcpServer _tcpServer = new (endPoint, 1);

	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServerSimple)}.{nameof(StartAsync)}");
		return _tcpServer.StartAsync(cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServerSimple)}.{nameof(StopAsync)}");
		return _tcpServer.StopAsync(cancellationToken);
	}
	
	public async IAsyncEnumerable<PooledMemory<byte>> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServerSimple)}.{nameof(ReadAllAsync)}");
		await foreach (var (_, data) in _tcpServer.ReadDataAsync(cancellationToken))
		{
			yield return data;
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
		using var activity = Telemetry.StartActivity($"{nameof(TcpServerSimple)}.{nameof(SendDataAsync)}");
		var clientId = _tcpServer.ClientIds.FirstOrDefault(Guid.Empty);
		return _tcpServer.SendDataAsync(clientId, data, cancellationToken);
	}

	/// <summary>
	/// Send a string to the remote partner
	/// </summary>
	/// <param name="data">String to send to the remote part</param>
	/// <param name="cancellationToken">Cancel long-running operations</param>
	public ValueTask SendDataAsync(string data, CancellationToken cancellationToken = default)
	{
		using var activity = Telemetry.StartActivity($"{nameof(TcpServerSimple)}.{nameof(SendDataAsync)}");
		var clientId = _tcpServer.ClientIds.FirstOrDefault(Guid.Empty);
		return _tcpServer.SendDataAsync(clientId, data, cancellationToken);
	}
	
	/// <inheritdoc />
	public void Dispose()
	{
		_tcpServer.Dispose();
	}
}