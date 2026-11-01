using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;

const int Port = 5000;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(Port);
    o.Limits.MaxRequestBodySize = null;      // no size limit
    o.Limits.MinRequestBodyDataRate = null;  // slow phones are fine
});
var app = builder.Build();

var dir = Path.Combine(Directory.GetCurrentDirectory(), "Shared");
Directory.CreateDirectory(dir);
foreach (var p in Directory.GetFiles(dir, "*.part")) File.Delete(p);

var active = new ConcurrentDictionary<string, Upload>();
var gate = new object();

app.UseDefaultFiles();
app.UseStaticFiles();

static string Unique(string folder, string name)
{
    var path = Path.Combine(folder, name);
    if (!File.Exists(path)) return path;
    var stem = Path.GetFileNameWithoutExtension(name);
    var ext = Path.GetExtension(name);
    for (var i = 1; ; i++)
    {
        path = Path.Combine(folder, $"{stem} ({i}){ext}");
        if (!File.Exists(path)) return path;
    }
}

List<string> Urls() => NetworkInterface.GetAllNetworkInterfaces()
    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254"))
    .Select(a => $"http://{a.Address}:{Port}")
    .Distinct()
    .ToList();

app.MapGet("/api/info", () => Results.Json(Urls()));

app.MapGet("/api/files", () =>
{
    var files = new DirectoryInfo(dir).GetFiles()
        .Where(f => !f.Name.EndsWith(".part"))
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new { name = f.Name, size = f.Length })
        .ToList();
    var uploading = active.Values
        .Select(u => new { id = u.Id, name = u.Name, size = u.Size, received = Interlocked.Read(ref u.Received) })
        .ToList();
    return Results.Json(new { files, uploading });
});

// Raw streaming upload: the browser sends the file as the request body, so no multipart parsing and no memory copy.
app.MapPut("/api/upload", async Task<IResult> (HttpRequest req, string name, long? size, CancellationToken ct) =>
{
    var safe = Path.GetFileName(name ?? "");
    if (string.IsNullOrWhiteSpace(safe)) return Results.BadRequest("name required");
    var id = Guid.NewGuid().ToString("N");
    var tmp = Path.Combine(dir, id + ".part");
    var up = new Upload(id, safe, size ?? 0);
    active[id] = up;
    try
    {
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None,
                         1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var buf = new byte[1 << 20];
            int n;
            while ((n = await req.Body.ReadAsync(buf.AsMemory(), ct)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, n), ct);
                Interlocked.Add(ref up.Received, n);
            }
        }
        lock (gate) { File.Move(tmp, Unique(dir, safe)); }
        return Results.Ok();
    }
    catch (Exception)
    {
        try { File.Delete(tmp); } catch { }   // cancelled or connection lost: drop the partial file
        return Results.StatusCode(499);
    }
    finally { active.TryRemove(id, out _); }
});

app.MapGet("/api/download/{name}", (string name) =>
{
    var safe = Path.GetFileName(name);
    var path = Path.Combine(dir, safe);
    if (safe.EndsWith(".part") || !File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/octet-stream", safe, enableRangeProcessing: true);
});

app.MapDelete("/api/files/{name}", (string name) =>
{
    var path = Path.Combine(dir, Path.GetFileName(name));
    if (File.Exists(path)) File.Delete(path);
    return Results.Ok();
});

Console.WriteLine("AllWebHub Share - PC Hub");
Console.WriteLine("Shared folder: " + dir);
Console.WriteLine("Open one of these on your phone (same Wi-Fi):");
foreach (var u in Urls()) Console.WriteLine("  " + u);
app.Run();

class Upload
{
    public Upload(string id, string name, long size) { Id = id; Name = name; Size = size; }
    public string Id;
    public string Name;
    public long Size;
    public long Received;
}
