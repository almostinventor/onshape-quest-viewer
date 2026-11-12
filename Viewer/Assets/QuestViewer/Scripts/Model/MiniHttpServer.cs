using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// Minimal HTTP/1.1 server on a raw TcpListener (works the same in the editor, Windows and Quest/IL2CPP).
/// One request per connection, Content-Length bodies only. Sends no CORS headers, so web pages can't use it;
/// the browser extension can (extensions with host permission aren't subject to CORS).
public sealed class MiniHttpServer : IDisposable
{
    public sealed class Request
    {
        public string Method;
        public string Path;
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Body = Array.Empty<byte>();
        public string Header(string name) => Headers.TryGetValue(name, out var v) ? v : null;
        public string BodyText => Encoding.UTF8.GetString(Body);
    }

    public sealed class Response
    {
        public int Status = 200;
        public string ContentType = "application/json; charset=utf-8";
        public byte[] Body = Array.Empty<byte>();
        public static Response Json(int status, string json) => new Response { Status = status, Body = Encoding.UTF8.GetBytes(json) };
    }

    const int MaxHeaderBytes = 32 * 1024;
    const int MaxBodyBytes = 200 * 1024 * 1024;
    const int MaxConnections = 8;
    const int ReadTimeoutMs = 20000;
    const int WriteTimeoutMs = 20000;

    readonly TcpListener listener;
    readonly Func<Request, Task<Response>> handler;
    readonly SemaphoreSlim slots = new SemaphoreSlim(MaxConnections, MaxConnections);
    volatile bool running;

    public MiniHttpServer(IPAddress address, int port, Func<Request, Task<Response>> handler)
    {
        listener = new TcpListener(address, port);
        this.handler = handler;
    }

    public void Start()
    {
        listener.Start();
        running = true;
        new Thread(AcceptLoop) { IsBackground = true, Name = "MiniHttpServer" }.Start();
    }

    void AcceptLoop()
    {
        while (running)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch (Exception) { if (!running) return; continue; }

            if (!slots.Wait(0))
            {
                client.Close(); // too many connections at once
                continue;
            }
            _ = Task.Run(async () =>
            {
                try { await Serve(client); }
                finally { slots.Release(); }
            });
        }
    }

    async Task Serve(TcpClient client)
    {
        using (client)
        {
            client.ReceiveTimeout = ReadTimeoutMs;
            client.SendTimeout = WriteTimeoutMs;
            var stream = client.GetStream();
            Response response;
            try
            {
                var request = ReadRequest(stream);
                if (request == null) return;
                response = await handler(request);
            }
            catch (InvalidDataException e)
            {
                response = Response.Json(400, "{\"ok\":false,\"message\":\"" + e.Message + "\"}");
            }
            catch (IOException)
            {
                return; // timed out or the client went away
            }
            catch (Exception)
            {
                response = Response.Json(500, "{\"ok\":false,\"message\":\"Internal error\"}");
            }
            try { WriteResponse(stream, response); } catch (Exception) { /* client went away */ }
        }
    }

    /// Blocking reads with the socket timeouts above (reliable on every platform).
    static Request ReadRequest(NetworkStream stream)
    {
        var head = new MemoryStream();
        var buf = new byte[8192];
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int n = stream.Read(buf, 0, buf.Length);
            if (n <= 0) return null;
            head.Write(buf, 0, n);
            if (head.Length > MaxHeaderBytes) throw new InvalidDataException("Headers too large");
            headerEnd = IndexOf(head.GetBuffer(), (int)head.Length, new byte[] { 13, 10, 13, 10 });
        }

        var raw = head.GetBuffer();
        var lines = Encoding.ASCII.GetString(raw, 0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None);
        var first = lines[0].Split(' ');
        if (first.Length < 2) throw new InvalidDataException("Bad request line");
        var req = new Request { Method = first[0].ToUpperInvariant(), Path = first[1].Split('?')[0] };
        for (int i = 1; i < lines.Length; i++)
        {
            int c = lines[i].IndexOf(':');
            if (c > 0) req.Headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
        }

        var lengthHeader = req.Header("Content-Length");
        int length = 0;
        if (lengthHeader != null && (!int.TryParse(lengthHeader, out length) || length < 0))
            throw new InvalidDataException("Bad Content-Length");
        if (length > MaxBodyBytes) throw new InvalidDataException("Body too large");

        var body = new byte[length];
        int already = Math.Min(length, (int)head.Length - (headerEnd + 4));
        Buffer.BlockCopy(raw, headerEnd + 4, body, 0, already);
        while (already < length)
        {
            int n = stream.Read(body, already, length - already);
            if (n <= 0) throw new IOException("Connection closed mid-body");
            already += n;
        }
        req.Body = body;
        return req;
    }

    static void WriteResponse(NetworkStream stream, Response r)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(r.Status).Append(' ').Append(Reason(r.Status)).Append("\r\n");
        sb.Append("Content-Type: ").Append(r.ContentType).Append("\r\n");
        sb.Append("Content-Length: ").Append(r.Body.Length).Append("\r\n");
        sb.Append("Cache-Control: no-store\r\n");
        sb.Append("X-Content-Type-Options: nosniff\r\n");
        sb.Append("Connection: close\r\n\r\n");
        var head = Encoding.ASCII.GetBytes(sb.ToString());
        stream.Write(head, 0, head.Length);
        if (r.Body.Length > 0) stream.Write(r.Body, 0, r.Body.Length);
        stream.Flush();
    }

    static string Reason(int status)
    {
        switch (status)
        {
            case 200: return "OK";
            case 400: return "Bad Request";
            case 401: return "Unauthorized";
            case 404: return "Not Found";
            case 405: return "Method Not Allowed";
            case 409: return "Conflict";
            default: return "Error";
        }
    }

    static int IndexOf(byte[] data, int length, byte[] pattern)
    {
        for (int i = 0; i <= length - pattern.Length; i++)
        {
            int j = 0;
            while (j < pattern.Length && data[i + j] == pattern[j]) j++;
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        running = false;
        try { listener.Stop(); } catch (Exception) { }
    }
}
