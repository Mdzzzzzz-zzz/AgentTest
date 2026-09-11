using System;
using System.Collections.Generic;
using System.IO;
using DiceDemo.DemoC;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiceDemo.DemoC.Editor
{
    public static class DemoCBuild
    {
        private const string ScenePath = "Assets/Scenes/DiceDemoC.unity";
        private const string DeskPrefabPath = "Assets/DemoA/OriginalAssets/GameObject/Desk Base - Forest.prefab";
        private const string DiceVisualPrefabPath = "Assets/DemoB/OriginalAssets/GameObject/Regular Dice View.prefab";
        private const string RubberMaterialPath = "Assets/DemoB/OriginalAssets/PhysicsMaterial/Rubber.physicMaterial";

        [MenuItem("符文骰子/演示阶段丙/创建多骰碰撞合并场景")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh();
            GameObject deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskPrefabPath);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceVisualPrefabPath);
            PhysicMaterial rubber = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(RubberMaterialPath);
            if (deskPrefab == null) throw new InvalidOperationException("找不到原作森林牌桌资源：" + DeskPrefabPath);
            if (visualPrefab == null) throw new InvalidOperationException("找不到原作三维骰子资源：" + DiceVisualPrefabPath);
            if (rubber == null) throw new InvalidOperationException("找不到原作橡胶物理材质：" + RubberMaterialPath);

            rubber.dynamicFriction = 0.05f;
            rubber.staticFriction = 0.05f;
            rubber.bounciness = 0.55f;
            rubber.frictionCombine = PhysicMaterialCombine.Average;
            rubber.bounceCombine = PhysicMaterialCombine.Maximum;
            EditorUtility.SetDirty(rubber);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DiceDemoC";

            GameObject desk = PrefabUtility.InstantiatePrefab(deskPrefab) as GameObject;
            if (desk == null) throw new InvalidOperationException("原作森林牌桌实例化失败。");
            desk.name = "原作森林牌桌";
            RemoveMissingScripts(desk);

            GameObject controller = new GameObject("阶段丙多骰控制器");
            DemoCDiceRuntime runtime = controller.AddComponent<DemoCDiceRuntime>();
            Transform diceRoot = new GameObject("场上骰子容器").transform;
            diceRoot.SetParent(controller.transform, false);
            Transform spawnPoint = new GameObject("连续投掷起点").transform;
            spawnPoint.SetParent(controller.transform, false);
            spawnPoint.position = new Vector3(0f, 3.2f, 5f);

            DemoCDie template = CreateDiceTemplate(controller.transform, visualPrefab, rubber);
            runtime.Configure(template, spawnPoint, diceRoot);
            DisableFallTriggerCollider(FindChild(desk.transform, "Front Trigger"));
            DisableFallTriggerCollider(FindChild(desk.transform, "Front Trigger (1)"));
            CreatePhysicsArena(controller.transform, rubber);
            CreateFallTrigger(controller.transform, runtime);

            CreateCamera();
            CreateLight();
            ConfigureRenderSettings();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("DEMO_C_SCENE_OK path=" + ScenePath + " maximum=" + DemoCDiceRuntime.MaximumDice +
                      " variants=" + template.VisualVariants.Length + " faces=" + template.CountFaces(1));
        }

        [MenuItem("符文骰子/演示阶段丙/构建视窗六十四位版本")]
        public static void BuildWindows()
        {
            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Demo-C/Windows/RuneDiceDemoC.exe"));

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "符文骰子 演示阶段丙";
            PlayerSettings.companyName = "原作复刻验证组";
            PlayerSettings.bundleVersion = "阶段丙";
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
                throw new Exception("演示阶段丙构建失败：" + report.summary.result + "，错误数=" + report.summary.totalErrors);

            Debug.Log("DEMO_C_BUILD_OK path=" + output + " size=" + report.summary.totalSize);
        }

        private static DemoCDie CreateDiceTemplate(Transform parent, GameObject visualPrefab, PhysicMaterial rubber)
        {
            GameObject templateObject = new GameObject("原作三维骰子模板");
            templateObject.transform.SetParent(parent, false);
            templateObject.transform.position = new Vector3(0f, -20f, 0f);

            Rigidbody body = templateObject.AddComponent<Rigidbody>();
            body.mass = DemoCDiceRuntime.OriginalMass;
            body.drag = DemoCDiceRuntime.OriginalLinearDamping;
            body.angularDrag = DemoCDiceRuntime.OriginalAngularDamping;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 8f;

            BoxCollider collider = templateObject.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = DemoCDiceRuntime.OriginalColliderSize;
            collider.sharedMaterial = rubber;

            GameObject visual = PrefabUtility.InstantiatePrefab(visualPrefab) as GameObject;
            if (visual == null) throw new InvalidOperationException("原作三维骰子视觉实例化失败。");
            visual.name = "原作骰子九级视觉";
            visual.transform.SetParent(templateObject.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(visual);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            SanitizeExportedMaterials(visual);

            Transform[] variants = FindVariants(visual.transform);
            if (variants.Length != 9) throw new InvalidOperationException("原作骰子视觉应包含九个等级，实际为：" + variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                variants[i].gameObject.SetActive(i == 0);
                CreateReadableFaceNumbers(variants[i], CreateDepthTestFontMaterial());
            }

            DemoCDie template = templateObject.AddComponent<DemoCDie>();
            template.ConfigureTemplate(body, collider, variants);
            templateObject.SetActive(false);
            return template;
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
            const string generatedFolder = "Assets/DemoC/Generated";
            if (!AssetDatabase.IsValidFolder("Assets/DemoC")) AssetDatabase.CreateFolder("Assets", "DemoC");
            if (!AssetDatabase.IsValidFolder(generatedFolder)) AssetDatabase.CreateFolder("Assets/DemoC", "Generated");

            Dictionary<Material, Material> sanitized = new Dictionary<Material, Material>();
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
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
                        clean = new Material(shader)
                        {
                            name = source.name + "_阶段丙修复",
                            color = ReadMaterialColor(source, isOutline ? Color.black : Color.white)
                        };
                        if (!isOutline)
                        {
                            clean.SetFloat("_Metallic", 0f);
                            clean.SetFloat("_Glossiness", 0.08f);
                        }
                        string safeName = source.name.Replace('/', '_').Replace('\\', '_');
                        string path = generatedFolder + "/" + safeName + "_阶段丙修复.mat";
                        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (existing == null) AssetDatabase.CreateAsset(clean, path);
                        else
                        {
                            EditorUtility.CopySerialized(clean, existing);
                            Object.DestroyImmediate(clean);
                            clean = existing;
                            EditorUtility.SetDirty(clean);
                        }
                        sanitized.Add(source, clean);
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

        private static Material CreateDepthTestFontMaterial()
        {
            const string materialPath = "Assets/DemoC/Generated/骰子数字深度遮挡.mat";
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return null;

            Shader depthShader = Shader.Find("DiceDemo/数字深度遮挡");
            if (depthShader == null) throw new InvalidOperationException("找不到骰子数字深度遮挡着色器。");

            Material source = font.material;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source) { name = "骰子数字深度遮挡" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = depthShader;
            material.SetTexture("_MainTex", source.mainTexture);
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateReadableFaceNumbers(Transform variant, Material numberMaterial)
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
                    if (renderer != null) renderer.sharedMaterial = numberMaterial == null ? font.material : numberMaterial;
                }
            }
        }

        private static void ConfigureFallTrigger(Transform trigger, DemoCDiceRuntime runtime)
        {
            if (trigger == null) return;
            BoxCollider collider = trigger.GetComponent<BoxCollider>();
            if (collider == null || !collider.isTrigger) return;
            DemoCFallDetector detector = trigger.gameObject.GetComponent<DemoCFallDetector>();
            if (detector == null) detector = trigger.gameObject.AddComponent<DemoCFallDetector>();
            detector.Configure(runtime);
        }

        private static void DisableFallTriggerCollider(Transform trigger)
        {
            if (trigger == null) return;
            BoxCollider collider = trigger.GetComponent<BoxCollider>();
            if (collider != null) collider.enabled = false;
        }

        private static void CreateFallTrigger(Transform parent, DemoCDiceRuntime runtime)
        {
            GameObject triggerObject = new GameObject("阶段丙低位掉落检测");
            triggerObject.transform.SetParent(parent, false);
            triggerObject.transform.position = new Vector3(0f, -5f, 1.5f);
            BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 1f, 9f);
            DemoCFallDetector detector = triggerObject.AddComponent<DemoCFallDetector>();
            detector.Configure(runtime);
        }

        private static void CreatePhysicsArena(Transform parent, PhysicMaterial rubber)
        {
            GameObject arena = new GameObject("阶段丙牌桌物理碰撞层");
            arena.transform.SetParent(parent, false);

            CreateStaticCollider(arena.transform, "阶段丙桌面碰撞", new Vector3(0f, 0f, 1.5f), new Vector3(10f, 0.2f, 9f), rubber);
            CreateStaticCollider(arena.transform, "阶段丙左侧边界", new Vector3(-5f, 2f, 1.5f), new Vector3(0.2f, 4f, 9f), rubber);
            CreateStaticCollider(arena.transform, "阶段丙右侧边界", new Vector3(5f, 2f, 1.5f), new Vector3(0.2f, 4f, 9f), rubber);
            CreateStaticCollider(arena.transform, "阶段丙后侧边界", new Vector3(0f, 2f, 6f), new Vector3(10f, 4f, 0.2f), rubber);
        }

        private static void CreateStaticCollider(Transform parent, string name, Vector3 position, Vector3 size, PhysicMaterial rubber)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
            collider.sharedMaterial = rubber;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("主相机");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 24.47f, -20.89f);
            cameraObject.transform.rotation = Quaternion.Euler(33f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = DemoCDiceRuntime.OriginalCameraSize;
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
