using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VpnTraffic;

internal sealed class VpnJsonSettingsManager : JsonSettingsManager
{
    public VpnJsonSettingsManager(string filePath)
    {
        FilePath = filePath;
    }
}
