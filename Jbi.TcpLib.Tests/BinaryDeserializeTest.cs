using Jbi.TcpLib.Serialization;
using Xunit;

namespace Jbi.TcpLib.Tests;

public class BinaryDeserializerTests
{
    [Fact]
    public void Skip_ShouldReturnCorrectValue_BigEndian()
    {
        byte[] data = [0x00, 0x12, 0x34];
        var deserializer = new BinaryDeserializer(data, Endianness.BigEndian);
        
        deserializer.Skip(1);
        var result = deserializer.ReadShort();
            
        Assert.Equal(0x1234, result);
    }
    
    [Fact]
    public void Skip_ShouldReturnCorrectValue_LittleEndiang()
    {
        byte[] data = [0x00, 0x12, 0x34];
        var deserializer = new BinaryDeserializer(data, Endianness.LittleEndian);
        
        deserializer.Skip(1);
        var result = deserializer.ReadShort();
            
        Assert.Equal(0x3412, result);
    }
    
    [Fact]
    public void ReadShort_ShouldReturnCorrectValue_BigEndian()
    {
        byte[] data = [0x12, 0x34];
        var deserializer = new BinaryDeserializer(data, Endianness.BigEndian);
            
        var result = deserializer.ReadShort();
            
        Assert.Equal(0x1234, result);
    }
    
    [Fact]
    public void ReadShort_ShouldReturnCorrectValue_LittleEndian()
    {
        byte[] data = [0x12, 0x34];
        var deserializer = new BinaryDeserializer(data, Endianness.LittleEndian);
            
        var result = deserializer.ReadShort();
            
        Assert.Equal(0x3412, result);
    }
    
    /*
    [Fact]
    public void Skip_ShouldMovePositionForward()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];
        var deserializer = new BinaryDeserializer(data, Endianness.BigEndian);
            
        deserializer.Skip(2);
            
        int result = deserializer.ReadByte();
            
        Assert.Equal(0x03, result);
    }
    */

    // [Fact]
    // public void Skip_ShouldThrowException_WhenSkippingTooMuch()
    // {
    //     byte[] data = [0x01, 0x02];
    //     var deserializer = new BinaryDeserializer(data, Endianness.BigEndian);
    //         
    //     Assert.Throws<InvalidOperationException>(() => deserializer.Skip(5));
    // }
}