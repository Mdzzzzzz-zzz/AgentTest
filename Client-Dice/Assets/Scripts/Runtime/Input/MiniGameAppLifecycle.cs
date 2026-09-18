namespace DiceDemo.M1
{
    /// <summary>
    /// 小游戏生命周期实现（空实现）。
    /// 微信/抖音等小游戏没有"主动退出到桌面"的语义：退出由平台负责。
    /// 后续若需要接入返回主菜单或平台生命周期回调，在此扩展。
    /// </summary>
    public sealed class MiniGameAppLifecycle : IAppLifecycle
    {
        public void Quit()
        {
            // 小游戏无退出语义：不执行任何操作，避免误调用 Application.Quit。
        }
    }
}
