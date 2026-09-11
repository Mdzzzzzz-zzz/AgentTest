using NUnit.Framework;

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
    }
}
