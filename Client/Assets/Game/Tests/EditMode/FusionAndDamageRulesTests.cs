using NUnit.Framework;
using RuneDice.Game;

public sealed class FusionAndDamageRulesTests
{
    [Test]
    public void SameTypeAndLevel_CanFuseAndCreatesNextLevel()
    {
        var first = new DiceData(DiceType.Ember, 2);
        var second = new DiceData(DiceType.Ember, 2);
        Assert.IsTrue(FusionRules.CanFuse(first, second));
        Assert.AreEqual(3, FusionRules.CreateResult(first, second).Level);
    }

    [Test]
    public void DifferentType_CannotFuse()
    {
        Assert.IsFalse(FusionRules.CanFuse(new DiceData(DiceType.Ember, 1), new DiceData(DiceType.Fire, 1)));
    }

    [Test]
    public void DifferentLevel_CannotFuse()
    {
        Assert.IsFalse(FusionRules.CanFuse(new DiceData(DiceType.Ember, 1), new DiceData(DiceType.Ember, 2)));
    }

    [TestCase(1, 10)]
    [TestCase(2, 20)]
    [TestCase(4, 40)]
    [TestCase(0, 0)]
    public void FusionDamage_ScalesWithResultLevel(int level, int expected)
    {
        Assert.AreEqual(expected, DamageRules.CalculateFusionDamage(level));
    }
}
