using NUnit.Framework;
using UnityEngine;

namespace DiceDemo.M1.Tests
{
    public sealed class BattleDomainTests
    {
        [Test]
        public void Damage_ConsumesShieldBeforeHealth()
        {
            CombatantModel player = new CombatantModel("Player", 60);
            player.AddShield(7);
            DamageResult result = player.ApplyDamage(8);
            Assert.AreEqual(7, result.ShieldLost);
            Assert.AreEqual(1, result.HealthLost);
            Assert.AreEqual(59, player.Health);
            Assert.AreEqual(0, player.Shield);
        }

        [Test]
        public void Heal_IsClampedToMaximumHealth()
        {
            CombatantModel player = new CombatantModel("Player", 60);
            player.ApplyDamage(5);
            Assert.AreEqual(5, player.Heal(99));
            Assert.AreEqual(60, player.Health);
        }

        [TestCase(DiceKind.Attack, DiceKind.Attack, 2, 2, true)]
        [TestCase(DiceKind.Attack, DiceKind.Heal, 2, 2, false)]
        [TestCase(DiceKind.Shield, DiceKind.Shield, 1, 2, false)]
        [TestCase(DiceKind.Heal, DiceKind.Heal, 6, 6, false)]
        public void MergeRule_RequiresSameKindAndLevelBelowCap(DiceKind a, DiceKind b, int levelA, int levelB, bool expected)
        {
            Assert.AreEqual(expected, BattleRules.CanMerge(a, levelA, false, b, levelB, false, 6));
        }

        [Test]
        public void MergeRule_RejectsLockedDice()
        {
            Assert.IsFalse(BattleRules.CanMerge(DiceKind.Attack, 1, true, DiceKind.Attack, 1, false, 6));
        }

        [Test]
        public void M1HealScenario_UsesSourceD2ValueOfFive()
        {
            Assert.AreEqual(5, BattleRules.GetScaledEffect(DiceKind.Heal, 2, 1));
        }

        [Test]
        public void ChainMultiplier_IsCappedAtTwoTimes()
        {
            Assert.AreEqual(8, BattleRules.GetScaledEffect(DiceKind.Attack, 1, 99));
        }

