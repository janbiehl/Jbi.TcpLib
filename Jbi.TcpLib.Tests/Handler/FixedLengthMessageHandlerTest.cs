using Jbi.TcpLib.Handler;
using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests.Handler;

[TestSubject(typeof(FixedLengthMessageHandler))]
public class FixedLengthMessageHandlerTest
{
	[Fact]
	public void DelimiterMessageHandler_AppendBytes_ShouldAppendBytes()
	{
		FixedLengthMessageHandler sut = new (1024, 2);

		Assert.Equal(0, sut.Length);
		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);
		sut.AppendBytes([10, 20, 30, 40]);
		Assert.Equal(7, sut.Length);
	}

	[Fact]
	public void FixedLengthMessageHandler_Reset_ShouldClearBuffer()
	{
		FixedLengthMessageHandler sut = new (1024, 2);

		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);

		sut.Reset();
		Assert.Equal(0, sut.Length);
	}

	
	[Fact]
	public void FixedLengthMessageHandler_ContainsNoMessage_ShouldExtractSingleMessage()
	{
		FixedLengthMessageHandler sut = new (1024, 2);
		
		sut.AppendBytes([]);

		var messages = sut.CheckForMessages();

		Assert.Empty(messages);
	}
	
	[Fact]
	public void FixedLengthMessageHandler_ContainsPartialMessage_ShouldExtractSingleMessage()
	{
		FixedLengthMessageHandler sut = new (1024, 2);
		
		sut.AppendBytes([10]);

		var messages = sut.CheckForMessages();

		Assert.Empty(messages);
	}
	
	[Fact]
	public void FixedLengthMessageHandler_ContainsSingleMessage_ShouldExtractSingleMessage()
	{
		FixedLengthMessageHandler sut = new (1024, 2);
		
		sut.AppendBytes([10, 20]);

		var messages = sut.CheckForMessages();

		Assert.Single(messages);
	}

	[Fact]
	public void FixedLengthMessageHandler_ContainsOneAndAHalfMessages_ShouldExtractSingleMessage()
	{
		FixedLengthMessageHandler sut = new (1024, 2);
		
		sut.AppendBytes([10, 20, 30]);

		var messages = sut.CheckForMessages();

		Assert.Single(messages);
	}

	
	[Fact]
	public void FixedLengthMessageHandler_ContainsTwoMessage_ShouldExtractSingleMessage()
	{
		FixedLengthMessageHandler sut = new (1024, 2);
		
		sut.AppendBytes([10, 20, 30, 40]);

		var messages = sut.CheckForMessages();

		Assert.Equal(2, messages.Count);
	}

}