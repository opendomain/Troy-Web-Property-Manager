using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>
    /// Stands in for the health check the app pings on startup (Telemetry:StartupPingUrl): a one-shot HTTP endpoint on
    /// a free loopback port that answers 200 and keeps the request, so a test can check the ping without it ever leaving
    /// the machine.
    /// </summary>
    public sealed class StartupPingCatcher
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly TaskCompletionSource<string> _request = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StartupPingCatcher()
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/ping";
            _ = CatchOneAsync();
        }

        public string Url { get; }

        /// <summary>The ping's request line, headers and body, once it arrives.</summary>
        public Task<string> Request => _request.Task;

        /// <summary>A loopback URL with nothing listening, so the ping fails straight away.</summary>
        public static string UnreachableUrl()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return $"http://127.0.0.1:{port}/ping";
        }

        private async Task CatchOneAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                var received = new List<byte>();
                var buffer = new byte[4096];
                // Read the headers, then as much body as Content-Length says.
                while (true)
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0) break;
                    received.AddRange(buffer.AsSpan(0, read).ToArray());
                    var text = Encoding.UTF8.GetString(received.ToArray());
                    var headersEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headersEnd < 0) continue;
                    var length = text[..headersEnd].Split("\r\n")
                        .Where(h => h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        .Select(h => int.Parse(h["Content-Length:".Length..].Trim()))
                        .FirstOrDefault();
                    if (received.Count >= Encoding.UTF8.GetByteCount(text[..headersEnd]) + 4 + length) break;
                }
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray());
                _request.TrySetResult(Encoding.UTF8.GetString(received.ToArray()));
            }
            catch (Exception ex)
            {
                _request.TrySetException(ex);
            }
            finally
            {
                _listener.Stop();
            }
        }
    }
}
