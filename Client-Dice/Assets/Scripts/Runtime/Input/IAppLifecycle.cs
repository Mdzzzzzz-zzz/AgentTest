namespace DiceDemo.M1
{
    /// <summary>
    /// 平台无关的应用生命周期抽象。把"退出/返回"从 Application.Quit 解耦，
    /// 让小游戏等无退出语义的平台可以安全空实现。
    /// </summary>
    public interface IAppLifecycle
    {
        /// <summary>请求退出应用（桌面会真正退出；小游戏为空实现）。</summary>
        void Quit();
    }
}
