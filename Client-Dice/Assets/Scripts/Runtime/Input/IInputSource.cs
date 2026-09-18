using UnityEngine;

namespace DiceDemo.M1
{
    /// <summary>
    /// 平台无关输入源。把鼠标/键盘/触摸抽象为统一指针语义，
    /// 使玩法层（BattleFlow）不直接依赖 UnityEngine.Input，便于小游戏/多端适配。
    /// 坐标语义与 Input.mousePosition 一致：屏幕像素、左下角为原点。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>当前指针屏幕坐标（左下角原点，单位像素）。</summary>
        Vector2 PointerPosition { get; }

        /// <summary>本帧按下。</summary>
        bool PointerDown { get; }

        /// <summary>持续按住。</summary>
        bool PointerHeld { get; }

        /// <summary>本帧抬起。</summary>
        bool PointerUp { get; }

        /// <summary>调试/验收入口（桌面为 F10；小游戏由 UI 入口触发）。</summary>
        bool DebugTogglePressed { get; }

        /// <summary>返回/退出（桌面为 Esc；小游戏为空实现）。</summary>
        bool BackPressed { get; }

        /// <summary>
        /// 每帧采样一次。桌面输入用 UnityEngine.Input（本身按帧），
        /// 触摸输入需要在此缓存本帧的按压状态，避免多次读取属性时重复计数。
        /// </summary>
        void Tick();
    }
}
