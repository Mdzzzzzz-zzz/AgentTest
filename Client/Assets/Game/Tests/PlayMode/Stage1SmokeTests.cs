using System.Collections;
using NUnit.Framework;
using RuneDice.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Stage1SmokeTests
{
    [UnityTest]
    public IEnumerator SceneLoadsAndCanResetTwentyTimesWithoutAccumulation()
    {
        SceneManager.LoadScene("RuneDiceDemo");
        yield return null;
        var controller = Object.FindObjectOfType<GameController>();
        Assert.IsNotNull(controller);
        for (int i = 0; i < 20; i++)
        {
            controller.ResetGame();
            yield return null;
            Assert.AreEqual(1, controller.ActiveDiceCount);
        }
        Assert.IsNotNull(Object.FindObjectOfType<EnemyTarget>());
    }

    [UnityTest]
    public IEnumerator TwoMatchingDiceFuseOnceAndIncreaseLevel()
    {
        SceneManager.LoadScene("RuneDiceDemo");
        yield return null;
        var controller = Object.FindObjectOfType<GameController>();
        Assert.IsNotNull(controller);
        var first = Object.FindObjectOfType<DiceEntity>();
        controller.LaunchDice(Vector2.zero);
        yield return null;
        var dice = Object.FindObjectsOfType<DiceEntity>();
        Assert.AreEqual(2, dice.Length);
        var second = dice[0] == first ? dice[1] : dice[0];
        controller.ReportDiceCollision(first, second);
        controller.ReportDiceCollision(first, second);
        yield return null;
        dice = Object.FindObjectsOfType<DiceEntity>();
        Assert.AreEqual(1, dice.Length);
        Assert.AreEqual(2, dice[0].Data.Level);
        Assert.AreEqual(1, controller.ActiveDiceCount);
    }

    [UnityTest]
    public IEnumerator FusionDamageCanWinAndBlocksLaunchUntilReset()
    {
        SceneManager.LoadScene("RuneDiceDemo");
        yield return null;
        var controller = Object.FindObjectOfType<GameController>();
        Assert.IsNotNull(controller);
        var enemy = Object.FindObjectOfType<EnemyTarget>();
        enemy.TakeDamage(10); // leave exactly one level-2 fusion hit (20) to finish in range
        var first = Object.FindObjectOfType<DiceEntity>();
        controller.LaunchDice(Vector2.zero);
        yield return null;
        var dice = Object.FindObjectsOfType<DiceEntity>();
        var second = dice[0] == first ? dice[1] : dice[0];
        first.transform.position = enemy.transform.position;
        second.transform.position = enemy.transform.position;
        controller.ReportDiceCollision(first, second);
        yield return null;
        Assert.IsTrue(enemy.IsDead);
        Assert.IsFalse(controller.CanLaunch);
        controller.LaunchDice(Vector2.right * 10f);
        yield return null;
        Assert.AreEqual(1, controller.ActiveDiceCount);
        controller.ResetGame();
        yield return null;
        Assert.IsTrue(controller.CanLaunch);
        Assert.AreEqual(1, controller.ActiveDiceCount);
        Assert.IsFalse(enemy.IsDead);
    }
}
