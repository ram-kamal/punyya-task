using System.Runtime.InteropServices;
using PHF.Client.Core.Telemetry;

namespace PHF.Client.Desktop.Telemetry;

public static class TelemetryEngineFactory
{
    public static ITelemetryEngine Create(int idleThresholdSeconds = 300)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PHF.Client.Telemetry.WorkstationTelemetryEngine(idleThresholdSeconds);
        }
        else
        {
            return new MacOsTelemetryEngine(idleThresholdSeconds);
        }
    }
}
