using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Jbi.TcpLib;

internal static class TelemetryUtils
{
	internal const string Name = "Jbi.Tcp";
	private static string? _version;
	internal static string Version =>
		_version ??= typeof(TelemetryUtils).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

internal static class Metrics
{
	private static Meter? _meter;
	private static UpDownCounter<int>? _clientInstancesCounter;
	private static UpDownCounter<int>? _serverInstancesCounter;
	private static UpDownCounter<int>? _serverClientInstancesCounter;
	private static Meter Meter => _meter ??= new Meter(TelemetryUtils.Name, TelemetryUtils.Version);

	private static UpDownCounter<int> ClientInstancesCounter =>
		_clientInstancesCounter ??= Meter.CreateUpDownCounter<int>("jbi_tcp_client_instances", "amount",
			"Total amount of currently active client instances");
	private static UpDownCounter<int> ServerInstancesCounter => _serverInstancesCounter ??=
		Meter.CreateUpDownCounter<int>("jbi_tcp_server_instances", "amount", 
			"Total amount of currently active server instances");

	private static UpDownCounter<int> ServerClientInstancesCounter => _serverClientInstancesCounter ??=
		Meter.CreateUpDownCounter<int>("jbi_tcp_server_connected_clients", "amount",
			"Total amount of clients that are connected to a server instance");

	public static void RegisterClientInstance() => ClientInstancesCounter.Add(1);
	public static void UnregisterClientInstance() => ClientInstancesCounter.Add(-1);
	public static void RegisterServerInstance() => ServerInstancesCounter.Add(1);
	public static void UnregisterServerInstance() => ServerInstancesCounter.Add(-1);
	public static void RegisterServerClientInstance() => ServerClientInstancesCounter.Add(1);
	public static void UnregisterServerClientInstance() => ServerClientInstancesCounter.Add(-1);
}

internal static class Telemetry
{
	private static ActivitySource? _activitySource;
	private static ActivitySource ActivitySource => _activitySource ??= new ActivitySource(TelemetryUtils.Name, TelemetryUtils.Version);

	internal static Activity? StartActivity(string name)
	{
		return ActivitySource.StartActivity(name);
	}
}