using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests;

[TestSubject(typeof(TcpServer))]
public class TcpServerTest
{
	private static IPEndPoint Endpoint => new (IPAddress.Loopback, Random.Shared.Next(54000, 54999));
	
	[Fact]
	public async Task TcpServerAdvanced_ConnectionHandler_ShouldConnectSingleClient()
	{
		var endpoint = Endpoint;
		await using TcpServer server = new (endpoint);
		using System.Net.Sockets.TcpClient client = new ();
		
		await server.StartAsync();
		await client.ConnectAsync(endpoint);

		await Task.Delay(10); // This seems necessary as the server handler needs some time
		
		Assert.True(client.Connected);
		Assert.Single(server.ClientIds);
	}
	
	[Fact]
	public async Task TcpServerAdvanced_ConnectionHandler_ShouldConnectTenClients()
	{
		var endpoint = Endpoint;
		await using TcpServer server = new (endpoint);
		using System.Net.Sockets.TcpClient client1 = new ();
		using System.Net.Sockets.TcpClient client2 = new ();
		using System.Net.Sockets.TcpClient client3 = new ();
		using System.Net.Sockets.TcpClient client4 = new ();
		using System.Net.Sockets.TcpClient client5 = new ();
		using System.Net.Sockets.TcpClient client6 = new ();
		using System.Net.Sockets.TcpClient client7 = new ();
		using System.Net.Sockets.TcpClient client8 = new ();
		using System.Net.Sockets.TcpClient client9 = new ();
		using System.Net.Sockets.TcpClient client10 = new ();
		
		await server.StartAsync();
		await client1.ConnectAsync(endpoint);
		await client2.ConnectAsync(endpoint);
		await client3.ConnectAsync(endpoint);
		await client4.ConnectAsync(endpoint);
		await client5.ConnectAsync(endpoint);
		await client6.ConnectAsync(endpoint);
		await client7.ConnectAsync(endpoint);
		await client8.ConnectAsync(endpoint);
		await client9.ConnectAsync(endpoint);
		await client10.ConnectAsync(endpoint);

		await Task.Delay(10); // This seems necessary as the server handler needs some time
		
		Assert.True(client1.Connected);
		Assert.True(client2.Connected);
		Assert.True(client3.Connected);
		Assert.True(client4.Connected);
		Assert.True(client5.Connected);
		Assert.True(client6.Connected);
		Assert.True(client7.Connected);
		Assert.True(client8.Connected);
		Assert.True(client9.Connected);
		Assert.True(client10.Connected);
		Assert.Equal(10, server.ClientIds.Count);
	}
	
	[Fact]
	public async Task TcpServerAdvanced_ConnectionHandler_ShouldConnectRevokeLastClient()
	{
		var endpoint = Endpoint;
		await using TcpServer server = new (endpoint);
		using System.Net.Sockets.TcpClient client1 = new ();
		using System.Net.Sockets.TcpClient client2 = new ();
		using System.Net.Sockets.TcpClient client3 = new ();
		using System.Net.Sockets.TcpClient client4 = new ();
		using System.Net.Sockets.TcpClient client5 = new ();
		using System.Net.Sockets.TcpClient client6 = new ();
		using System.Net.Sockets.TcpClient client7 = new ();
		using System.Net.Sockets.TcpClient client8 = new ();
		using System.Net.Sockets.TcpClient client9 = new ();
		using System.Net.Sockets.TcpClient client10 = new ();
		using System.Net.Sockets.TcpClient client11 = new ();
		
		await server.StartAsync();
		await client1.ConnectAsync(endpoint);
		await client2.ConnectAsync(endpoint);
		await client3.ConnectAsync(endpoint);
		await client4.ConnectAsync(endpoint);
		await client5.ConnectAsync(endpoint);
		await client6.ConnectAsync(endpoint);
		await client7.ConnectAsync(endpoint);
		await client8.ConnectAsync(endpoint);
		await client9.ConnectAsync(endpoint);
		await client10.ConnectAsync(endpoint);
		await client11.ConnectAsync(endpoint);

		await Task.Delay(10); // This seems necessary as the server handler needs some time
		
		// Eleven clients try to connect, but the servers default concurrent client is 10
		// So we expect to have only 10 connected Clients
		Assert.Equal(10, server.ClientIds.Count);
	}

