using System.Globalization;
using VpnTraffic.Services;

// Smoke-test pure service logic without WinRT host.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

var failures = 0;

void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
    if (!ok)
    {
        failures++;
    }
}

// Parse subscription-userinfo
var snap = SubscriptionClient.ParseUserInfo(
    "upload=1048576; download=3145728; total=10737418240; expire=1893456000",
    DateTimeOffset.UnixEpoch.AddYears(50),
    "My Airport");
Check("parse used", snap.UsedBytes == 1048576 + 3145728, snap.UsedBytes.ToString());
Check("parse total", snap.TotalBytes == 10737418240L);
Check("parse percent", Math.Abs(snap.Percent!.Value - 0.039) < 0.001, snap.Percent.Value.ToString("0.###"));
Check("parse expire", snap.ExpireUnix == 1893456000);
Check("parse success", snap.IsSuccess);

var bad = SubscriptionClient.ParseUserInfo("hello", DateTimeOffset.Now);
Check("parse bad", !bad.IsSuccess && bad.Error is not null);

var empty = SubscriptionClient.ParseUserInfo("", DateTimeOffset.Now);
Check("parse empty", !empty.IsSuccess);

// History sparse persist
var dir = Path.Combine(Path.GetTempPath(), "vpntraffic-test-" + Guid.NewGuid().ToString("N"));
var path = Path.Combine(dir, "history.json");
var history = new HistoryStore();
history.Configure(path, persistEnabled: true, maxPoints: 10);
var s1 = SubscriptionClient.ParseUserInfo("upload=0; download=0; total=1000; expire=0", DateTimeOffset.Now);
history.Add(s1);
Check("no write on first tiny sample within interval only if last empty", File.Exists(path));
var s2 = SubscriptionClient.ParseUserInfo("upload=1; download=1; total=1000; expire=0", DateTimeOffset.Now.AddMinutes(1));
history.Add(s2);
Check("in-memory points", history.Snapshot().Count >= 1);

// Force persist via delta
var s3 = SubscriptionClient.ParseUserInfo(
    $"upload={70L * 1024 * 1024}; download=0; total=1000000000; expire=0",
    DateTimeOffset.Now.AddMinutes(2));
history.Add(s3);
Check("persist after delta", File.Exists(path), path);

var history2 = new HistoryStore();
history2.Configure(path, persistEnabled: true, maxPoints: 10);
history2.Load();
Check("reload points", history2.Snapshot().Count >= 1, history2.Snapshot().Count.ToString());

// No persist when disabled
var dir2 = Path.Combine(Path.GetTempPath(), "vpntraffic-test2-" + Guid.NewGuid().ToString("N"));
var path2 = Path.Combine(dir2, "history.json");
var history3 = new HistoryStore();
history3.Configure(path2, persistEnabled: false, maxPoints: 10);
history3.Add(SubscriptionClient.ParseUserInfo("upload=100; download=100; total=1000; expire=0", DateTimeOffset.Now.AddDays(1)));
Check("disabled persist skips disk", !File.Exists(path2));

// Chart
var chart = ChartRenderer.Render(
[
    new HistoryPoint(DateTimeOffset.Now.AddHours(-1), 100, 1000),
    new HistoryPoint(DateTimeOffset.Now, 400, 1000),
]);
Check("chart has block chars", chart.Contains('█') && chart.Contains('%'), chart);

Console.WriteLine(failures == 0 ? "ALL PASS" : $"FAILURES={failures}");
return failures == 0 ? 0 : 1;
