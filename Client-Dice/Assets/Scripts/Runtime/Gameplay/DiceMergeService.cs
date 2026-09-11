using UnityEngine;

namespace DiceDemo.M1
{
    public sealed class DiceMergeService
    {
        private long _nextRecordId = 1;
        private float _lastMergeTime = -100f;
        private int _chainDepth;

        public int CurrentChainDepth => _chainDepth;

        public void Reset()
        {
            _nextRecordId = 1;
            _lastMergeTime = -100f;
            _chainDepth = 0;
        }

        public bool CanMerge(BattleDie first, BattleDie second)
        {
            if (first == null || second == null || first.IsProtected || second.IsProtected) return false;
            return BattleRules.CanMerge(first.Kind, first.Level, first.IsMergeLocked,
                second.Kind, second.Level, second.IsMergeLocked, BattleBalance.MaxDiceLevel);
        }

        public MergeRecord LockAndCreateRecord(BattleDie first, BattleDie second)
        {
            first.IsMergeLocked = true;
            second.IsMergeLocked = true;

            if (Time.time - _lastMergeTime <= BattleBalance.ChainWindow) _chainDepth++;
            else _chainDepth = 1;
            _lastMergeTime = Time.time;

            return new MergeRecord
            {
                Id = _nextRecordId++,
                Kind = first.Kind,
                SourceLevel = first.Level,
                ResultLevel = first.Level + 1,
                ChainDepth = _chainDepth,
                Time = Time.time
            };
        }
    }
}
