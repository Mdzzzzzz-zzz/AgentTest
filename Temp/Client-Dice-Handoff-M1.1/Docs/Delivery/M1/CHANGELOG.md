# M1.0.0-开发版 变更记录

## 新增

- 正式战斗场景 Assets/Scenes/BattleM1.unity 与 Windows x64 构建。
- RoundPreparation、PlayerAim、PlayerLaunch、PhysicsResolution、PlayerEffects、EnemyActions、RoundRefresh 七阶段回合状态机，以及 Victory/Defeat 终止状态。
- 攻击、治疗、护盾三种骰子，等级 D1～D6；只有同类型、同等级且未锁定的骰子可合并。
- 唯一合并事务 ID、原子锁定、按发生顺序结算队列和 Chain 倍率。
- 玩家生命/护盾、哥布林/弓手生命、固定敌方意图及死亡后动作中断。
- 瞄准线、下一枚骰子、敌方意图、合并队列、活动骰子数、战斗日志、胜负与重新挑战 UI。
- F10 验收模式：M1-A～M1-G 固定场景、骰子生成、玩家/敌人数据、敌方意图、金币调试值、1×/2×/3×表现速度、清理调试数据及复制日志路径。
- 物理阶段 4 秒硬超时与安全冻结，防止持续滚动阻塞回合。
- 15 项 EditMode 自动测试、命令行运行时冒烟测试和一键 Windows 构建脚本。

## 修改

- M0 DiceMergeGame 的自动启动增加场景隔离，避免在 M1 战斗场景重复创建相机和物理边界。
- 构建场景切换为 BattleM1；版本显示为 M1.0.0-开发版。
- 游戏内可见内容全部改为中文，包括战斗界面、验收模式、阶段、意图、日志、胜负提示和 M0 原型界面。
- 隐藏带英文的原始 Logo，改用中文标题；Windows 包改为非开发构建，移除英文开发水印。
- 去除交付工程对缺失的本地 cn.tuanjie.ai.generators 路径包的依赖，保留 Codely Bridge。

## 删除

- 无玩法内容删除；M0 场景和脚本仍保留在工程内。
