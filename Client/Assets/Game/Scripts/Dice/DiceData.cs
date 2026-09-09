namespace RuneDice.Game
{
    public enum DiceType { Basic, Fire, Lightning, Ember = Basic }

    public readonly struct DiceData
    {
        public DiceType Type { get; }
        public int Level { get; }

        public DiceData(DiceType type, int level)
        {
            Type = type;
            Level = level;
        }
    }
}
