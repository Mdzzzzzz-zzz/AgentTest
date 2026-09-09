using System.Collections;
using NUnit.Framework;
using RuneDice.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Stage2BattleFlowTests
{
    [UnityTest]
    public IEnumerator EnemyActionDamagesPlayerAndReturnsToReady()
    {
        SceneManager.LoadScene("RuneDiceDemo"); yield return null;
        var controller = Object.FindObjectOfType<GameController>(); var enemy = Object.FindObjectOfType<EnemyTarget>();
        Assert.IsNotNull(controller); Assert.IsNotNull(enemy);
        var first = Object.FindObjectOfType<DiceEntity>(); controller.LaunchDice(Vector2.zero); yield return null;
        var dice = Object.FindObjectsOfType<DiceEntity>(); var second = dice[0] == first ? dice[1] : dice[0];
        int before = controller.PlayerHealth; first.transform.position = enemy.transform.position; second.transform.position = enemy.transform.position;
        controller.ReportDiceCollision(first, second);
        yield return new WaitForSeconds(0.35f);
        Assert.AreEqual(before - 5, controller.PlayerHealth); Assert.AreEqual(BattleTurn.PlayerReady, controller.Turn);
    }

    [UnityTest]
    public IEnumerator FireAndLightningRewardsChangeNextDiceAndReturnToPlayer()
    {
        SceneManager.LoadScene("RuneDiceDemo"); yield return null;
        var controller = Object.FindObjectOfType<GameController>(); var enemy = Object.FindObjectOfType<EnemyTarget>();
        enemy.TakeDamage(100); yield return null; Assert.AreEqual(BattleTurn.Reward, controller.Turn);
        controller.ChooseReward(1); Assert.AreEqual(DiceType.Fire, controller.NextDice.Type); Assert.AreEqual(BattleTurn.PlayerReady, controller.Turn);
        controller.ResetGame(); enemy.TakeDamage(100); controller.ChooseReward(2);
        Assert.AreEqual(DiceType.Lightning, controller.NextDice.Type); Assert.AreEqual(BattleTurn.PlayerReady, controller.Turn);
    }

    [UnityTest]
    public IEnumerator DefeatAndResetRestorePlayableState()
    {
        SceneManager.LoadScene("RuneDiceDemo"); yield return null;
        var controller = Object.FindObjectOfType<GameController>();
        for (int i = 0; i < 6 && controller.Turn != BattleTurn.Defeat; i++)
        {
            controller.LaunchDice(Vector2.zero);
            yield return new WaitForSeconds(0.35f);
        }
        Assert.AreEqual(BattleTurn.Defeat, controller.Turn); controller.ResetGame(); yield return null;
        Assert.AreEqual(30, controller.PlayerHealth); Assert.AreEqual(BattleTurn.PlayerReady, controller.Turn); Assert.IsTrue(controller.CanLaunch);
    }
}
