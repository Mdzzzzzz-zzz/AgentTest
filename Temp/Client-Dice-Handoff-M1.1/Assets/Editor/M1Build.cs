using System;
using System.IO;
using DiceDemo.DemoC;
using DiceDemo.M1;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiceDemo.M1.Editor
{
    public static class M1Build
    {
        private const string ScenePath = "Assets/Scenes/BattleM1.unity";
        private const string SourceScenePath = "Assets/Scenes/DiceDemoC.unity";

        [MenuItem("符文骰子/阶段一/创建三维战斗场景")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            foreach (DemoCDiceRuntime runtime in Object.FindObjectsOfType<DemoCDiceRuntime>()) runtime.enabled = false;

            DemoCDie sourceDie = null;
            foreach (DemoCDie die in Resources.FindObjectsOfTypeAll<DemoCDie>())
                if (die != null && die.gameObject.scene == scene && die.gameObject.name == "原作三维骰子模板") { sourceDie = die; break; }
            if (sourceDie == null) throw new InvalidOperationException("找不到阶段丙的原作三维骰子模板。");

            BattleDie old = sourceDie.GetComponent<BattleDie>();
            if (old != null) Object.DestroyImmediate(old);
            BattleDie battleDie = sourceDie.gameObject.AddComponent<BattleDie>();
            battleDie.ConfigureTemplate(sourceDie.Body, sourceDie.Collider, sourceDie.VisualVariants);
            Object.DestroyImmediate(sourceDie);

            GameObject flowObject = GameObject.Find("阶段一三维战斗流程");
            if (flowObject == null) flowObject = new GameObject("阶段一三维战斗流程");
            if (flowObject.GetComponent<BattleFlow>() == null) flowObject.AddComponent<BattleFlow>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("M1三维场景创建完成：复用牌桌、九级骰子、数字深度遮挡材质和真实物理层。");
        }

        [MenuItem("符文骰子/阶段一/构建视窗六十四位版本")]
        public static void BuildWindows()
        {
            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/M1/Windows/RuneDiceM1.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "符文骰子 阶段一三维战斗"; PlayerSettings.companyName = "原作复刻验证组"; PlayerSettings.bundleVersion = BattleBalance.Version; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("阶段一三维战斗构建失败：" + report.summary.result + "，错误数=" + report.summary.totalErrors);
            Debug.Log("M1三维构建完成：" + output + "，大小=" + report.summary.totalSize);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++) if (scenes[i].path == path) { scenes[i].enabled = true; EditorBuildSettings.scenes = scenes; return; }
            Array.Resize(ref scenes, scenes.Length + 1); scenes[scenes.Length - 1] = new EditorBuildSettingsScene(path, true); EditorBuildSettings.scenes = scenes;
        }
        private static string GetArgument(string name) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
    }
}
