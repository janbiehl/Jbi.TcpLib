using JetBrains.Annotations;
using Xunit;

namespace Jbi.TcpLib.Tests;

[TestSubject(typeof(Buffer))]
public class BufferTest
{

	[Fact]
	public void Buffer_Size_ShouldFit()
	{
		Buffer buffer = new (1024);
		Assert.Equal(1024, buffer.Size);
		Assert.Equal(0, buffer.Position);
	}

	[Fact]
	public void Buffer_Advance_ShouldAdvancePosition()
	{
		Buffer buffer = new (1024);
		buffer.Write("Hello World!"u8);
		Assert.Equal(12 ,buffer.Position);
	}

	[Fact]
	public void Buffer_RemoveFromLeft_ShouldRemove()
	{
		Buffer buffer = new (1024);
		buffer.Write("Hello World!"u8);

		using var data = buffer.Read(12);

		Assert.Equal("Hello World!"u8, data.Span);
		Assert.Equal(0, buffer.Position);
	}

	[Fact]
	public void Buffer_Read_ShouldShiftData()
	{
		Buffer buffer = new (1024);
		buffer.Write("Hello World!"u8);

		using var data = buffer.Read(5);
		
		Assert.Equal("Hello"u8, data.Span);
		Assert.Equal(" World!"u8, buffer.Memory[..buffer.Position].Span);
	}
}