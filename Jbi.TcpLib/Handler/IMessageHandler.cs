namespace Jbi.TcpLib.Handler;

public interface IMessageHandler
{
	int Length { get; }
	void AppendBytes(ReadOnlySpan<byte> bytes);
	IReadOnlyCollection<PooledMemory<byte>> CheckForMessages();
	void Reset();
}