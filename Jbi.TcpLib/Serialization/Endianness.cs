namespace Jbi.TcpLib.Serialization;

/// <summary>
/// Defines the byte ordering for number types.
/// </summary>
public enum Endianness
{
	/// <summary>
	/// Least significant byte first.
	/// </summary>
	LittleEndian,

	/// <summary>
	/// Most significant byte first.
	/// </summary>
	BigEndian
}