	[Fact]
	public async Task TcpServerAdvanced_ReadDataAsync_ShouldReceiveSingleChunkFromClient()
	{
		var endpoint = Endpoint;
		Memory<byte> buffer = new byte[1024];
		Random.Shared.NextBytes(buffer.Span);
		await using TcpServer server = new (endpoint);
		await server.StartAsync();

		await SimulateOneShotSendingClient(endpoint, buffer);

		await foreach (var (clientId, data) in server.ReadDataAsync())
		{
			Assert.NotEqual(Guid.Empty, clientId);
			Assert.Equal(buffer, data);
			break; // we expect only one chunk of data
		}
	}
	
	[Fact]
	public async Task TcpServerAdvanced_ReadDataAsync_ShouldReceiveMultiChunksFromClient()
	{
		var endpoint = Endpoint;
		Memory<byte> buffer = new byte[4096];
		Random.Shared.NextBytes(buffer.Span);
		await using TcpServer server = new (endpoint);
		await server.StartAsync();

		await SimulateOneShotSendingClient(endpoint, buffer);

		var i = 0;
		await foreach (var (clientId, chunk) in server.ReadDataAsync())
		{
			Range range = new (i * 1024, i * 1024 + 1024);
			Assert.NotEqual(Guid.Empty, clientId);
			Assert.Equal(buffer[range], chunk);
			i++;

			if (i == 4)
			{
				break;
			}
		}
	}

	[Fact]
	public async Task TcpServerAdvanced_SendDataAsync_ShouldSendSingleChunk()
	{
		var endpoint = Endpoint;
		
		Memory<byte> buffer = new byte[4096];
		Random.Shared.NextBytes(buffer.Span);
		
		TcpServer server = new (endpoint);
		
		try
		{
			await server.StartAsync();
			
			var clientTask =
				SimulateOneShotReceivingClientAsync(endpoint, receivedData =>
				{
					Assert.Equal(receivedData, buffer);
				});

			await Task.Delay(10);
			
			var task = Task.Run(() => server.SendDataAsync(server.ClientIds.First(), buffer));
			await Task.WhenAll(clientTask, task);
		}
		finally
		{
			await server.DisposeAsync();
		}
	}

	[Fact]
	public async Task TcpServer_ClientIsRegisteredInternal()
	{
		var endpoint = Endpoint;
		
		await using TcpServer server = new (endpoint);
		await server.StartAsync();
		
		using System.Net.Sockets.TcpClient client = new ();

		await client.ConnectAsync(endpoint);

		await Task.Delay(5);
		
		// Here we want to make sure that the client will be noticed by the server
		Assert.Single(server.ClientIds);
	}
	
	private static async Task SimulateOneShotSendingClient(IPEndPoint endPoint, ReadOnlyMemory<byte> data)
	{
		using System.Net.Sockets.TcpClient client = new ();
		await client.ConnectAsync(endPoint);

		await using var stream = client.GetStream();
		await stream.WriteAsync(data);
	}
	
	private static async Task SimulateOneShotReceivingClientAsync(IPEndPoint endPoint, Action<ReadOnlyMemory<byte>> callback)
	{
		using System.Net.Sockets.TcpClient tcpClient = new ();
		await tcpClient.ConnectAsync(endPoint);
		
		await using var stream = tcpClient.GetStream();
		Memory<byte> buffer = new byte[65536];

		var receivedBytes = await stream.ReadAsync(buffer);
		callback(buffer[..receivedBytes]);
	}

}