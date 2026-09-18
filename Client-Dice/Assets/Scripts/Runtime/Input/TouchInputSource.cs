using UnityEngine;

namespace DiceDemo.M1
{
    /// <summary>
    /// 触摸输入实现（微信/抖音等小游戏与移动端）。
    /// 每帧在 Tick() 中采样一次并缓存，指针语义与桌面保持一致；
    /// 无触摸时回退到鼠标，便于在编辑器/开发者工具里继续用鼠标调试。
    /// 注意：小游戏无物理键盘，DebugToggle/Back 返回 false，改由 UI 入口触发。
    /// </summary>
    public sealed class TouchInputSource : IInputSource
    {
        private Vector2 _position;
        private bool _down;
        private bool _held;
        private bool _up;

        public Vector2 PointerPosition => _position;
        public bool PointerDown => _down;
        public bool PointerHeld => _held;
        public bool PointerUp => _up;
        public bool DebugTogglePressed => false;
        public bool BackPressed => false;

        public void Tick()
        {
            _down = false;
            _held = false;
            _up = false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                _position = touch.position;
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _down = true;
                        _held = true;
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        _held = true;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        _up = true;
                        break;
                }
                return;
            }

            // 无触摸（编辑器 / PC 开发者工具）回退到鼠标，保证同一套逻辑可调试。
            _position = Input.mousePosition;
            _down = Input.GetMouseButtonDown(0);
            _held = Input.GetMouseButton(0);
            _up = Input.GetMouseButtonUp(0);
        }
    }
}
