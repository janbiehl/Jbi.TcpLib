using System.Linq;
using System.Text;
using Jbi.TcpLib.Handler;
using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests.Handler;

[TestSubject(typeof(DelimiterMessageHandler))]
public class DelimiterMessageHandlerTest
{
	[Fact]
	public void DelimiterMessageHandler_AppendBytes_ShouldAppendBytes()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");

		Assert.Equal(0, sut.Length);
		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);
		sut.AppendBytes([10, 20, 30, 40]);
		Assert.Equal(7, sut.Length);
	}

	[Fact]
	public void DelimiterMessageHandler_Reset_ShouldClearBuffer()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");

		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);

		sut.Reset();
		Assert.Equal(0, sut.Length);
	}
	
	[Fact]
	public void DelimiterMessageHandler_CheckForMessages_ShouldReturnNoMessage()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");
		
		sut.AppendBytes([]);

		var messages = sut.CheckForMessages();

		Assert.Empty(messages);
	}
	
	[Fact]
	public void DelimiterMessageHandler_CheckForMessages_ShouldReturnSingleMessage()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");
		
		sut.AppendBytes([10, 20, 30, 40, .. "\r\n"u8.ToArray()]);

		var messages = sut.CheckForMessages()
			.ToArray();

		Assert.Single(messages);
		Assert.Equal(messages[0].Memory.ToArray(), [10, 20, 30, 40]);
	}
	
	[Fact]
	public void DelimiterMessageHandler_CheckForMessages_ShouldReturnTwoMessages()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");
		
		sut.AppendBytes([10, 20, 30, 40, .. "\r\n"u8.ToArray(), 50, 60, 70, 80, .. "\r\n"u8.ToArray()]);

		var messages = sut.CheckForMessages()
			.ToArray();

		Assert.Equal(2, messages.Length);
		Assert.Equal(messages[0].Memory.ToArray(), [10, 20, 30, 40]);
		Assert.Equal(messages[1].Memory.ToArray(), [50, 60, 70, 80]);
	}
	
	[Fact]
	public void DelimiterMessageHandler_CheckForMessages_ShouldRetrunSingleMessageAndRemainPartialMessage()
	{
		DelimiterMessageHandler sut = new (1024, Encoding.UTF8, "\r\n");
		
		sut.AppendBytes([10, 20, 30, 40, .. "\r\n"u8.ToArray(), 50, 60, 70, 80]);

		var messages = sut.CheckForMessages()
			.ToArray();

		Assert.Single(messages);
		Assert.Equal(messages[0].Memory.ToArray(), [10, 20, 30, 40]);
		Assert.Equal(4, sut.Length);
	}
}