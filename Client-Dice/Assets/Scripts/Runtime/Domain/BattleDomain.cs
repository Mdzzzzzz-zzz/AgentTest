using System;
using System.Collections.Generic;

namespace DiceDemo.M1
{
    public enum BattlePhase
    {
        RoundPreparation,
        PlayerAim,
        PlayerLaunch,
        PhysicsResolution,
        PlayerEffects,
        EnemyActions,
        RoundRefresh,
        Victory,
        Defeat
    }

    public enum DiceKind
    {
        Attack,
        Heal,
        Shield
    }

    public enum EnemyKind
    {
        Goblin,
        Archer
    }

    public enum IntentKind
    {
        Wait,
        Attack
    }

    public enum AcceptanceScenario
    {
        Standard,
        Attack,
        HealCap,
        ShieldAbsorb,
        Chain,
        VictoryInterrupt,
        DefeatInterrupt,
        PhysicsTimeout
    }

    [Serializable]
    public struct DamageResult
    {
        public int Requested;
        public int ShieldLost;
        public int HealthLost;
    }

    [Serializable]
    public sealed class CombatantModel
    {
        public string Name { get; private set; }
        public int MaxHealth { get; private set; }
        public int Health { get; private set; }
        public int Shield { get; private set; }
        public bool IsAlive => Health > 0;

        public CombatantModel(string name, int maxHealth)
        {
            Reset(name, maxHealth, maxHealth, 0);
        }

        public void Reset(string name, int maxHealth, int health, int shield)
        {
            Name = name;
            MaxHealth = Math.Max(1, maxHealth);
            Health = Math.Max(0, Math.Min(health, MaxHealth));
            Shield = Math.Max(0, shield);
        }

        public DamageResult ApplyDamage(int amount)
        {
            amount = Math.Max(0, amount);
            int shieldLost = Math.Min(Shield, amount);
            Shield -= shieldLost;
            int remaining = amount - shieldLost;
            int healthLost = Math.Min(Health, remaining);
            Health -= healthLost;
            return new DamageResult { Requested = amount, ShieldLost = shieldLost, HealthLost = healthLost };
        }

        public int Heal(int amount)
        {
            int before = Health;
            Health = Math.Min(MaxHealth, Health + Math.Max(0, amount));
            return Health - before;
        }

        public int AddShield(int amount)
        {
            int gained = Math.Max(0, amount);
            Shield += gained;
            return gained;
        }

        public void ClearShield()
        {
            Shield = 0;
        }
    }

    [Serializable]
    public sealed class EnemyModel
    {
        private readonly int[] _attacks;
        private int _intentIndex;

        public EnemyKind Kind { get; private set; }
        public CombatantModel Unit { get; private set; }
        public IntentKind IntentKind { get; private set; }
        public int IntentValue { get; private set; }

        public EnemyModel(EnemyKind kind, string name, int health, int[] attacks)
        {
            Kind = kind;
            Unit = new CombatantModel(name, health);
            _attacks = attacks ?? Array.Empty<int>();
            _intentIndex = 0;
            SelectIntent();
        }

        public void ForceIntent(IntentKind kind, int value)
        {
            IntentKind = kind;
            IntentValue = Math.Max(0, value);
        }

        public void AdvanceIntent()
        {
            _intentIndex++;
            SelectIntent();
        }

        private void SelectIntent()
        {
            if (_attacks.Length == 0)
            {
                IntentKind = IntentKind.Wait;
                IntentValue = 0;
                return;
            }

            int value = _attacks[_intentIndex % _attacks.Length];
            IntentKind = value <= 0 ? IntentKind.Wait : IntentKind.Attack;
            IntentValue = Math.Max(0, value);
        }
    }

    [Serializable]
    public struct MergeRecord
    {
        public long Id;
        public DiceKind Kind;
        public int SourceLevel;
        public int ResultLevel;
        public int ChainDepth;
        public float Time;
    }

    /// <summary>
    /// Owns merge-effect delivery. A record id may be observed by physics more
    /// than once, but it can enter and leave this queue only once.
    /// </summary>
    public sealed class MergeEffectQueue
    {
        private readonly Queue<MergeRecord> _pending = new Queue<MergeRecord>();
        private readonly HashSet<long> _knownIds = new HashSet<long>();

        public int Count => _pending.Count;

        public bool EnqueueUnique(MergeRecord record)
        {
            if (record.Id <= 0 || !_knownIds.Add(record.Id)) return false;
            _pending.Enqueue(record);
            return true;
        }

        public bool TryDequeue(out MergeRecord record)
        {
            if (_pending.Count == 0)
            {
                record = default(MergeRecord);
                return false;
            }

            record = _pending.Dequeue();
            return true;
        }

        public void Clear()
        {
            _pending.Clear();
            _knownIds.Clear();
        }
    }

    public static class BattleRules
    {
        private static readonly int[] AttackValues = { 0, 4, 7, 11, 16, 22, 30 };
        private static readonly int[] HealValues = { 0, 3, 5, 8, 12, 17, 23 };
        private static readonly int[] ShieldValues = { 0, 4, 7, 10, 14, 19, 25 };

        public static bool CanMerge(DiceKind firstKind, int firstLevel, bool firstLocked,
            DiceKind secondKind, int secondLevel, bool secondLocked, int maxLevel)
        {
            return !firstLocked && !secondLocked && firstKind == secondKind &&
                   firstLevel == secondLevel && firstLevel > 0 && firstLevel < maxLevel;
        }

        public static int GetBaseEffect(DiceKind kind, int resultLevel)
        {
            int index = Math.Max(1, Math.Min(resultLevel, 6));
            switch (kind)
            {
                case DiceKind.Heal: return HealValues[index];
                case DiceKind.Shield: return ShieldValues[index];
                default: return AttackValues[index];
            }
        }

        public static int GetScaledEffect(DiceKind kind, int resultLevel, int chainDepth)
        {
            float multiplier = 1f + 0.25f * Math.Max(0, chainDepth - 1);
            multiplier = Math.Min(multiplier, 2f);
            return (int)Math.Round(GetBaseEffect(kind, resultLevel) * multiplier, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class BattlePhaseMachine
    {
        private static readonly Dictionary<BattlePhase, BattlePhase[]> Allowed =
            new Dictionary<BattlePhase, BattlePhase[]>
            {
                { BattlePhase.RoundPreparation, new[] { BattlePhase.PlayerAim, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.PlayerAim, new[] { BattlePhase.PlayerLaunch, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.PlayerLaunch, new[] { BattlePhase.PhysicsResolution, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.PhysicsResolution, new[] { BattlePhase.PlayerEffects, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.PlayerEffects, new[] { BattlePhase.EnemyActions, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.EnemyActions, new[] { BattlePhase.RoundRefresh, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.RoundRefresh, new[] { BattlePhase.RoundPreparation, BattlePhase.Victory, BattlePhase.Defeat } },
                { BattlePhase.Victory, Array.Empty<BattlePhase>() },
                { BattlePhase.Defeat, Array.Empty<BattlePhase>() }
            };

        public BattlePhase Current { get; private set; }

        public BattlePhaseMachine(BattlePhase initial)
        {
            Current = initial;
        }

        public bool CanTransition(BattlePhase target)
        {
            BattlePhase[] targets = Allowed[Current];
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == target) return true;
            }
            return false;
        }

        public bool TryTransition(BattlePhase target)
        {
            if (!CanTransition(target)) return false;
            Current = target;
            return true;
        }

        public void Reset(BattlePhase phase)
        {
            Current = phase;
        }
    }
}
