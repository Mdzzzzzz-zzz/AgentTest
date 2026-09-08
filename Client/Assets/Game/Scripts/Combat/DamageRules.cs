namespace RuneDice.Game
{
    public static class DamageRules
    {
        public static int CalculateFusionDamage(int resultingLevel, int baseDamage = 10)
        {
            return resultingLevel < 1 || baseDamage < 0 ? 0 : baseDamage * resultingLevel;
        }
    }
}
