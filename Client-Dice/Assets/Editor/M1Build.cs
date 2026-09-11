using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceDemo.M1.Editor
{
    public static class M1Build
    {
        private const string ScenePath = "Assets/Scenes/BattleM1.unity";

        [MenuItem("Rune Dice/Build M1 Windows x64")]
        public static void BuildWindows()
        {
            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/M1/Windows/RuneDiceM1.exe"));

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "Rune Dice M1";
            PlayerSettings.companyName = "Prototype";
            PlayerSettings.bundleVersion = BattleBalance.Version;
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
                throw new Exception("M1 build failed: " + report.summary.result + " / errors=" + report.summary.totalErrors);

            Debug.Log("M1_BUILD_OK path=" + output + " size=" + report.summary.totalSize);
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
