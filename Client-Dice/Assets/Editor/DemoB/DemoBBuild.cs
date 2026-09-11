using System;
using System.Collections.Generic;
using System.IO;
using DiceDemo.DemoB;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiceDemo.DemoB.Editor
{
    public static class DemoBBuild
    {
        private const string ScenePath = "Assets/Scenes/DiceDemoB.unity";
        private const string DeskPrefabPath = "Assets/DemoA/OriginalAssets/GameObject/Desk Base - Forest.prefab";
        private const string DiceVisualPrefabPath = "Assets/DemoB/OriginalAssets/GameObject/Regular Dice View.prefab";
        private const string RubberMaterialPath = "Assets/DemoB/OriginalAssets/PhysicsMaterial/Rubber.physicMaterial";

        [MenuItem("符文骰子/演示阶段乙/创建三维骰子场景")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh();
            GameObject deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskPrefabPath);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceVisualPrefabPath);
            PhysicMaterial rubber = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(RubberMaterialPath);
            if (deskPrefab == null) throw new InvalidOperationException("找不到原作森林牌桌资源：" + DeskPrefabPath);
            if (visualPrefab == null) throw new InvalidOperationException("找不到原作三维骰子资源：" + DiceVisualPrefabPath);
            if (rubber == null) throw new InvalidOperationException("找不到原作橡胶物理材质：" + RubberMaterialPath);
            // AssetRipper 导出的旧序列化字段在部分团结版本中会回落到默认值，因此显式恢复原作参数。
            rubber.dynamicFriction = 0.05f;
            rubber.staticFriction = 0.05f;
            rubber.bounciness = 0.55f;
            rubber.frictionCombine = PhysicMaterialCombine.Average;
            rubber.bounceCombine = PhysicMaterialCombine.Maximum;
            EditorUtility.SetDirty(rubber);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DiceDemoB";

            GameObject desk = PrefabUtility.InstantiatePrefab(deskPrefab) as GameObject;
            if (desk == null) throw new InvalidOperationException("原作森林牌桌实例化失败。");
            desk.name = "原作森林牌桌";
            RemoveMissingScripts(desk);

            Transform spawnPoint = new GameObject("骰子投掷起点").transform;
            spawnPoint.position = new Vector3(0f, 3.2f, 5f);

            GameObject dice = new GameObject("原作三维骰子");
            dice.transform.position = spawnPoint.position;
            Rigidbody body = dice.AddComponent<Rigidbody>();
            body.mass = DemoBDiceRuntime.OriginalMass;
            body.drag = DemoBDiceRuntime.OriginalLinearDamping;
            body.angularDrag = DemoBDiceRuntime.OriginalAngularDamping;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider collider = dice.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = DemoBDiceRuntime.OriginalColliderSize;
            collider.sharedMaterial = rubber;

            GameObject visual = PrefabUtility.InstantiatePrefab(visualPrefab) as GameObject;
            if (visual == null) throw new InvalidOperationException("原作三维骰子视觉实例化失败。");
            visual.name = "原作骰子九级视觉";
            visual.transform.SetParent(dice.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(visual);
            // 原作 Prefab 带有已剥离脚本的序列化记录。彻底解包后，播放器只接收清理后的普通对象，
            // 避免 AssetRipper 的旧 Prefab 覆盖记录导致关卡文件在运行时被判损坏。
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // 保留原作版本 12 网格。当前团结版本重新保存 Mesh 会产生带未烘焙 GI NaN 边界的版本 13 数据。
            SanitizeExportedMaterials(visual);

            Transform[] variants = FindVariants(visual.transform);
            if (variants.Length != 9) throw new InvalidOperationException("原作骰子视觉应包含九个等级，实际为：" + variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                variants[i].gameObject.SetActive(i == 0);
                CreateReadableFaceNumbers(variants[i]);
            }

            DemoBDiceRuntime runtime = dice.AddComponent<DemoBDiceRuntime>();
            runtime.Configure(body, collider, variants, spawnPoint);
            ConfigureFallTrigger(FindChild(desk.transform, "Front Trigger"), runtime);
            ConfigureFallTrigger(FindChild(desk.transform, "Front Trigger (1)"), runtime);

            CreateCamera();
            CreateLight();
            ConfigureRenderSettings();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("DEMO_B_SCENE_OK path=" + ScenePath + " variants=" + variants.Length +
                      " sides=" + runtime.GetActiveSides().Length);
        }

        [MenuItem("符文骰子/演示阶段乙/构建视窗六十四位版本")]
        public static void BuildWindows()
        {
            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Demo-B/Windows/RuneDiceDemoB.exe"));

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "符文骰子 演示阶段乙";
            PlayerSettings.companyName = "原作复刻验证组";
            PlayerSettings.bundleVersion = "阶段乙";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
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
                throw new Exception("演示阶段乙构建失败：" + report.summary.result + "，错误数=" + report.summary.totalErrors);

            Debug.Log("DEMO_B_BUILD_OK path=" + output + " size=" + report.summary.totalSize);
        }

        private static Transform[] FindVariants(Transform root)
        {
            List<Transform> variants = new List<Transform>();
            for (int level = 1; level <= 9; level++)
            {
                Transform found = FindChild(root, "Dice Pixel " + level);
                if (found != null) variants.Add(found);
            }
            return variants.ToArray();
        }

        private static void SanitizeExportedMaterials(GameObject visual)
        {
            const string generatedFolder = "Assets/DemoB/Generated";
            Dictionary<Material, Material> sanitized = new Dictionary<Material, Material>();
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                // 三维面数字使用团结内置字体材质，不属于 AssetRipper 导出的材质。
                if (renderer.GetComponent<TextMesh>() != null) continue;
                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] cleanMaterials = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    Material source = sourceMaterials[i];
                    if (source == null) continue;
                    if (!sanitized.TryGetValue(source, out Material clean))
                    {
                        bool isOutline = source.name.IndexOf("Outline", StringComparison.OrdinalIgnoreCase) >= 0;
                        Shader shader = Shader.Find(isOutline ? "Unlit/Color" : "Standard");
                        if (shader == null) throw new InvalidOperationException("找不到内置修复着色器。");
                        Color color = ReadMaterialColor(source, isOutline ? Color.black : Color.white);
                        clean = new Material(shader) { name = source.name + "_修复", color = color };
                        if (!isOutline)
                        {
                            clean.SetFloat("_Metallic", 0f);
                            clean.SetFloat("_Glossiness", 0.08f);
                        }
                        string safeName = source.name.Replace('/', '_').Replace('\\', '_');
                        string path = generatedFolder + "/" + safeName + "_修复.mat";
                        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (existing == null)
                        {
                            AssetDatabase.CreateAsset(clean, path);
                        }
                        else
                        {
                            EditorUtility.CopySerialized(clean, existing);
                            Object.DestroyImmediate(clean);
                            clean = existing;
                            EditorUtility.SetDirty(clean);
                        }
                        sanitized.Add(source, clean);
                        Debug.Log("DEMO_B_MATERIAL_REPAIRED name=" + source.name + " color=" + color);
                    }
                    cleanMaterials[i] = clean;
                }
                renderer.sharedMaterials = cleanMaterials;
            }
        }

        private static Color ReadMaterialColor(Material material, Color fallback)
        {
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return fallback;
        }

        private static void CreateReadableFaceNumbers(Transform variant)
        {
            for (int face = 1; face <= 6; face++)
            {
                Transform side = FindChild(variant, "Side " + face);
                if (side == null) continue;
                Transform numberTransform = FindChild(side, "Number Text");
                Vector3 localPosition = Vector3.back * 0.1334f;
                Quaternion localRotation = Quaternion.identity;
                Vector3 localScale = Vector3.one;
                if (numberTransform != null)
                {
                    localPosition = numberTransform.localPosition;
                    localRotation = numberTransform.localRotation;
                    localScale = numberTransform.localScale;
                    Object.DestroyImmediate(numberTransform.gameObject);
                }
                Transform icon = FindChild(side, "Icon");
                if (icon != null) Object.DestroyImmediate(icon.gameObject);

                // 不保留 AssetRipper 导出的文字 RectTransform/TMP 渲染器，改建为干净的三维文字对象。
                numberTransform = new GameObject("Number Text").transform;
                numberTransform.SetParent(side, false);
                numberTransform.localPosition = localPosition;
                numberTransform.localRotation = localRotation;
                numberTransform.localScale = localScale;

                TextMesh number = numberTransform.gameObject.AddComponent<TextMesh>();
                number.text = face.ToString();
                number.anchor = TextAnchor.MiddleCenter;
                number.alignment = TextAlignment.Center;
                number.fontSize = 64;
                number.characterSize = 0.035f;
                number.color = new Color(0.18f, 0.07f, 0.23f, 1f);
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    number.font = font;
                    MeshRenderer renderer = numberTransform.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = font.material;
                }
            }
        }

        private static void ConfigureFallTrigger(Transform trigger, DemoBDiceRuntime runtime)
        {
            if (trigger == null) return;
            BoxCollider collider = trigger.GetComponent<BoxCollider>();
            if (collider == null || !collider.isTrigger) return;
            DemoBFallDetector detector = trigger.gameObject.GetComponent<DemoBFallDetector>();
            if (detector == null) detector = trigger.gameObject.AddComponent<DemoBFallDetector>();
            detector.Configure(runtime);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("主相机");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 24.47f, -20.89f);
            cameraObject.transform.rotation = Quaternion.Euler(33f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = DemoBDiceRuntime.OriginalCameraSize;
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
            light.intensity = 0.8f;
            light.shadowStrength = 0.75f;
            light.color = new Color(1f, 0.94f, 0.84f);
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
            {
                if (scenes[i].path != path) continue;
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes;
                return;
            }
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
