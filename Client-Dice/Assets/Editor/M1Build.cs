using System;
using System.IO;
using DiceDemo.M1;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceDemo.M1.Editor
{
    public static class M1Build
    {
        private const string ScenePath = "Assets/Scenes/BattleM1.unity";

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

        private static string GetArgument(string name) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
    }
}
