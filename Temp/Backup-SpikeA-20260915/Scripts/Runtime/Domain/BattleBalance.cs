using UnityEngine;

namespace DiceDemo.M1
{
    public static class BattleBalance
    {
        public const string Version = "M1.1.0-三维战斗开发版";
        public const int Seed = 2048;
        public const int MaxDiceLevel = 6;
        public const int MaxBoardDice = 36;
        public const int PlayerMaxHealth = 60;
        public const float PhysicsTimeout = 4f;
        public const float MinimumPhysicsTime = 0.65f;
        public const float MergeProtectionTime = 0.10f;
        public const float ChainWindow = 1.25f;
        public const float BoardLeft = -4.75f;
        public const float BoardRight = 4.75f;
        public const float BoardBottom = 0.7f;
        public const float BoardTop = 8.2f;
        public const float BoardSurfaceY = 0.8f;
        public const float LaunchX = -2.2f;
        public const float LaunchY = 1.75f;
        public const float LaunchZ = 1.4f;
        public const float AimArcHeight = 1.1f;
    }

    public static class AimTrajectoryMath
    {
        public static bool TryCalculateLaunch(Vector3 origin, Vector3 direction, float strength, Vector3 gravity,
            out Vector3 target, out Vector3 velocity, out float flightTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            float maxDistance = MaxBoardDistance(origin, direction);
            if (maxDistance <= 0f)
            {
                target = origin;
                velocity = Vector3.zero;
                flightTime = 0f;
                return false;
            }

            target = origin + direction * (maxDistance * Mathf.Clamp01(strength));
            target.y = BattleBalance.BoardSurfaceY;

            float gravityMagnitude = Mathf.Max(0.01f, Mathf.Abs(gravity.y));
            float verticalVelocity = Mathf.Sqrt(2f * gravityMagnitude * BattleBalance.AimArcHeight);
            float deltaY = target.y - origin.y;
            float discriminant = Mathf.Max(0.01f,
                verticalVelocity * verticalVelocity - 2f * gravityMagnitude * deltaY);
            flightTime = (verticalVelocity + Mathf.Sqrt(discriminant)) / gravityMagnitude;
            Vector3 horizontalVelocity = target - origin;
            horizontalVelocity.y = 0f;
            velocity = horizontalVelocity / Mathf.Max(0.05f, flightTime);
            velocity.y = verticalVelocity;
            return true;
        }

        public static float MaxBoardDistance(Vector3 origin, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            direction.Normalize();

            float distance = float.MaxValue;
            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                float edgeX = direction.x > 0f ? BattleBalance.BoardRight : BattleBalance.BoardLeft;
                float candidate = (edgeX - origin.x) / direction.x;
                if (candidate > 0f) distance = Mathf.Min(distance, candidate);
            }
            if (Mathf.Abs(direction.z) > 0.0001f)
            {
                float edgeZ = direction.z > 0f ? BattleBalance.BoardTop : BattleBalance.BoardBottom;
                float candidate = (edgeZ - origin.z) / direction.z;
                if (candidate > 0f) distance = Mathf.Min(distance, candidate);
            }
            return distance == float.MaxValue ? 0f : distance;
        }
    }
}
