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

    /// Main-thread events for HUD animations.
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
        if (server == null) Hud.Error($"Port {Port} is in use. Is another viewer running?");
        else Hud.Info(Application.platform == RuntimePlatform.Android
            ? $"Ready. In the extension options, set the viewer to\n{address}"
            : "Ready. Send a Part Studio from Onshape.", 8f);
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
                        Hud.Busy($"Downloading {n.message} from Onshape");
                        DownloadStarted?.Invoke(n.message);
                        break;
                    case "unchanged":
                        Hud.Ok($"{n.message}: already up to date");
                        break;
                    case "error":
                        Hud.Error(n.message);
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

                var summary = await OnMainThread(async () =>
                {
                    Hud.Busy($"Loading {name}");
                    try { return await host.LoadGlbAsync(req.Body, name); }
                    catch (Exception e)
                    {
                        Hud.Error("Could not load the model: " + e.Message);
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
