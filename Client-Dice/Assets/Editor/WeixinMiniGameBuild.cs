using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceDemo.M1.Editor
{
    /// <summary>
    /// 微信小游戏（WeixinMiniGame）构建入口。
    ///
    /// 平台能力来源：团结 Hub 安装的「WeixinMiniGameSupport」平台模块
    /// （Editor/Data/PlaybackEngines/WeixinMiniGameSupport，自带
    ///  WeixinMiniGamePlayerBuildProgram.exe、BuildTools/Emscripten、WebGLTemplates 等）。
    /// 因此**不需要**再通过 git URL 安装 minigame-tuanjie-transform-sdk——
    /// 那是旧版 Unity 的做法。
    ///
    /// 使用方式：
    ///   菜单：符文骰子 / 小游戏 / ...
    ///   命令行：-executeMethod DiceDemo.M1.Editor.WeixinMiniGameBuild.BuildWeixinMiniGame
    ///
    /// 说明：脚本刻意用 Enum.Parse 解析目标名而非直接写 BuildTarget.WeixinMiniGame，
    /// 这样万一枚举成员命名与预期不同，会抛出可读异常而不是编译失败。
    /// </summary>
    public static class WeixinMiniGameBuild
    {
        private const string ScenePath = "Assets/Scenes/BattleM1.unity";
        private const string TargetName = "WeixinMiniGame";

        /// <summary>竖屏基准分辨率（方案 §8 竖屏 UI 规格：1080×1920）。</summary>
        private const int PortraitWidth = 1080;
        private const int PortraitHeight = 1920;

        // ────────────────────────────── 体检 ──────────────────────────────

        [MenuItem("符文骰子/小游戏/体检（打印平台与构建目标）")]
        public static void Probe()
        {
            Debug.Log("=== WeixinMiniGame 体检开始 ===");

            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log("当前激活构建目标: " + active + " / group=" + BuildPipeline.GetBuildTargetGroup(active));

            string[] related = Enum.GetNames(typeof(BuildTarget))
                .Where(n => n.IndexOf("Weixin", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("MiniGame", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("WebGL", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(n => n)
                .ToArray();
            Debug.Log("与微信小游戏/WebGL 相关的 BuildTarget: "
                      + (related.Length == 0 ? "(一个都没有)" : string.Join(", ", related)));

            bool exists = Enum.GetNames(typeof(BuildTarget)).Contains(TargetName);
            Debug.Log("BuildTarget." + TargetName + " 是否存在: " + exists);

            if (exists)
            {
                BuildTarget target = (BuildTarget)Enum.Parse(typeof(BuildTarget), TargetName);
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                Debug.Log("对应 BuildTargetGroup: " + group);
                Debug.Log("平台模块是否就绪(IsBuildTargetSupported): " + BuildPipeline.IsBuildTargetSupported(group, target));
            }
            else
            {
                Debug.LogError("找不到 " + TargetName + " —— 请确认团结 Hub 已为该编辑器版本安装 WeixinMiniGame 平台组件。");
            }

            Debug.Log("场景是否在构建列表: " + EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled));
            Debug.Log("色彩空间: " + PlayerSettings.colorSpace + "（微信端社区建议 Gamma，待实测）");
            Debug.Log("=== WeixinMiniGame 体检结束 ===");
        }

        // ────────────────────────────── 构建 ──────────────────────────────

        [MenuItem("符文骰子/小游戏/构建微信小游戏")]
        public static void BuildWeixinMiniGame()
        {
            if (!Enum.GetNames(typeof(BuildTarget)).Contains(TargetName))
                throw new InvalidOperationException(
                    "当前编辑器不含 BuildTarget." + TargetName + "。请先用团结 Hub 为该编辑器版本安装 WeixinMiniGame 平台组件。");

            BuildTarget target = (BuildTarget)Enum.Parse(typeof(BuildTarget), TargetName);
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException("微信小游戏平台模块未就绪（IsBuildTargetSupported=false）。");

            string output = GetArgument("-buildPath");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/WxMiniGame"));
            Directory.CreateDirectory(output);

            // 竖屏产品信息（与方案 §8 的 1080×1920 基准一致）
            PlayerSettings.productName = "符文骰子 阶段一三维战斗";
            PlayerSettings.companyName = "原作复刻验证组";
            PlayerSettings.defaultScreenWidth = PortraitWidth;
            PlayerSettings.defaultScreenHeight = PortraitHeight;

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log("正在切换到 " + target + " ...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                    throw new InvalidOperationException("切换构建目标失败：" + target);
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("微信小游戏构建失败：" + report.summary.result
                                    + "，错误数=" + report.summary.totalErrors);

            Debug.Log("微信小游戏构建完成：" + output + "，总大小=" + report.summary.totalSize + " 字节");

            ReportMainPackage(output);
        }

        /// <summary>
        /// 统计产物里各部分的字节数，重点回答「主包是否 ≤4MB」。
        /// 微信官方口径：主包 ≤4MB，单个分包不限，整包 ≤30MB。
        /// </summary>
        private static void ReportMainPackage(string root)
        {
            if (!Directory.Exists(root))
            {
                Debug.LogWarning("产物目录不存在，跳过体积拆解：" + root);
                return;
            }

            Debug.Log("=== 产物体积拆解（" + root + "） ===");
            foreach (string dir in Directory.GetDirectories(root))
            {
                long bytes = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                Debug.Log(string.Format("  {0,-28} {1,12:N0} 字节  ({2:N2} MB)", Path.GetFileName(dir), bytes, bytes / 1048576.0));
            }

            long rootFiles = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly).Sum(f => new FileInfo(f).Length);
            Debug.Log(string.Format("  {0,-28} {1,12:N0} 字节  ({2:N2} MB)", "(根目录文件: game.js/game.json 等)", rootFiles, rootFiles / 1048576.0));
            Debug.Log("判读：主包（代码包）应 ≤4MB=4194304 字节；整包 ≤30MB。");
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