        [Test]
        public void EffectQueue_DeduplicatesAndSettlesOneThousandTransactions()
        {
            MergeEffectQueue queue = new MergeEffectQueue();
            for (int i = 1; i <= 1000; i++)
            {
                MergeRecord record = new MergeRecord { Id = i, Kind = DiceKind.Attack, SourceLevel = 1, ResultLevel = 2 };
                Assert.IsTrue(queue.EnqueueUnique(record));
                Assert.IsFalse(queue.EnqueueUnique(record), "Duplicate transaction was accepted: " + i);
            }

            Assert.AreEqual(1000, queue.Count);
            bool[] seen = new bool[1001];
            MergeRecord dequeued;
            int count = 0;
            while (queue.TryDequeue(out dequeued))
            {
                Assert.IsFalse(seen[dequeued.Id]);
                seen[dequeued.Id] = true;
                count++;
            }
            Assert.AreEqual(1000, count);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void PhaseMachine_AllowsSevenStageRoundLoop()
        {
            BattlePhaseMachine machine = new BattlePhaseMachine(BattlePhase.RoundPreparation);
            BattlePhase[] sequence =
            {
                BattlePhase.PlayerAim, BattlePhase.PlayerLaunch, BattlePhase.PhysicsResolution,
                BattlePhase.PlayerEffects, BattlePhase.EnemyActions, BattlePhase.RoundRefresh,
                BattlePhase.RoundPreparation
            };
            for (int i = 0; i < sequence.Length; i++) Assert.IsTrue(machine.TryTransition(sequence[i]), "Rejected phase " + sequence[i]);
        }

        [Test]
        public void PhaseMachine_RejectsSkippedStage()
        {
            BattlePhaseMachine machine = new BattlePhaseMachine(BattlePhase.RoundPreparation);
            Assert.IsFalse(machine.TryTransition(BattlePhase.EnemyActions));
            Assert.AreEqual(BattlePhase.RoundPreparation, machine.Current);
        }

        [TestCase(BattlePhase.Victory)]
        [TestCase(BattlePhase.Defeat)]
        public void TerminalPhase_CancelsFurtherTransitions(BattlePhase terminal)
        {
            BattlePhaseMachine machine = new BattlePhaseMachine(BattlePhase.PlayerEffects);
            Assert.IsTrue(machine.TryTransition(terminal));
            Assert.IsFalse(machine.TryTransition(BattlePhase.EnemyActions));
        }

        [Test]
        public void EnemyIntentPattern_IsDeterministic()
        {
            EnemyModel enemy = new EnemyModel(EnemyKind.Goblin, "Goblin", 30, new[] { 8, 10, 0 });
            Assert.AreEqual(8, enemy.IntentValue);
            enemy.AdvanceIntent();
            Assert.AreEqual(10, enemy.IntentValue);
            enemy.AdvanceIntent();
            Assert.AreEqual(IntentKind.Wait, enemy.IntentKind);
            enemy.AdvanceIntent();
            Assert.AreEqual(8, enemy.IntentValue);
        }

        [TestCase(1f, 0f, 0f, 6.95f)]
        [TestCase(-1f, 0f, 0f, 2.55f)]
        [TestCase(0f, 0f, 1f, 6.8f)]
        [TestCase(0f, 0f, -1f, 0.7f)]
        public void AimTrajectory_FullDragReachesFirstBoardEdge(float directionX, float directionY, float directionZ, float expectedDistance)
        {
            Vector3 origin = new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, BattleBalance.LaunchZ);
            float distance = AimTrajectoryMath.MaxBoardDistance(origin, new Vector3(directionX, directionY, directionZ));
            Assert.AreEqual(expectedDistance, distance, 0.001f);
        }

        [Test]
        public void AimTrajectory_SolvedPathEndsAtTargetAndRisesAboveLaunch()
        {
            Vector3 origin = new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, BattleBalance.LaunchZ);
            Vector3 target, velocity;
            float flightTime;
            Assert.IsTrue(AimTrajectoryMath.TryCalculateLaunch(origin, Vector3.forward, 1f,
                new Vector3(0f, -30f, 0f), out target, out velocity, out flightTime));

            Vector3 end = origin + velocity * flightTime + 0.5f * new Vector3(0f, -30f, 0f) * flightTime * flightTime;
            Assert.AreEqual(target.x, end.x, 0.001f);
            Assert.AreEqual(target.y, end.y, 0.001f);
            Assert.AreEqual(target.z, end.z, 0.001f);
            Assert.Greater(origin.y + velocity.y * (velocity.y / 30f) - 0.5f * 30f * (velocity.y / 30f) * (velocity.y / 30f), origin.y);
        }

        [Test]
        public void AimTrajectory_StrengthScalesDistanceLinearly()
        {
            Vector3 origin = new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, BattleBalance.LaunchZ);
            Vector3 halfTarget, halfVelocity;
            float halfTime;
            Vector3 fullTarget, fullVelocity;
            float fullTime;
            Assert.IsTrue(AimTrajectoryMath.TryCalculateLaunch(origin, Vector3.right, 0.5f,
                new Vector3(0f, -30f, 0f), out halfTarget, out halfVelocity, out halfTime));
            Assert.IsTrue(AimTrajectoryMath.TryCalculateLaunch(origin, Vector3.right, 1f,
                new Vector3(0f, -30f, 0f), out fullTarget, out fullVelocity, out fullTime));
            Assert.AreEqual((origin.x + fullTarget.x) * 0.5f, halfTarget.x, 0.001f);
            Assert.AreEqual((origin.z + fullTarget.z) * 0.5f, halfTarget.z, 0.001f);
        }
    }
}
