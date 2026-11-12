using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

/// HTTP endpoint for the "Send to Quest" extension (port 8723):
///   GET  /status  -> {"ok":true,"model":"…","key":"…","address":"…"}
///   POST /notify  {"state":"downloading|error|unchanged","message":"…"}  -> shows it in the headset
///   POST /model   binary glTF body, headers X-Model-Name / X-Model-Key / X-Hidden-Parts -> loads it, replies when shown
/// On PC it listens on localhost only; on Quest on the local network, so Chrome on the PC can reach it.
public class ModelServer : MonoBehaviour
{
    public const int Port = 8723;
    public ModelHost host;

    /// Raised on the main thread when a download starts / something fails.
    public static event Action<string> DownloadStarted;
    public static event Action<string> Failed;

    MiniHttpServer server;
    readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
    volatile string modelName;
    volatile string modelKey;
    volatile bool loading;
    string address;

    [Serializable] class Notice { public string state; public string message; }

    void OnEnable()
    {
        bool lan = Application.platform == RuntimePlatform.Android;
        address = (lan ? LocalIPv4() ?? "?" : "127.0.0.1") + ":" + Port;
        try
        {
            server = new MiniHttpServer(lan ? IPAddress.Any : IPAddress.Loopback, Port, Handle);
            server.Start();
            Debug.Log("[ModelServer] Listening on " + address);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ModelServer] Port {Port} unavailable: {e.Message}");
            server = null;
        }
    }

    void Start()
    {
        if (server == null) Status.Error($"Port {Port} in use");
        else Status.Info(Application.platform == RuntimePlatform.Android ? $"Viewer address\n{address}" : "Ready", 30f);
    }

    void OnDisable()
    {
        server?.Dispose();
        server = null;
    }

    void Update()
    {
        while (mainThread.TryDequeue(out var a)) a();
    }

    Task<T> OnMainThread<T>(Func<Task<T>> work)
    {
        var tcs = new TaskCompletionSource<T>();
        mainThread.Enqueue(async () =>
        {
            try { tcs.SetResult(await work()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    async Task<MiniHttpServer.Response> Handle(MiniHttpServer.Request req)
    {
        if (req.Method == "GET" && req.Path == "/status")
        {
            return MiniHttpServer.Response.Json(200,
                $"{{\"ok\":true,\"model\":{Json(modelName)},\"key\":{Json(modelKey)},\"address\":{Json(address)},\"loading\":{(loading ? "true" : "false")}}}");
        }

        if (req.Method == "POST" && req.Path == "/notify")
        {
            var n = JsonUtility.FromJson<Notice>(req.BodyText) ?? new Notice();
            mainThread.Enqueue(() =>
            {
                switch (n.state)
                {
                    case "downloading":
                        Status.Busy($"Receiving\n{n.message}");
                        DownloadStarted?.Invoke(n.message);
                        break;
                    case "unchanged":
                        Status.Ok($"Up to date\n{n.message}");
                        break;
                    case "error":
                        Status.Error(n.message);
                        Failed?.Invoke(n.message);
                        break;
                }
            });
            return MiniHttpServer.Response.Json(200, "{\"ok\":true}");
        }

        if (req.Method == "POST" && req.Path == "/model")
        {
            if (loading) return MiniHttpServer.Response.Json(409, "{\"ok\":false,\"message\":\"Already loading a model.\"}");
            loading = true;
            try
            {
                string name = Unescape(req.Header("X-Model-Name")) ?? "Onshape part";
                string key = Unescape(req.Header("X-Model-Key"));
                int.TryParse(req.Header("X-Hidden-Parts"), out int hidden);
                string partInfo = DecodeBase64(req.Header("X-Part-Info"));

                var summary = await OnMainThread(async () =>
                {
                    Status.Busy($"Loading\n{name}");
                    try { return await host.LoadGlbAsync(req.Body, name, partInfo); }
                    catch (Exception e)
                    {
                        Status.Error("Load failed\n" + e.Message);
                        Failed?.Invoke(e.Message);
                        throw;
                    }
                });

                modelName = name;
                modelKey = key;
                string note = hidden > 0 ? $", {hidden} hidden part{(hidden == 1 ? "" : "s")} skipped" : "";
                return MiniHttpServer.Response.Json(200, $"{{\"ok\":true,\"message\":{Json(summary + note)}}}");
            }
            catch (Exception e)
            {
                return MiniHttpServer.Response.Json(400, $"{{\"ok\":false,\"message\":{Json(e.Message)}}}");
            }
            finally
            {
                loading = false;
            }
        }

        return MiniHttpServer.Response.Json(404, "{\"ok\":false,\"message\":\"Use GET /status, POST /notify or POST /model.\"}");
    }

    static string Unescape(string s) => string.IsNullOrEmpty(s) ? null : Uri.UnescapeDataString(s);

    static string DecodeBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch (FormatException) { return null; }
    }

    static string Json(string s) =>
        s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    /// The address other devices on the network use to reach this one (no packets are sent).
    static string LocalIPv4()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("10.255.255.255", 1);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
        }
        catch (Exception) { return null; }
    }
}
