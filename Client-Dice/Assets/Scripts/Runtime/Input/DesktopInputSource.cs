using UnityEngine;

namespace DiceDemo.M1
{
    /// <summary>
    /// 桌面/编辑器输入实现：直接转发 UnityEngine.Input 的鼠标与键盘。
    /// 适用于 Windows 标准独立构建与编辑器内调试。
    /// </summary>
    public sealed class DesktopInputSource : IInputSource
    {
        public Vector2 PointerPosition => Input.mousePosition;
        public bool PointerDown => Input.GetMouseButtonDown(0);
        public bool PointerHeld => Input.GetMouseButton(0);
        public bool PointerUp => Input.GetMouseButtonUp(0);
        public bool DebugTogglePressed => Input.GetKeyDown(KeyCode.F10);
        public bool BackPressed => Input.GetKeyDown(KeyCode.Escape);

        // 桌面输入本身按帧读取，无需额外缓存。
        public void Tick() { }
    }
}
