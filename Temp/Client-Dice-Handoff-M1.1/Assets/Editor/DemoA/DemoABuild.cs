using System;
using System.IO;
using DiceDemo.DemoA;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiceDemo.DemoA.Editor
{
    public static class DemoABuild
    {
        private const string ScenePath = "Assets/Scenes/DeskDemoA.unity";
        private const string DeskPrefabPath = "Assets/DemoA/OriginalAssets/GameObject/Desk Base - Forest.prefab";

        [MenuItem("符文骰子/Demo-A/创建原作牌桌")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("找不到原作森林牌桌资源：" + DeskPrefabPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DeskDemoA";

            GameObject desk = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (desk == null)
                throw new InvalidOperationException("原作森林牌桌实例化失败。");

            desk.name = "原作森林牌桌";
            RemoveMissingScripts(desk);

            Transform spawnRoot = FindChild(desk.transform, "Prespawned Dices");
            Transform frontTrigger = FindChild(desk.transform, "Front Trigger");
            Transform frontTriggerSecond = FindChild(desk.transform, "Front Trigger (1)");
            int spawnPointCount = spawnRoot == null ? 0 : spawnRoot.childCount;
            int boundaryCount = CountActiveColliders(desk, false);
            int triggerCount = 0;

            DemoADeskRuntime runtime = desk.AddComponent<DemoADeskRuntime>();
            runtime.Configure(spawnRoot, frontTrigger, spawnPointCount, boundaryCount, 0);

            triggerCount += AddFallDetector(frontTrigger, runtime);
            triggerCount += AddFallDetector(frontTriggerSecond, runtime);
            runtime.Configure(spawnRoot, frontTrigger, spawnPointCount, boundaryCount, triggerCount);

            CreateSpawnPointMarkers(desk.transform, spawnRoot);
            CreateCamera();
            CreateLight();
            ConfigureRenderSettings();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("DEMO_A_SCENE_OK path=" + ScenePath + " spawnPoints=" + spawnPointCount +
                      " boundaries=" + boundaryCount + " triggers=" + triggerCount);
        }

        [MenuItem("符文骰子/Demo-A/构建 Windows x64")]
        public static void BuildWindows()
        {
            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Demo-A/Windows/RuneDiceDemoA.exe"));

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "符文骰子 Demo-A";
            PlayerSettings.companyName = "原作复刻验证组";
            PlayerSettings.bundleVersion = "Demo-A";
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Demo-A 构建失败：" + report.summary.result + " / errors=" + report.summary.totalErrors);

            Debug.Log("DEMO_A_BUILD_OK path=" + output + " size=" + report.summary.totalSize);
        }

        private static int AddFallDetector(Transform trigger, DemoADeskRuntime runtime)
        {
            if (trigger == null) return 0;
            BoxCollider collider = trigger.GetComponent<BoxCollider>();
            if (collider == null || !collider.isTrigger) return 0;
            DemoAFallDetector detector = trigger.gameObject.GetComponent<DemoAFallDetector>();
            if (detector == null) detector = trigger.gameObject.AddComponent<DemoAFallDetector>();
            detector.Configure(runtime);
            return 1;
        }

        private static int CountActiveColliders(GameObject root, bool triggers)
        {
            int count = 0;
            foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>(true))
                if (collider.gameObject.activeInHierarchy && collider.isTrigger == triggers) count++;
            return count;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("主相机");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 24.47f, -20.89f);
            cameraObject.transform.rotation = Quaternion.Euler(33f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = DemoADeskRuntime.OriginalCameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.08f);
            camera.allowHDR = true;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("主光");
            lightObject.transform.position = new Vector3(0f, 3f, 0f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;
            light.shadowStrength = 0.7f;
            light.color = new Color(1f, 0.94f, 0.84f);
        }

        private static void CreateSpawnPointMarkers(Transform deskRoot, Transform spawnRoot)
        {
            if (spawnRoot == null) return;
            GameObject markerRoot = new GameObject("预生成点可视化");
            markerRoot.transform.SetParent(deskRoot, false);
            Material material = CreateMarkerMaterial();
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Transform source = spawnRoot.GetChild(i);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "预生成点 " + (i + 1);
                marker.transform.SetParent(markerRoot.transform, false);
                marker.transform.position = source.position + Vector3.up * 0.045f;
                marker.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f);
                marker.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
        }

        private static Material CreateMarkerMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "DemoA 预生成点材质" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.2f, 0.85f, 0.95f, 0.72f));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.2f, 0.85f, 0.95f, 0.72f));
            return material;
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.35f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.2f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.06f, 0.04f);
            RenderSettings.ambientIntensity = 0.9f;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void RemoveMissingScripts(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].path == path) { scenes[i].enabled = true; EditorBuildSettings.scenes = scenes; return; }

            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = scenes;
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
