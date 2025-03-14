namespace Jbi.TcpLib.Serialization;

public interface IBinaryDeserializer<out T>
{
	T Deserialize(BinaryDeserializer deserializer);
}

public interface IBinarySerializer<in T>
{
	void Serialize(T value, BinarySerializer serializer);
}