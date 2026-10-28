using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using DeadlockTimers.Core.Config;

namespace DeadlockTimers.App.Services;

public sealed record StateRow(string Label, string Remaining, bool Warning);

// Two DTO records in one file, same reasoning as Models/AlertsFile.cs.
public sealed record StateSnapshot(string Elapsed, bool Running, IReadOnlyList<StateRow> Upcoming);

public sealed class StateServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly object _lock = new();
    private readonly byte[] _html;
    private StateSnapshot _snapshot = new("0:00", false, Array.Empty<StateRow>());

    public StateServer(int port)
    {
        // localhost prefixes need no admin rights.
        _listener.Prefixes.Add($"http://localhost:{port}/");
        using var stream = EmbeddedFiles.Open("Web.index.html");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        _html = buffer.ToArray();
    }

    public void UpdateSnapshot(StateSnapshot snapshot)
    {
        lock (_lock) _snapshot = snapshot;
    }

    /// Starts the background request loop. Throws if the port is not usable.
    public void Start()
    {
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception) { break; }   // listener stopped

            try
            {
                var path = context.Request.Url?.AbsolutePath ?? "/";
                byte[] body;
                string contentType;

                if (path == "/state")
                {
                    StateSnapshot snapshot;
                    lock (_lock) snapshot = _snapshot;
                    body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, ConfigLoader.Options));
                    contentType = "application/json";
                }
                else
                {
                    body = _html;
                    contentType = "text/html";
                }

                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
            catch (Exception) { /* the client disconnected mid-response */ }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
    }
}
