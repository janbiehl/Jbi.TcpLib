using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests;

[TestSubject(typeof(TcpClient))]
public class TcpClientTest
{
	private static IPEndPoint InternalEndpoint => new (IPAddress.Loopback, Random.Shared.Next(50000, 54000));
	
	[Fact]
	public async Task TcpClient_ConnectAsync_ShouldNotConnect()
	{
		using TcpClient client = new (InternalEndpoint);
		
		// Act
		var task = client.ConnectAsync();

		// Assert
		await Assert.ThrowsAsync<SocketException>(async () => await task);
	}
	
	[Fact]
	public async Task TcpClient_ConnectAsync_ShouldConnect()
	{
		var endpoint = InternalEndpoint;
		using TcpListener listener = new (endpoint);
		using TcpClient client = new (endpoint);
		listener.Start();
		
		// Act
		await client.ConnectAsync();

		// Assert
		Assert.True(true);
	}
	
	[Fact]
	public async Task TcpClient_ReadDataAsync_ShouldReadValidString()
	{
		var endpoint = InternalEndpoint;
		using TcpClient client = new (endpoint);

		_ = SimulateOneShotSendingServerAsync(endpoint, "Hello World!"u8.ToArray());

		await Task.Delay(100);
		
		await client.ConnectAsync();
		
		await foreach (var pooledMemory in client.ReadDataAsync())
		{
			try
			{
				var data = Encoding.UTF8.GetString(pooledMemory.Memory.Span);
				Assert.Equal("Hello World!", data);
			}
			finally
			{
				pooledMemory.Dispose();
			}
			
#pragma warning disable S1751
			break; // We want to break here after the first data chunk
#pragma warning restore S1751
		}
	}
	
	[Fact]
	public async Task TcpClient_ReadDataAsync_ShouldReadValidGuid()
	{
		var endpoint = InternalEndpoint;
		using TcpClient client = new (endpoint);
		var guid = Guid.NewGuid();
		
		_ = SimulateOneShotSendingServerAsync(endpoint, guid.ToByteArray());

		await Task.Delay(100);
		
		await client.ConnectAsync();
		
		await foreach (var pooledMemory in client.ReadDataAsync())
		{
			try
			{
				Assert.Equal(guid.ToByteArray(), pooledMemory.Memory);
			}
			finally
			{
				pooledMemory.Dispose();
			}
#pragma warning disable S1751
			break; // We want to break here after the first data chunk
#pragma warning restore S1751
		}
	}

	[Fact]
	public async Task TcpClient_ReadDataAsync_ShouldReadLargeData_DefaultBufferSize()
	{
		var endpoint = InternalEndpoint;
		using TcpClient client = new (endpoint);
		var bytes = new Memory<byte>(new byte[65536]);
		Random.Shared.NextBytes(bytes.Span);
		
		_ = SimulateOneShotSendingServerAsync(endpoint, bytes);

		await Task.Delay(100);
		
		await client.ConnectAsync();

		var i = 0;
		await foreach (var pooledMemory in client.ReadDataAsync())
		{
			try
			{
				Range range = new (i * 1024, i * 1024 + 1024);
				Assert.Equal(bytes.Span[range], pooledMemory.Span);
				i++;

				if (i == 10)
				{
					break;
				}
			}
			finally
			{
				pooledMemory.Dispose();
			}
		}
	}
	
	[Fact]
	public async Task TcpClient_ReadDataAsync_ShouldReadLargeData_CustomBufferSize()
	{
		var endpoint = InternalEndpoint;
		using TcpClient client = new (endpoint);
		var bytes = new Memory<byte>(new byte[65536]);
		Random.Shared.NextBytes(bytes.Span);
		
		_ = SimulateOneShotSendingServerAsync(endpoint, bytes);
		
		await client.ConnectAsync();

		var i = 0;
		await foreach (var pooledMemory in client.ReadDataAsync(512))
		{
			Range range = new (i * 512, i * 512 + 512);
			Assert.Equal(bytes.Span[range], pooledMemory.Span);
			i++;

			if (i == 10)
			{
				break;
			}
		}
	}

	[Fact]
	public async Task TcpClient_SendData_ShouldSendGuid()
	{
		var endpoint = InternalEndpoint;
		var guid = Guid.NewGuid();
		
		var serverTask = SimulateOneShotReceivingServerAsync(endpoint, memory =>
		{
			Assert.Equal(guid.ToByteArray(), memory);	
		});

		var clientTask = Task.Run(async () =>
		{
			using TcpClient client = new (endpoint);
			await client.ConnectAsync();
			await client.SendDataAsync(guid.ToByteArray());
		});

		await Task.WhenAll(serverTask, clientTask);
	}

	[Fact]
	public async Task TcpClient_SendData_ShouldSendLargeData()
	{
		var endpoint = InternalEndpoint;
		Memory<byte> buffer = new (new byte[32000]);
		Random.Shared.NextBytes(buffer.Span);
		
		var serverTask = SimulateOneShotReceivingServerAsync(endpoint, memory =>
		{
			Assert.Equal(buffer, memory);	
		});

		var clientTask = Task.Run(async () =>
		{
			using TcpClient client = new (endpoint);
			await client.ConnectAsync();
			await client.SendDataAsync(buffer);
		});

		await Task.WhenAll(serverTask, clientTask);
	}

	
	[Fact]
	public async Task TcpClient_SendData_ShouldSendString()
	{
		const string data = "Hello World!";
		var endpoint = InternalEndpoint;
		
		var serverTask = SimulateOneShotReceivingServerAsync(endpoint, memory =>
		{
			Assert.Equal(data, Encoding.UTF8.GetString(memory.Span));	
		});

		var clientTask = Task.Run(async () =>
		{
			using TcpClient client = new (endpoint);
			await client.ConnectAsync();
			await client.SendDataAsync(data);
		});

		await Task.WhenAll(serverTask, clientTask);
	}
	
	private static async Task SimulateOneShotSendingServerAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> data)
	{
		using TcpListener listener = new (endPoint);
		listener.Start();

		var acceptTcpClientAsync = await listener.AcceptTcpClientAsync();
		await using var stream = acceptTcpClientAsync.GetStream();
		await stream.WriteAsync(data);
	}

	private static async Task SimulateOneShotReceivingServerAsync(IPEndPoint endPoint, Action<ReadOnlyMemory<byte>> callback)
	{
		using TcpListener listener = new (endPoint);
		listener.Start();

		var acceptTcpClientAsync = await listener.AcceptTcpClientAsync();
		await using var stream = acceptTcpClientAsync.GetStream();
		Memory<byte> buffer = new byte[65536];

		var receivedBytes = await stream.ReadAsync(buffer);
		callback(buffer[..receivedBytes]);
	}
}