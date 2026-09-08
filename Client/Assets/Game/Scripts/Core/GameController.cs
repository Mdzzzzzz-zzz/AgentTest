using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuneDice.Game
{
    public sealed class GameController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private DiceEntity dicePrefab;
        [SerializeField] private Transform diceRoot;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private EnemyTarget enemy;
        [SerializeField] private Text statusText;
        [SerializeField] private Button resetButton;
        [Header("Rules")]
        [SerializeField] private float fusionDamageRadius = 3f;
        [SerializeField] private int baseFusionDamage = 10;

        private readonly List<DiceEntity> dice = new List<DiceEntity>();
        private readonly HashSet<ulong> settledPairs = new HashSet<ulong>();
        private bool won;

        public bool CanLaunch => !won && dicePrefab != null && launchPoint != null;
        public int ActiveDiceCount => dice.Count;

        private void Awake()
        {
            if (resetButton != null) resetButton.onClick.AddListener(ResetGame);
            if (enemy != null)
            {
                enemy.HealthChanged += OnEnemyHealthChanged;
                enemy.Died += OnEnemyDied;
            }
        }

        private void Start() => ResetGame();

        private void OnDestroy()
        {
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetGame);
            if (enemy != null)
            {
                enemy.HealthChanged -= OnEnemyHealthChanged;
                enemy.Died -= OnEnemyDied;
            }
        }

        public void ResetGame()
        {
            won = false;
            settledPairs.Clear();
            foreach (var item in dice)
            {
                if (item == null) continue;
                item.MarkPendingRemoval();
                Destroy(item.gameObject);
            }
            dice.Clear();
            enemy.ResetHealth();
            SpawnDice(new DiceData(DiceType.Ember, 1), new Vector2(0f, 1.1f), Vector2.zero);
            SetStatus("拖拽并松手发射同类骰子");
        }

        public void LaunchDice(Vector2 impulse)
        {
            if (!CanLaunch) return;
            SpawnDice(new DiceData(DiceType.Ember, 1), launchPoint.position, impulse);
        }

        public void ReportDiceCollision(DiceEntity first, DiceEntity second)
        {
            if (won || first == null || second == null || first == second || first.IsPendingRemoval || second.IsPendingRemoval) return;
            ulong pair = PairKey(first.GetInstanceID(), second.GetInstanceID());
            if (!settledPairs.Add(pair) || !FusionRules.CanFuse(first.Data, second.Data)) return;

            DiceData result = FusionRules.CreateResult(first.Data, second.Data);
            Vector2 position = ((Vector2)first.transform.position + (Vector2)second.transform.position) * .5f;
            first.MarkPendingRemoval();
            second.MarkPendingRemoval();
            dice.Remove(first);
            dice.Remove(second);
            Destroy(first.gameObject);
            Destroy(second.gameObject);

            SpawnDice(result, position, Vector2.up * 1.5f);
            ApplyFusionDamage(position, result.Level);
            SetStatus($"融合成功：等级 {result.Level}");
        }

        private DiceEntity SpawnDice(DiceData data, Vector2 position, Vector2 impulse)
        {
            DiceEntity entity = Instantiate(dicePrefab, position, Quaternion.identity, diceRoot);
            entity.Initialize(this, data);
            dice.Add(entity);
            entity.GetComponent<Rigidbody2D>().AddForce(impulse, ForceMode2D.Impulse);
            return entity;
        }

        private void ApplyFusionDamage(Vector2 center, int resultingLevel)
        {
            int damage = DamageRules.CalculateFusionDamage(resultingLevel, baseFusionDamage);
            if (enemy != null && !enemy.IsDead && Vector2.Distance(center, enemy.transform.position) <= fusionDamageRadius)
                enemy.TakeDamage(damage);
            StartCoroutine(FusionFeedback.Show(center, fusionDamageRadius));
        }

        private void OnEnemyHealthChanged(int health)
        {
            if (!won) SetStatus($"敌人生命：{health}");
        }

        private void OnEnemyDied()
        {
            won = true;
            SetStatus("胜利！点击重置再次挑战");
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private static ulong PairKey(int a, int b)
        {
            uint low = (uint)Mathf.Min(a, b);
            uint high = (uint)Mathf.Max(a, b);
            return ((ulong)low << 32) | high;
        }
    }
}
