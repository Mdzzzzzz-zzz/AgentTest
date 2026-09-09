using System;

namespace RuneDice.Game
{
    public enum BattleTurn { PlayerReady, PlayerThrow, Resolve, EnemyAction, Reward, Victory, Defeat }
    public enum RewardType { ExtraBasicDamage, FireQueue, LightningChain }

    public readonly struct RewardOption
    {
        public RewardType Type { get; }
        public string Label { get; }
        public RewardOption(RewardType type, string label) { Type = type; Label = label; }
    }

    public static class BattleRules
    {
        public static readonly RewardOption[] Options = {
            new RewardOption(RewardType.ExtraBasicDamage, "强化基础骰：伤害 +5"),
            new RewardOption(RewardType.FireQueue, "火焰骰：加入下一枚队列"),
            new RewardOption(RewardType.LightningChain, "闪电链：链伤 +2")
        };

        public static BattleTurn NextAfterResolve(bool enemyDead, bool playerDead)
        {
            if (enemyDead) return BattleTurn.Reward;
            if (playerDead) return BattleTurn.Defeat;
            return BattleTurn.EnemyAction;
        }

        public static DiceData ApplyReward(DiceData next, RewardType reward)
        {
            if (reward == RewardType.FireQueue) return new DiceData(DiceType.Fire, next.Level);
            if (reward == RewardType.LightningChain) return new DiceData(DiceType.Lightning, next.Level);
            return next;
        }
    }
}
