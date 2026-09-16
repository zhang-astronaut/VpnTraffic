using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace VpnTraffic;

[ComVisible(true)]
[Guid(VpnTrafficConstants.Clsid)]
[ClassInterface(ClassInterfaceType.None)]
public sealed partial class VpnTrafficExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposedEvent = new(false);
    private readonly VpnTrafficCommandsProvider _provider = new();

    public object? GetProvider(ProviderType providerType) =>
        providerType == ProviderType.Commands ? _provider : null;

    public void Dispose()
    {
        _provider.Dispose();
        _disposedEvent.Set();
        _disposedEvent.Dispose();
        GC.SuppressFinalize(this);
    }

    public void WaitForCompletion() => _disposedEvent.WaitOne();
}

public static class VpnTrafficConstants
{
    public const string Clsid = "a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17";
    public const string ExtensionId = "VpnTraffic";
}

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 &&
            string.Equals(args[0], "-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase))
        {
            ExtensionServer server = new();
            try
            {
                server.RegisterExtension(static () => new VpnTrafficExtension(), false);
                server.Run();
            }
            finally
            {
                server.Dispose();
            }
        }
    }
}
