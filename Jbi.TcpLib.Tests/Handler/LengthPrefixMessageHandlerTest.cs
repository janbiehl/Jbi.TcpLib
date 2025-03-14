using Jbi.TcpLib.Handler;
using Jbi.TcpLib.Serialization;
using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests.Handler;

[TestSubject(typeof(LengthPrefixMessageHandler))]
public class LengthPrefixMessageHandlerTest
{
	[Fact]
	public void LengthPrefixMessageHandler_AppendBytes_ShouldAppendBytes()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Int, Endianness.BigEndian);

		Assert.Equal(0, sut.Length);
		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);
		sut.AppendBytes([10, 20, 30, 40]);
		Assert.Equal(7, sut.Length);
	}

	[Fact]
	public void LengthPrefixMessageHandler_Reset_ShouldClearBuffer()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Int, Endianness.BigEndian);

		sut.AppendBytes([10, 20, 30]);
		Assert.Equal(3, sut.Length);

		sut.Reset();
		Assert.Equal(0, sut.Length);
	}
	
	[Fact]
	public void LengthPrefixMessageHandler_CheckForMessages_ShouldNotExtractEmptyMessage()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Short, Endianness.BigEndian);
		
		sut.AppendBytes([]);

		var messages = sut.CheckForMessages();

		Assert.Empty(messages);
	}
	
	[Fact]
	public void LengthPrefixMessageHandler_CheckForMessages_ShouldNotExtractPartialMessage()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Short, Endianness.BigEndian);
		
		sut.AppendBytes([0x00, 0x02, 10]);

		var messages = sut.CheckForMessages();

		Assert.Empty(messages);
	}
	
	[Fact]
	public void LengthPrefixMessageHandler_CheckForMessages_ShouldExtractSingleMessage()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Short, Endianness.BigEndian);
	
		sut.AppendBytes([0x00, 0x02, 10, 20]);

		var messages = sut.CheckForMessages();

		Assert.Single(messages);
	}

	
	[Fact]
	public void LengthPrefixMessageHandler_CheckForMessages_ShouldExtractSingleMessageAndRemainPartialMessage()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Short, Endianness.BigEndian);
		
		sut.AppendBytes([0x00, 0x02, 10, 20, 0x00, 0x02, 30]);

		var messages = sut.CheckForMessages();

		Assert.Single(messages);
	}

	[Fact]
	public void LengthPrefixMessageHandler_ContainsTwoMessages_ShouldExtractMultipleMessages()
	{
		LengthPrefixMessageHandler sut = new (1024, LengthPrefixMessageHandler.PrefixType.Short, Endianness.BigEndian);
		
		sut.AppendBytes([0x00, 0x02, 10, 20, 0x00, 0x02, 30, 40]);

		var messages = sut.CheckForMessages();

		Assert.Equal(2, messages.Count);
	}
}