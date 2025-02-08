# Jbi.Tcp

This library provides easy to use TcpClient and TcpServer. It reduces the hustle to manage the tcp stuff yourself.
Caution! As tcp data comes in as streams of data. We will not Buffer the data, as the user is responsible for that 
as we do not know how the messages will look like. So we are just passing over the raw memory chunks.

## Client samples

### Create a client

Create and connect a client

```c#
using System.Net;
using Jbi.TcpLib;

using CancellationTokenSource cts = new ();
using TcpClient client = new (new IPEndPoint(IPAddress.Loopback, 53467));

await client.ConnectAsync(cts.Token);

client.Disconnect();
```
### Read data

```c#
using System.Net;
using Jbi.TcpLib;

...

// Read data
// Await foreach over each received data chunk
await foreach (var pooledMemory in client.ReadDataAsync(1024, cts.Token))
{
	// We do return a PooledMemory<T> here. This is to indicate to you that you have to dispose the memory after use
	try
	{
		// do something with the data either as memory or as span. 
		// Both will contain the same data, the span is provided for a smaller code footprint
		//pooledMemory.Memory;
		//pooledMemory.Span;
	}
	finally
	{
		pooledMemory.Dispose();
	}
} 
```

### Write data

```c#
using System.Net;
using Jbi.TcpLib;

...

// Write as string
await client.SendDataAsync("Hello World!");

// Write as memory
var data = "Hello World!"u8.ToArray();
await client.SendDataAsync(data);
```

## Sever samples

### Create

```c#
using System.Net;
using Jbi.TcpLib;

IPEndPoint endPoint = new (IPAddress.Loopback, 0);
using TcpServerSimple server = new (endPoint);

await server.StartAsync();

await server.StopAsync();
```
### Read data

```c#
using System.Net;
using Jbi.TcpLib;

...

// Read data 
await foreach (var pooledMemory in server.ReadAllAsync())
{
	try
	{
		// Do something with that data
		//pooledMemory.Memory;
		//pooledMemory.Span;
	}
	finally
	{
		pooledMemory.Dispose();
	}
}
```

### Write data

```c#
using System.Net;
using Jbi.TcpLib;

...

// Write data
await server.SendDataAsync("Hallo Welt");

var data = "Hallo Welt!"u8.ToArray();
await server.SendDataAsync(data);
```