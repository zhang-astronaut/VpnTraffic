using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace VpnTraffic;

[ComVisible(true)]
[Guid(VpnTrafficConstants.Clsid)]
[ClassInterface(ClassInterfaceType.None)]
public sealed partial class VpnTrafficExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposedEvent = new(false);
    private VpnTrafficCommandsProvider? _provider;

    public VpnTrafficExtension()
    {
        Diag.Log("VpnTrafficExtension ctor");
        _provider = new VpnTrafficCommandsProvider();
        Diag.Log("provider ready");
    }

    public object? GetProvider(ProviderType providerType)
    {
        Diag.Log($"GetProvider {providerType}");
        return providerType == ProviderType.Commands ? _provider : null;
    }

    public void Dispose()
    {
        Diag.Log("extension dispose");
        _provider?.Dispose();
        _provider = null;
        _disposedEvent.Set();
        GC.SuppressFinalize(this);
    }

    public void WaitForCompletion() => _disposedEvent.WaitOne();
}

public static class VpnTrafficConstants
{
    public const string Clsid = "a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17";
    public const string ExtensionId = "VpnTraffic";
}

internal static class Diag
{
    private static readonly object Gate = new();
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VpnTraffic",
        "diag.log");

    public static void Log(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // ignore
        }
    }
}

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        Diag.Log("Main args=" + string.Join(" ", args) + " cwd=" + Environment.CurrentDirectory + " base=" + AppContext.BaseDirectory);

        if (args.Length > 0 &&
            string.Equals(args[0], "-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ExtensionServer server = new();
                Diag.Log("ExtensionServer created");
                server.RegisterExtension(static () => new VpnTrafficExtension(), false);
                Diag.Log("RegisterExtension ok; entering Run");
                server.Run();
                Diag.Log("Run returned");
            }
            catch (Exception ex)
            {
                Diag.Log("FATAL " + ex);
            }

            // COM server must outlive Run if host has not yet connected / Run returns early.
            Diag.Log("keeping process alive for COM");
            new ManualResetEvent(false).WaitOne();
        }
        else
        {
            Diag.Log("missing -RegisterProcessAsComServer; exiting");
        }
    }
}
