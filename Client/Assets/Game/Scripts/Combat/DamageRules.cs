namespace RuneDice.Game
{
    public static class DamageRules
    {
        public static int CalculateFusionDamage(int resultingLevel, int baseDamage = 10)
        {
            return resultingLevel < 1 || baseDamage < 0 ? 0 : baseDamage * resultingLevel;
        }

        public static int CalculateDamage(DiceData dice, int baseDamage = 10)
        {
            int scaled = CalculateFusionDamage(dice.Level, baseDamage);
            if (dice.Type == DiceType.Fire) return scaled + dice.Level * 2;
            if (dice.Type == DiceType.Lightning) return scaled + dice.Level;
            return scaled;
        }

        public static int CalculateChainDamage(DiceData dice, int targetsHit, int baseDamage = 10)
        {
            if (targetsHit <= 0) return 0;
            return CalculateDamage(dice, baseDamage) + (targetsHit - 1) * dice.Level;
        }
    }
}
