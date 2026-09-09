using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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
        [SerializeField] private Text hudText;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private Text turnText;
        [SerializeField] private Text nextDiceText;
        [SerializeField] private Text enemyStatusText;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private Button[] rewardButtons;
        [SerializeField] private Button resetButton;
        [Header("Rules")]
        [SerializeField] private float fusionDamageRadius = 3f;
        [SerializeField] private int baseFusionDamage = 10;
        [SerializeField] private int playerMaxHealth = 30;
        [SerializeField] private int enemyAttack = 5;
        [SerializeField, Min(0.1f)] private float resolveDuration = 0.25f;

        private readonly List<DiceEntity> dice = new List<DiceEntity>();
        private readonly HashSet<ulong> settledPairs = new HashSet<ulong>();
        private readonly Queue<DiceData> queue = new Queue<DiceData>();
        private UnityAction[] rewardCallbacks;
        private int playerHealth;
        private int bonusDamage;
        private int chainBonus;
        private int resolveToken;
        private int enemyActToken = -1;

        public BattleTurn Turn { get; private set; }
        public int PlayerHealth => playerHealth;
        public int ActiveDiceCount => dice.Count;
        public DiceData NextDice { get; private set; }
        public bool CanLaunch => Turn == BattleTurn.PlayerReady && dicePrefab != null && launchPoint != null;
        public bool HasReward => Turn == BattleTurn.Reward;
        public int QueuedDiceCount => queue.Count;
        public int BonusDamage => bonusDamage;
        public int ChainBonus => chainBonus;

        private void Awake()
        {
            if (resetButton != null) resetButton.onClick.AddListener(ResetGame);
            if (rewardButtons != null)
            {
                rewardCallbacks = new UnityAction[rewardButtons.Length];
                for (int i = 0; i < rewardButtons.Length; i++)
                {
                    int rewardIndex = i;
                    if (rewardButtons[i] != null) { rewardCallbacks[i] = () => ChooseReward(rewardIndex); rewardButtons[i].onClick.AddListener(rewardCallbacks[i]); }
                }
            }
            if (enemy != null) { enemy.HealthChanged += OnEnemyHealthChanged; enemy.Died += OnEnemyDied; }
        }
        private void Start() => ResetGame();
        private void OnDestroy()
        {
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetGame);
            if (rewardButtons != null)
                for (int i = 0; i < rewardButtons.Length; i++)
                    if (rewardButtons[i] != null && rewardCallbacks != null && rewardCallbacks[i] != null) rewardButtons[i].onClick.RemoveListener(rewardCallbacks[i]);
            if (enemy != null) { enemy.HealthChanged -= OnEnemyHealthChanged; enemy.Died -= OnEnemyDied; }
        }

        public void ResetGame()
        {
            StopAllCoroutines();
            FusionFeedback.ClearAll();
            resolveToken++;
            enemyActToken = -1;
            Turn = BattleTurn.PlayerReady;
            settledPairs.Clear(); queue.Clear(); bonusDamage = 0; chainBonus = 0; playerHealth = playerMaxHealth;
            foreach (var item in dice) if (item != null) { item.MarkPendingRemoval(); Destroy(item.gameObject); }
            dice.Clear();
            if (enemy != null) enemy.ResetHealth();
            queue.Enqueue(new DiceData(DiceType.Basic, 1)); queue.Enqueue(new DiceData(DiceType.Basic, 1)); queue.Enqueue(new DiceData(DiceType.Fire, 1)); queue.Enqueue(new DiceData(DiceType.Lightning, 1));
            NextDice = queue.Dequeue(); SpawnDice(NextDice, new Vector2(0f, 1.1f), Vector2.zero);
            PrepareNext(); SetStatus("玩家准备：拖拽并松手投掷");
        }

        public void LaunchDice(Vector2 impulse)
        {
            if (!CanLaunch) return;
            settledPairs.Clear();
            Turn = BattleTurn.PlayerThrow;
            SpawnDice(NextDice, launchPoint.position, impulse);
            PrepareNext();
            Turn = BattleTurn.Resolve;
            int token = ++resolveToken;
            StartCoroutine(CompleteResolveAfterDelay(token));
            SetStatus("结算中：等待融合与伤害");
        }

        private IEnumerator CompleteResolveAfterDelay(int token)
        {
            yield return new WaitForSeconds(resolveDuration);
            if (token == resolveToken && Turn == BattleTurn.Resolve) EnemyActOnce(token);
        }

        public void ReportDiceCollision(DiceEntity first, DiceEntity second)
        {
            if (Turn != BattleTurn.Resolve || first == null || second == null || first == second || first.IsPendingRemoval || second.IsPendingRemoval) return;
            ulong pair = PairKey(first.GetInstanceID(), second.GetInstanceID());
            if (!settledPairs.Add(pair) || !FusionRules.CanFuse(first.Data, second.Data)) return;
            DiceData result = FusionRules.CreateResult(first.Data, second.Data);
            Vector2 position = ((Vector2)first.transform.position + (Vector2)second.transform.position) * .5f;
            first.MarkPendingRemoval(); second.MarkPendingRemoval(); dice.Remove(first); dice.Remove(second); Destroy(first.gameObject); Destroy(second.gameObject);
            SpawnDice(result, position, Vector2.up * 1.5f);
            int damage = DamageRules.CalculateDamage(result, baseFusionDamage + (result.Type == DiceType.Basic ? bonusDamage : 0));
            if (result.Type == DiceType.Lightning) damage += chainBonus;
            if (enemy != null && !enemy.IsDead && Vector2.Distance(position, enemy.transform.position) <= fusionDamageRadius) enemy.TakeDamage(damage);
            StartCoroutine(FusionFeedback.Show(position, fusionDamageRadius));
            SetStatus($"融合成功：{result.Type} 等级 {result.Level}，造成 {damage} 伤害");
        }

        private void EnemyActOnce(int token)
        {
            if (enemyActToken == token || Turn != BattleTurn.Resolve) return;
            enemyActToken = token;
            if (enemy != null && enemy.IsDead) { Turn = BattleTurn.Reward; SetStatus("胜利！请选择三选一奖励"); return; }
            EnemyAct();
        }

        private void EnemyAct()
        {
            Turn = BattleTurn.EnemyAction;
            playerHealth = Mathf.Max(0, playerHealth - enemyAttack);
            if (playerHealth == 0) { Turn = BattleTurn.Defeat; SetStatus("失败：玩家生命归零"); return; }
            Turn = BattleTurn.PlayerReady; SetStatus("玩家准备：下一枚骰子已就绪");
        }

        public void ChooseReward(int index)
        {
            if (Turn != BattleTurn.Reward || index < 0 || index >= BattleRules.Options.Length) return;
            RewardType reward = BattleRules.Options[index].Type;
            if (reward == RewardType.ExtraBasicDamage) bonusDamage += 5;
            else if (reward == RewardType.LightningChain) { chainBonus += 2; NextDice = BattleRules.ApplyReward(NextDice, reward); }
            else { NextDice = BattleRules.ApplyReward(NextDice, reward); }
            StartNextBattle("奖励已选择：" + BattleRules.Options[index].Label);
        }

        private void StartNextBattle(string message)
        {
            foreach (var item in dice) if (item != null) { item.MarkPendingRemoval(); Destroy(item.gameObject); }
            dice.Clear(); settledPairs.Clear(); resolveToken++; enemyActToken = -1;
            if (enemy != null) enemy.ResetHealth();
            Turn = BattleTurn.PlayerReady;
            SpawnDice(new DiceData(DiceType.Basic, 1), new Vector2(0f, 1.1f), Vector2.zero);
            SetStatus(message + "；下一战开始");
        }

        private void PrepareNext()
        {
            if (queue.Count == 0) queue.Enqueue(new DiceData(DiceType.Basic, 1));
            NextDice = queue.Dequeue(); UpdateHud();
        }
        private DiceEntity SpawnDice(DiceData data, Vector2 position, Vector2 impulse)
        {
            DiceEntity entity = Instantiate(dicePrefab, position, Quaternion.identity, diceRoot); entity.Initialize(this, data); dice.Add(entity);
            Rigidbody2D body = entity.GetComponent<Rigidbody2D>(); if (body != null) body.AddForce(impulse, ForceMode2D.Impulse); return entity;
        }
        private void OnEnemyHealthChanged(int health) { UpdateHud(); if (Turn != BattleTurn.Victory && health > 0) SetStatus("敌人生命：" + health); }
        private void OnEnemyDied() { Turn = BattleTurn.Reward; SetStatus("胜利！请选择三选一奖励"); }
        private void SetStatus(string message) { if (statusText != null) statusText.text = message; UpdateHud(); }
        private void UpdateHud()
        {
            int enemyHealth = enemy == null ? 0 : enemy.CurrentHealth;
            if (hudText != null) hudText.text = $"回合：{Turn}\n玩家生命：{playerHealth}\n下一骰：{NextDice.Type} Lv.{NextDice.Level}\n敌人生命：{enemyHealth}";
            if (playerHealthText != null) playerHealthText.text = $"玩家生命  {playerHealth}/{playerMaxHealth}";
            if (turnText != null) turnText.text = "回合  " + Turn;
            if (nextDiceText != null) nextDiceText.text = $"下一骰  {NextDice.Type}  Lv.{NextDice.Level}";
            if (enemyStatusText != null) enemyStatusText.text = enemy != null && enemy.IsDead ? "敌人  已击败" : "敌人生命  " + enemyHealth;
            if (rewardPanel != null) rewardPanel.SetActive(Turn == BattleTurn.Reward);
        }
        private static ulong PairKey(int a, int b) { uint low = (uint)Mathf.Min(a, b); uint high = (uint)Mathf.Max(a, b); return ((ulong)low << 32) | high; }
    }
}
