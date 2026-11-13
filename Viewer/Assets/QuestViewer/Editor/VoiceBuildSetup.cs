using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// Voice commands on the Quest: Vosk's Android library (from Maven Central, resolved by Gradle at build time),
/// the microphone permission, and the English model in StreamingAssets/vosk-model (downloaded once, not in git).
public class VoiceBuildSetup : IPostGenerateGradleAndroidProject
{
    const string VoskAar = "com.alphacephei:vosk-android:0.3.75@aar";
    const string JnaAar = "net.java.dev.jna:jna:5.18.1@aar";
    const string RecordAudio = "android.permission.RECORD_AUDIO";
    public const string ModelDir = "Assets/StreamingAssets/vosk-model";
    const string ModelName = "vosk-model-small-en-us-0.15";
    const string ModelUrl = "https://alphacephei.com/vosk/models/" + ModelName + ".zip";

    public int callbackOrder => 100;

    /// path is the generated unityLibrary module.
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string gradle = Path.Combine(path, "build.gradle");
        string text = File.ReadAllText(gradle);
        if (!text.Contains(VoskAar))
            File.AppendAllText(gradle, $"\n// Voice commands (VoiceBuildSetup.cs)\ndependencies {{\n    implementation '{VoskAar}'\n    implementation '{JnaAar}'\n}}\n");

        string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        string xml = File.ReadAllText(manifest);
        if (!xml.Contains(RecordAudio))
        {
            int at = xml.IndexOf("<application", System.StringComparison.Ordinal);
            if (at < 0) throw new BuildFailedException("[Voice] No <application> in " + manifest);
            xml = xml.Insert(at, $"<uses-permission android:name=\"{RecordAudio}\" />\n  ");
            File.WriteAllText(manifest, xml);
        }
        if (!HasModel()) Debug.LogWarning("[Voice] No voice model in " + ModelDir + ". Run QuestViewer > Download Voice Model.");
    }

    public static bool HasModel() => File.Exists(Path.Combine(ModelDir, "uuid"));

    [MenuItem("QuestViewer/Download Voice Model")]
    public static void DownloadModel()
    {
        if (HasModel()) { Debug.Log("[Voice] Model already in " + ModelDir); return; }
        string zip = Path.Combine(Path.GetTempPath(), ModelName + ".zip");
        string unpack = Path.Combine(Path.GetTempPath(), ModelName + "-unpack");
        try
        {
            EditorUtility.DisplayProgressBar("Voice model", "Downloading " + ModelName + " (about 40 MB)", 0.2f);
            using (var http = new System.Net.Http.HttpClient())
            using (var file = File.Create(zip))
                http.GetStreamAsync(ModelUrl).Result.CopyTo(file);

            EditorUtility.DisplayProgressBar("Voice model", "Unpacking", 0.8f);
            if (Directory.Exists(unpack)) Directory.Delete(unpack, true);
            ZipFile.ExtractToDirectory(zip, unpack);
            Directory.CreateDirectory(Path.GetDirectoryName(ModelDir));
            if (Directory.Exists(ModelDir)) Directory.Delete(ModelDir, true);
            Directory.Move(Path.Combine(unpack, ModelName), ModelDir);
            // Vosk's StorageService copies the model out of the APK only when this file changes.
            File.WriteAllText(Path.Combine(ModelDir, "uuid"), ModelName);
            Debug.Log("[Voice] Model ready in " + ModelDir);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (File.Exists(zip)) File.Delete(zip);
            if (Directory.Exists(unpack)) Directory.Delete(unpack, true);
            AssetDatabase.Refresh();
        }
    }
}
