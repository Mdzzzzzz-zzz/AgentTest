namespace RuneDice.Game
{
    public static class FusionRules
    {
        public static bool CanFuse(DiceData left, DiceData right)
        {
            return left.Type == right.Type && left.Level == right.Level;
        }

        public static DiceData CreateResult(DiceData left, DiceData right)
        {
            if (!CanFuse(left, right))
                throw new System.ArgumentException("Dice must have the same type and level.");
            return new DiceData(left.Type, left.Level + 1);
        }
    }
}
