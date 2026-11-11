using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

/// One-click project + scene setup, and builds for PC (Link) and Quest (standalone).
/// Menu: QuestViewer > Setup Project and Scene. Also runnable via -executeMethod QuestViewerSetup.Run.
public static class QuestViewerSetup
{
    const string ScenePath = "Assets/QuestViewer/Scenes/Viewer.unity";
    const string MetaFeatureSetId = "com.meta.openxr.featureset.metaxr";
    const string SdkPrefabs = "Packages/com.meta.xr.sdk.core/Prefabs/";
    const string ProductName = "Onshape Quest Viewer";
    const string CompanyName = "almostinventor";
    const string AndroidId = "com.almostinventor.onshapequestviewer";
    const string Version = "1.0.0";
    const int VersionCode = 1; // increase for every Quest release

    [MenuItem("QuestViewer/Setup Project and Scene")]
    public static void Run()
    {
        ConfigureXR(BuildTargetGroup.Standalone);
        ConfigureXR(BuildTargetGroup.Android);
        ConfigureOVR();
        ConfigurePlayerAndRendering();
        IncludeGltfShaders();
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestViewerSetup] Done.");
    }

    [MenuItem("QuestViewer/Build PC (Link)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/OnshapeQuestViewer.exe");

    [MenuItem("QuestViewer/Build Quest (APK)")]
    public static void BuildQuest()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[QuestViewerSetup] Install Android Build Support (with SDK, NDK and OpenJDK) for this Unity version in Unity Hub.");
            return;
        }
        Build(BuildTarget.Android, "Builds/Android/OnshapeQuestViewer.apk");
    }

    static void Build(BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });
        Debug.Log($"[QuestViewerSetup] Build {report.summary.result}: {report.summary.outputPath}");
    }

    static void ConfigureXR(BuildTargetGroup group)
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
        {
            System.IO.Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);

        var settings = perTarget.SettingsForBuildTarget(group);
        if (!AssetDatabase.Contains(settings)) AssetDatabase.AddObjectToAsset(settings, perTarget);
        if (settings.Manager == null)
        {
            var mgr = ScriptableObject.CreateInstance<XRManagerSettings>();
            mgr.name = group + " Providers";
            AssetDatabase.AddObjectToAsset(mgr, perTarget);
            settings.Manager = mgr;
        }
        settings.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(settings.Manager, typeof(OpenXRLoader).FullName, group);

        var fs = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaFeatureSetId);
        if (fs != null)
        {
            fs.isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
        }
        else Debug.LogWarning($"[QuestViewerSetup] Meta XR OpenXR feature set not found for {group}.");

        var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (oxr == null) { Debug.LogWarning($"[QuestViewerSetup] No OpenXR settings for {group}."); return; }
        Enable(oxr.GetFeature<OculusTouchControllerProfile>());
        Enable(oxr.GetFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>());
        if (group == BuildTargetGroup.Android)
        {
            Enable(oxr.GetFeature<MetaQuestFeature>());
            // Meta Depth API (real objects hide the model) on standalone Quest.
            Enable(oxr.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature>());
            Enable(oxr.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.AROcclusionFeature>());
        }
        else
        {
            // The depth path is standalone-only; keep the PC (Link) version as it was.
            Disable(oxr.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature>());
            Disable(oxr.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.AROcclusionFeature>());
        }
        EditorUtility.SetDirty(oxr);
        EditorUtility.SetDirty(perTarget);
    }

    static void Enable(UnityEngine.XR.OpenXR.Features.OpenXRFeature feature)
    {
        if (feature != null) feature.enabled = true;
    }

    static void Disable(UnityEngine.XR.OpenXR.Features.OpenXRFeature feature)
    {
        if (feature != null) feature.enabled = false;
    }

    static void ConfigureOVR()
    {
        var cfg = OVRProjectConfig.CachedProjectConfig;
        cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        cfg.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
        cfg.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
        cfg.sceneSupport = OVRProjectConfig.FeatureSupport.Supported; // spatial data permission for depth
        cfg.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType>
        {
            OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S,
        };
        OVRProjectConfig.CommitProjectConfig(cfg);
    }

    static void ConfigurePlayerAndRendering()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // Quest (Android): 64-bit IL2CPP, Vulkan, network access for the local model server.
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, AndroidId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        // Passthrough needs an alpha channel in the eye buffer, so no HDR.
        for (int i = 0; i < QualitySettings.count; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) ConfigureUrp(q);
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) ConfigureUrp(d);
    }

    /// glTFast creates its materials at runtime, so builds would strip its shaders. Placeholder materials in a
    /// Resources folder keep them (opaque variants; double-sided is a property, not a variant, in URP).
    static void IncludeGltfShaders()
    {
        const string dir = "Assets/QuestViewer/Resources/GltfShaders";
        System.IO.Directory.CreateDirectory(dir);
        foreach (var name in new[] { "glTF-pbrMetallicRoughness", "glTF-unlit" })
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>($"Packages/com.unity.cloud.gltfast/Runtime/Shader/{name}.shadergraph");
            if (shader == null) { Debug.LogError($"[QuestViewerSetup] glTFast shader {name} not found."); continue; }
            var path = $"{dir}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(new Material(shader), path);
        }
    }

    static void ConfigureUrp(UniversalRenderPipelineAsset asset)
    {
        asset.supportsHDR = false;
        asset.msaaSampleCount = 4;
        EditorUtility.SetDirty(asset);
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureLayer(Grabber.HandsLayerName);

        var light = new GameObject("Key Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.None; // invisible over passthrough, not worth the GPU time
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Camera rig with passthrough.
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SdkPrefabs + "OVRCameraRig.prefab"));
        var manager = rig.GetComponent<OVRManager>();
        manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
        manager.isInsightPassthroughEnabled = true;
        rig.AddComponent<OVRPassthroughLayer>();

        var centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        var cam = centerEye.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.01f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        // Hands: tracking + pinch (XR Hands), and Meta hand meshes as a passthrough mask so real hands show.
        var trackingSpace = rig.transform.Find("TrackingSpace");
        var left = AddHand(trackingSpace, true);
        var right = AddHand(trackingSpace, false);
        var mask = rig.AddComponent<HandMask>();
        mask.handRenderers = new[]
        {
            AddHandMesh(trackingSpace.Find("LeftHandAnchor"), true),
            AddHandMesh(trackingSpace.Find("RightHandAnchor"), false),
        };
        mask.holeMaterial = HoleMaterial();

        var tape = rig.AddComponent<TapeMeasure>();
        tape.left = left;
        tape.right = right;
        tape.head = centerEye;
        tape.lineMaterial = UnlitMaterial("Assets/QuestViewer/TapeLine.mat", new Color(1f, 0.85f, 0.3f));

        // Controllers: grab/scale with grips, buttons for recall/HUD/hologram, models from Meta's prefab.
        AddController(FindDeep(trackingSpace, "LeftControllerAnchor"), OVRInput.Controller.LTouch);
        AddController(FindDeep(trackingSpace, "RightControllerAnchor"), OVRInput.Controller.RTouch);

        var lighting = rig.AddComponent<SceneLighting>();
        lighting.keyLight = light;
        lighting.head = centerEye;

        new GameObject("Hud").AddComponent<Hud>().head = centerEye;
        var hologram = HologramMaterial();
        HudMaterials();

        // 100 mm test cube, shown until the first model arrives.
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "TestCube_100mm";
        cube.transform.localScale = Vector3.one * 0.1f;
        cube.transform.position = new Vector3(0, 1.2f, 0.4f);
        cube.GetComponent<Renderer>().sharedMaterial = CubeMaterial();
        AddFloatingBody(cube);
        cube.AddComponent<Grabbable>();
        cube.AddComponent<SpawnInFront>().head = centerEye;
        cube.AddComponent<ModelLook>().hologramTemplate = hologram;

        // The Onshape model and the server the extension sends it to.
        var model = new GameObject("Model");
        AddFloatingBody(model);
        model.AddComponent<Grabbable>();
        model.AddComponent<BoxCollider>();
        model.AddComponent<SpawnInFront>().head = centerEye;
        model.AddComponent<ModelLook>().hologramTemplate = hologram;
        var host = model.AddComponent<ModelHost>();
        host.placeholder = cube;
        new GameObject("ModelServer").AddComponent<ModelServer>().host = host;

        var info = new GameObject("InfoHud").AddComponent<InfoHud>();
        info.head = centerEye;
        info.host = host;
        info.model = model.GetComponent<Grabbable>();

        var visor = new GameObject("VisorHud").AddComponent<VisorHud>();
        visor.head = centerEye;
        visor.model = model.GetComponent<Grabbable>();

        // Keypad (HOLO / HUD / LIGHT), placed low on your left like a desk panel; pinch to move it.
        var keypad = new GameObject("Keypad");
        keypad.AddComponent<Grabbable>().allowScale = false;
        var keypadSpawn = keypad.AddComponent<SpawnInFront>();
        keypadSpawn.head = centerEye;
        keypadSpawn.distance = 0.33f;
        keypadSpawn.belowEyes = 0.32f;
        keypadSpawn.sideways = -0.2f;
        keypadSpawn.tiltDegrees = 40f;
        keypadSpawn.recallable = false;
        keypad.AddComponent<Keypad>();

        // Depth occlusion (standalone Quest): Meta's depth manager plus material conversion for models.
        var depthGo = new GameObject("DepthOcclusion");
        var depthManager = depthGo.AddComponent<Meta.XR.EnvironmentDepth.EnvironmentDepthManager>();
        SetBool(depthManager, "_removeHands", true); // hands use the hand mask instead
        var depth = depthGo.AddComponent<DepthOcclusion>();
        depth.depthManager = depthManager;
        depth.occlusionTemplate = OcclusionMaterial();
        depth.alsoApplyTo = new[] { cube };

        System.IO.Directory.CreateDirectory("Assets/QuestViewer/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static PinchGrabber AddHand(Transform trackingSpace, bool left)
    {
        var go = new GameObject(left ? "LeftHand" : "RightHand");
        go.transform.SetParent(trackingSpace, false);
        var hand = go.AddComponent<TrackedHand>();
        hand.handedness = left ? UnityEngine.XR.Hands.Handedness.Left : UnityEngine.XR.Hands.Handedness.Right;
        hand.trackingSpace = trackingSpace;
        go.AddComponent<FingerColliders>();
        return go.AddComponent<PinchGrabber>();
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static void AddController(Transform anchor, OVRInput.Controller controller)
    {
        if (anchor == null) { Debug.LogError($"[QuestViewerSetup] No anchor for {controller}."); return; }
        var grabber = anchor.gameObject.AddComponent<ControllerGrabber>();
        grabber.controller = controller;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SdkPrefabs + "OVRControllerPrefab.prefab");
        if (prefab == null) return;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var helper = model.GetComponent<OVRControllerHelper>();
        if (helper != null)
        {
            helper.m_controller = controller;
            EditorUtility.SetDirty(helper);
        }
    }

    /// Floating physics body: no gravity, gently damped, so fingers can push it and throws glide to a stop.
    static void AddFloatingBody(GameObject go)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 1f;
        rb.linearDamping = 1.2f;
        rb.angularDamping = 1.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    static void EnsureLayer(string name)
    {
        if (LayerMask.NameToLayer(name) >= 0) return;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return;
        }
        Debug.LogError($"[QuestViewerSetup] No free layer for '{name}'.");
    }

    /// Hologram look (QuestViewer/Hologram). Lives in Resources so the shader is kept in builds.
    static Material HologramMaterial()
    {
        const string dir = "Assets/QuestViewer/Resources/Hologram";
        const string path = dir + "/Hologram.mat";
        System.IO.Directory.CreateDirectory(dir);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("QuestViewer/Hologram"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    /// Always-on-top HUD materials, loaded at runtime from Resources/Hud.
    static void HudMaterials()
    {
        const string dir = "Assets/QuestViewer/Resources/Hud";
        System.IO.Directory.CreateDirectory(dir);
        foreach (var (file, shader) in new[] { ("HudLine", "QuestViewer/HudLine"), ("HudText", "QuestViewer/HudText") })
        {
            var path = $"{dir}/{file}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_Color", Color.white); // colours come from vertex colours
            EditorUtility.SetDirty(mat);
        }
    }

    static OVRMeshRenderer AddHandMesh(Transform anchor, bool left)
    {
        var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SdkPrefabs + "OVRHandPrefab.prefab"), anchor);
        hand.name = left ? "LeftHandMesh" : "RightHandMesh";
        PrefabUtility.UnpackPrefabInstance(hand, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        SetEnum(hand.GetComponent<OVRHand>(), "HandType", (int)(left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight));
        SetEnum(hand.GetComponent<OVRSkeleton>(), "_skeletonType", (int)(left ? OVRSkeleton.SkeletonType.XRHandLeft : OVRSkeleton.SkeletonType.XRHandRight));
        SetEnum(hand.GetComponent<OVRMesh>(), "_meshType", (int)(left ? OVRMesh.MeshType.XRHandLeft : OVRMesh.MeshType.XRHandRight));
        // Never swap to Meta's visible "system gesture" hand material.
        SetEnum(hand.GetComponent<OVRMeshRenderer>(), "_systemGestureBehavior", (int)OVRMeshRenderer.SystemGestureBehavior.None);
        return hand.GetComponent<OVRMeshRenderer>();
    }

    static void SetEnum(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[QuestViewerSetup] Field {field} not found on {target.GetType().Name}"); return; }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[QuestViewerSetup] Field {field} not found on {target.GetType().Name}"); return; }
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Template for occlusion-aware model materials. Lives in Resources so the shader is kept in builds.
    static Material OcclusionMaterial()
    {
        const string dir = "Assets/QuestViewer/Resources/Occlusion";
        const string path = dir + "/OcclusionLit.mat";
        System.IO.Directory.CreateDirectory(dir);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("EnvironmentDepth/OcclusionLit");
            if (shader == null) { Debug.LogError("[QuestViewerSetup] EnvironmentDepth/OcclusionLit shader not found."); return null; }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static Material HoleMaterial()
    {
        const string path = "Assets/QuestViewer/HandMask.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("QuestViewer/PassthroughHole"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Inflate", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material UnlitMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CubeMaterial()
    {
        const string path = "Assets/QuestViewer/TestCube.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.55f, 0.1f) };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
