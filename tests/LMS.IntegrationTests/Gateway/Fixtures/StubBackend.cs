using System.Net;
using System.Text;

namespace LMS.IntegrationTests.Gateway.Fixtures;

/// <summary>
/// A minimal in-process HTTP server (HttpListener) that acts as a stub downstream
/// service. It accepts one request, records the headers, responds 200, then stops.
///
/// Usage pattern:
/// <code>
///   using var stub = new StubBackend();
///   factory.WithStubBackend(stub.Port);
///   _ = client.GetAsync("/api/courses/test");
///   var headers = await stub.WaitForRequestAsync(TimeSpan.FromSeconds(5));
/// </code>
/// </summary>
public sealed class StubBackend : IDisposable
{
    private readonly HttpListener _listener;
    private readonly TaskCompletionSource<IReadOnlyDictionary<string, string>?> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cts = new();

    public int Port { get; }

    public StubBackend()
    {
        // Pick a random free port on loopback
        Port = FindFreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = ListenAsync(_cts.Token);
    }

    /// <summary>
    /// Waits until the stub backend receives one HTTP request and returns a
    /// dictionary of its headers (header names are title-cased as received).
    /// Returns <c>null</c> if the timeout expires before a request arrives.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>?> WaitForRequestAsync(
        TimeSpan timeout)
    {
        using var delayCts = new CancellationTokenSource(timeout);
        try
        {
            var completedTask = await Task.WhenAny(_tcs.Task, Task.Delay(timeout, delayCts.Token));
            return completedTask == _tcs.Task ? await _tcs.Task : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().WaitAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }

                // Capture headers
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string? key in ctx.Request.Headers.Keys)
                {
                    if (key is not null)
                        headers[key] = ctx.Request.Headers[key] ?? string.Empty;
                }

                // Respond 200 so the proxy closes the connection cleanly
                ctx.Response.StatusCode = 200;
                var body = Encoding.UTF8.GetBytes("stub ok");
                await ctx.Response.OutputStream.WriteAsync(body, ct);
                ctx.Response.Close();

                _tcs.TrySetResult(headers);
                // Only capture the first request
                break;
            }
        }
        catch
        {
            _tcs.TrySetResult(null);
        }
    }

    private static int FindFreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* best-effort */ }
        _cts.Dispose();
    }
}
