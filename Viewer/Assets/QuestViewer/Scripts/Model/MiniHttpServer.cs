using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// Minimal HTTP/1.1 server on a raw TcpListener (works the same in the editor, Windows and Quest/IL2CPP).
/// One request per connection, Content-Length bodies only.
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

    const int MaxHeaderBytes = 64 * 1024;
    const int MaxBodyBytes = 512 * 1024 * 1024;

    readonly TcpListener listener;
    readonly Func<Request, Task<Response>> handler;
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
            _ = Task.Run(() => Serve(client));
        }
    }

    async Task Serve(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            Response response;
            try
            {
                var request = await ReadRequest(stream);
                if (request == null) return;
                response = request.Method == "OPTIONS" ? new Response { Status = 204 } : await handler(request);
            }
            catch (Exception e)
            {
                response = Response.Json(500, "{\"ok\":false,\"message\":\"" + e.Message.Replace("\"", "'") + "\"}");
            }
            try { await WriteResponse(stream, response); } catch (Exception) { /* client went away */ }
        }
    }

    static async Task<Request> ReadRequest(NetworkStream stream)
    {
        // Read until the end of the headers.
        var head = new MemoryStream();
        var buf = new byte[8192];
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int n = await stream.ReadAsync(buf, 0, buf.Length);
            if (n <= 0) return null;
            head.Write(buf, 0, n);
            if (head.Length > MaxHeaderBytes) throw new InvalidDataException("Headers too large");
            headerEnd = IndexOf(head.GetBuffer(), (int)head.Length, new byte[] { 13, 10, 13, 10 });
        }

        var raw = head.GetBuffer();
        var lines = Encoding.ASCII.GetString(raw, 0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None);
        var first = lines[0].Split(' ');
        var req = new Request { Method = first[0].ToUpperInvariant(), Path = first.Length > 1 ? first[1].Split('?')[0] : "/" };
        for (int i = 1; i < lines.Length; i++)
        {
            int c = lines[i].IndexOf(':');
            if (c > 0) req.Headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
        }

        int length = int.TryParse(req.Header("Content-Length"), out var l) ? l : 0;
        if (length < 0 || length > MaxBodyBytes) throw new InvalidDataException("Body too large");
        var body = new byte[length];
        int already = Math.Min(length, (int)head.Length - (headerEnd + 4));
        Buffer.BlockCopy(raw, headerEnd + 4, body, 0, already);
        while (already < length)
        {
            int n = await stream.ReadAsync(body, already, length - already);
            if (n <= 0) throw new EndOfStreamException("Connection closed mid-body");
            already += n;
        }
        req.Body = body;
        return req;
    }

    static async Task WriteResponse(NetworkStream stream, Response r)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(r.Status).Append(' ').Append(Reason(r.Status)).Append("\r\n");
        sb.Append("Content-Type: ").Append(r.ContentType).Append("\r\n");
        sb.Append("Content-Length: ").Append(r.Body.Length).Append("\r\n");
        sb.Append("Access-Control-Allow-Origin: *\r\n");
        sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
        sb.Append("Access-Control-Allow-Headers: Content-Type, X-Model-Name, X-Model-Key, X-Hidden-Parts\r\n");
        sb.Append("Access-Control-Allow-Private-Network: true\r\n");
        sb.Append("Connection: close\r\n\r\n");
        var head = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(head, 0, head.Length);
        if (r.Body.Length > 0) await stream.WriteAsync(r.Body, 0, r.Body.Length);
        await stream.FlushAsync();
    }

    static string Reason(int status)
    {
        switch (status)
        {
            case 200: return "OK";
            case 204: return "No Content";
            case 400: return "Bad Request";
            case 404: return "Not Found";
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
