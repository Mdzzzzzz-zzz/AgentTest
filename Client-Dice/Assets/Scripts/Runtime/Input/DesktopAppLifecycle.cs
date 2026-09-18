using UnityEngine;

namespace DiceDemo.M1
{
    /// <summary>桌面/编辑器生命周期实现：调用 Application.Quit 真正退出。</summary>
    public sealed class DesktopAppLifecycle : IAppLifecycle
    {
        public void Quit() => Application.Quit();
    }
}
