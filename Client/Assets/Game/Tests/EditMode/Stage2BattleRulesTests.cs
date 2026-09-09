using NUnit.Framework;
using RuneDice.Game;

public sealed class Stage2BattleRulesTests
{
    [Test]
    public void ThreeDiceHaveDistinctRules()
    {
        Assert.AreEqual(20, DamageRules.CalculateDamage(new DiceData(DiceType.Basic, 2)));
        Assert.AreEqual(24, DamageRules.CalculateDamage(new DiceData(DiceType.Fire, 2)));
        Assert.AreEqual(22, DamageRules.CalculateDamage(new DiceData(DiceType.Lightning, 2)));
        Assert.AreEqual(24, DamageRules.CalculateChainDamage(new DiceData(DiceType.Lightning, 2), 2));
    }

    [Test]
    public void ResolveMovesToEnemyOrRewardOrDefeat()
    {
        Assert.AreEqual(BattleTurn.EnemyAction, BattleRules.NextAfterResolve(false, false));
        Assert.AreEqual(BattleTurn.Reward, BattleRules.NextAfterResolve(true, false));
        Assert.AreEqual(BattleTurn.Defeat, BattleRules.NextAfterResolve(false, true));
    }

    [Test]
    public void RewardsChangeQueueDiceType()
    {
        var next = new DiceData(DiceType.Basic, 1);
        Assert.AreEqual(DiceType.Fire, BattleRules.ApplyReward(next, RewardType.FireQueue).Type);
        Assert.AreEqual(DiceType.Lightning, BattleRules.ApplyReward(next, RewardType.LightningChain).Type);
        Assert.AreEqual(DiceType.Basic, BattleRules.ApplyReward(next, RewardType.ExtraBasicDamage).Type);
    }
}
