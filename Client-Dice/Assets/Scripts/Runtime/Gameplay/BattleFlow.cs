using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiceDemo.M1
{
    public sealed class BattleFlow : MonoBehaviour
    {
        private const string BattleSceneName = "BattleM1";
        private readonly DiceMergeService _mergeService = new DiceMergeService();
        private readonly MergeEffectQueue _effectQueue = new MergeEffectQueue();
        private readonly List<EnemyModel> _enemies = new List<EnemyModel>();
        private readonly List<SpriteRenderer> _enemyViews = new List<SpriteRenderer>();
        private readonly List<string> _battleLog = new List<string>();
        private readonly Sprite[] _diceSprites = new Sprite[3];

        private Camera _camera;
        private CombatantModel _player;
        private BattlePhaseMachine _phase;
        private System.Random _random;
        private Texture2D _background;
        private Texture2D _logo;
        private Sprite _playerSprite;
        private Sprite _goblinSprite;
        private Sprite _archerSprite;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _selectedButtonTexture;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private Coroutine _roundRoutine;
        private AcceptanceScenario _scenario = AcceptanceScenario.Standard;
        private DiceKind _nextKind = DiceKind.Attack;
        private int _nextLevel = 1;
        private int _round;
        private bool _isAiming;
        private Vector2 _aimWorld;
        private float _physicsStartedAt;
        private float _allSleepingSince = -1f;
        private float _presentationSpeed = 1f;
        private bool _debugOpen;
        private bool _physicsTimedOut;
        private int _debugGold;
        private string _resultMessage = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (SceneManager.GetActiveScene().name == BattleSceneName && FindObjectOfType<BattleFlow>() == null)
            {
                new GameObject("M1 Battle Flow").AddComponent<BattleFlow>();
            }
        }

        public BattlePhase CurrentPhase => _phase == null ? BattlePhase.RoundPreparation : _phase.Current;
        public int PendingEffects => _effectQueue.Count;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Physics2D.gravity = new Vector2(0f, -3.7f);
            LoadArt();
            CreateWorld();
            LoadScenario(AcceptanceScenario.Standard);
            if (HasCommandLineArgument("-m1SmokeTest")) StartCoroutine(RunCommandLineSmokeTest());
            else if (HasCommandLineArgument("-m1Capture")) StartCoroutine(RunCommandLineCapture());
        }

        private void LoadArt()
        {
            _background = Resources.Load<Texture2D>("DiceDemo/Main_Menu_Background_01");
            _logo = Resources.Load<Texture2D>("DiceDemo/Rune_Dice_Logo_01");
            _diceSprites[(int)DiceKind.Attack] = LoadSprite("BattleM1/Dice_Full_Warrior_Fury");
            _diceSprites[(int)DiceKind.Heal] = LoadSprite("BattleM1/Dice_Full_Mage_Healing");
            _diceSprites[(int)DiceKind.Shield] = LoadSprite("BattleM1/Dice_Full_Warrior_Shield");
            Sprite fallback = LoadSprite("DiceDemo/Dice_Full_1");
            for (int i = 0; i < _diceSprites.Length; i++) if (_diceSprites[i] == null) _diceSprites[i] = fallback;
            _playerSprite = LoadSprite("BattleM1/1_archer_64x64_idle_1", 64f);
            _goblinSprite = LoadSprite("BattleM1/goblinwarrior_64x64_idle_1", 64f);
            _archerSprite = LoadSprite("BattleM1/goblinarcher_64x64_idle_2", 64f);
        }

        private static Sprite LoadSprite(string path, float pixelsPerUnit = 100f)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private void CreateWorld()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5.25f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.02f, 0.035f, 0.06f);

            if (_background != null)
            {
                Sprite sprite = Sprite.Create(_background, new Rect(0, 0, _background.width, _background.height), new Vector2(0.5f, 0.5f), 100f);
                GameObject backgroundObject = new GameObject("Battle Background");
                SpriteRenderer renderer = backgroundObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -100;
                renderer.color = new Color(0.42f, 0.52f, 0.64f, 1f);
                float worldHeight = _camera.orthographicSize * 2f;
                float worldWidth = worldHeight * (16f / 9f);
                backgroundObject.transform.localScale = Vector3.one * Mathf.Max(worldWidth / sprite.bounds.size.x, worldHeight / sprite.bounds.size.y);
            }

            CreateWall("Floor", new Vector2(-2.25f, BattleBalance.BoardBottom - 0.2f), new Vector2(12.4f, 0.4f));
            CreateWall("Ceiling", new Vector2(-2.25f, BattleBalance.BoardTop + 0.2f), new Vector2(12.4f, 0.4f));
            CreateWall("Left Wall", new Vector2(BattleBalance.BoardLeft - 0.2f, -0.15f), new Vector2(0.4f, 8.2f));
            CreateWall("Right Wall", new Vector2(BattleBalance.BoardRight + 0.2f, -0.15f), new Vector2(0.4f, 8.2f));
        }

        private static void CreateWall(string name, Vector2 position, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = size;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10)) _debugOpen = !_debugOpen;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_debugOpen) _debugOpen = false;
                else Application.Quit();
            }

            UpdateAimInput();
        }

        private void UpdateAimInput()
        {
            if (_phase == null || _phase.Current != BattlePhase.PlayerAim || IsPointerOverGui()) return;
            Vector2 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0) && IsInsideBoard(world))
            {
                _isAiming = true;
                _aimWorld = world;
            }
            if (_isAiming && Input.GetMouseButton(0)) _aimWorld = world;
            if (_isAiming && Input.GetMouseButtonUp(0))
            {
                _isAiming = false;
                LaunchToward(world);
            }
        }

        private bool IsPointerOverGui()
        {
            float yFromTop = Screen.height - Input.mousePosition.y;
            return yFromTop < 138f || Input.mousePosition.x > Screen.width * 0.78f || (_debugOpen && Input.mousePosition.x > Screen.width * 0.57f);
        }

        private static bool IsInsideBoard(Vector2 p)
        {
            return p.x >= BattleBalance.BoardLeft && p.x <= BattleBalance.BoardRight && p.y >= BattleBalance.BoardBottom && p.y <= BattleBalance.BoardTop;
        }

        private void LaunchToward(Vector2 target)
        {
            if (!_phase.TryTransition(BattlePhase.PlayerLaunch)) return;
            Vector2 origin = new Vector2(BattleBalance.LaunchX, BattleBalance.LaunchY);
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude < 0.2f) delta = Vector2.up;
            float strength = Mathf.Clamp(delta.magnitude * 1.7f, 5.5f, 13.5f);
            BattleDie die = SpawnDie(_nextKind, _nextLevel, origin, true);
            die.Body.velocity = delta.normalized * strength;
            die.Body.angularVelocity = (float)(_random.NextDouble() * 280.0 - 140.0);
            AddLog("已投掷：" + KindShort(_nextKind) + " D" + _nextLevel);
            RollNextDie();
            _phase.TryTransition(BattlePhase.PhysicsResolution);
            _physicsStartedAt = Time.time;
            _allSleepingSince = -1f;
            _physicsTimedOut = false;
            _roundRoutine = StartCoroutine(ResolveRound());
        }

        private IEnumerator ResolveRound()
        {
            while (_phase.Current == BattlePhase.PhysicsResolution)
            {
                float elapsed = Time.time - _physicsStartedAt;
                bool settled = elapsed >= BattleBalance.MinimumPhysicsTime && AllDiceSleeping();
                if (settled)
                {
                    if (_allSleepingSince < 0f) _allSleepingSince = Time.time;
                    if (Time.time - _allSleepingSince >= 0.35f) break;
                }
                else _allSleepingSince = -1f;

                if (elapsed >= BattleBalance.PhysicsTimeout)
                {
                    _physicsTimedOut = true;
                    FreezeAllDice();
                    AddLog("物理超时：已强制安全收尾");
                    break;
                }
                yield return null;
            }

            if (!_phase.TryTransition(BattlePhase.PlayerEffects)) yield break;
            yield return ExecutePlayerEffects();
            if (TryFinishBattle()) yield break;
            if (!_phase.TryTransition(BattlePhase.EnemyActions)) yield break;
            yield return ExecuteEnemyActions();
            if (TryFinishBattle()) yield break;
            if (!_phase.TryTransition(BattlePhase.RoundRefresh)) yield break;
            yield return PresentationWait(0.35f);
            if (!_phase.TryTransition(BattlePhase.RoundPreparation)) yield break;
            BeginRound();
        }

        private IEnumerator ExecutePlayerEffects()
        {
            MergeRecord record;
            while (_effectQueue.TryDequeue(out record))
            {
                int amount = BattleRules.GetScaledEffect(record.Kind, record.SourceLevel, record.ChainDepth);
                if (record.Kind == DiceKind.Attack)
                {
                    EnemyModel target = FirstLivingEnemy();
                    if (target == null) yield break;
                    DamageResult result = target.Unit.ApplyDamage(amount);
                    AddLog("连锁 " + record.ChainDepth + "：攻击 " + amount + " → " + target.Unit.Name + "（生命 -" + result.HealthLost + "）");
                    RefreshEnemyViews();
                }
                else if (record.Kind == DiceKind.Heal)
                {
                    int healed = _player.Heal(amount);
                    AddLog("连锁 " + record.ChainDepth + "：治疗 +" + healed + "（骰面 " + amount + "）");
                }
                else
                {
                    int shield = _player.AddShield(amount);
                    AddLog("连锁 " + record.ChainDepth + "：护盾 +" + shield);
                }

                if (TryFinishBattle()) yield break;
                yield return PresentationWait(0.45f);
            }
        }

        private IEnumerator ExecuteEnemyActions()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyModel enemy = _enemies[i];
                if (!enemy.Unit.IsAlive) continue;
                if (enemy.IntentKind == IntentKind.Attack)
                {
                    DamageResult result = _player.ApplyDamage(enemy.IntentValue);
                    AddLog(enemy.Unit.Name + "攻击 " + enemy.IntentValue + "（护盾 -" + result.ShieldLost + "，生命 -" + result.HealthLost + "）");
                }
                else AddLog(enemy.Unit.Name + "等待");
                enemy.AdvanceIntent();
                if (TryFinishBattle()) yield break;
                yield return PresentationWait(0.45f);
            }
        }

        private void BeginRound()
        {
            _round++;
            _player.ClearShield();
            AddLog("第 " + _round + " 回合：瞄准并松开投掷");
            StartCoroutine(BeginAimAfterDelay());
        }

        private IEnumerator BeginAimAfterDelay()
        {
            yield return PresentationWait(0.2f);
            if (_phase.Current == BattlePhase.RoundPreparation) _phase.TryTransition(BattlePhase.PlayerAim);
        }

        private IEnumerator PresentationWait(float seconds)
        {
            float until = Time.unscaledTime + seconds / Mathf.Max(1f, _presentationSpeed);
            while (Time.unscaledTime < until) yield return null;
        }

        private IEnumerator RunCommandLineSmokeTest()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_phase.Current != BattlePhase.PlayerAim && Time.realtimeSinceStartup < deadline) yield return null;
            if (_phase.Current != BattlePhase.PlayerAim)
            {
                Debug.LogError("M1_SMOKE_FAILED did not reach PlayerAim");
                Application.Quit(2);
                yield break;
            }

            LaunchToward(new Vector2(-1.2f, 2.2f));
            deadline = Time.realtimeSinceStartup + 8f;
            while (_round < 2 && _phase.Current != BattlePhase.Victory && _phase.Current != BattlePhase.Defeat && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool passed = _round >= 2 || _phase.Current == BattlePhase.Victory || _phase.Current == BattlePhase.Defeat;
            if (passed)
            {
                Debug.Log("M1_SMOKE_OK phase=" + _phase.Current + " round=" + _round + " playerHP=" + _player.Health + " enemies=" + _enemies.Count);
                Application.Quit(0);
            }
            else
            {
                Debug.LogError("M1_SMOKE_FAILED round did not resolve; phase=" + _phase.Current);
                Application.Quit(3);
            }
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private IEnumerator RunCommandLineCapture()
        {
            _debugOpen = true;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_phase.Current != BattlePhase.PlayerAim && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            string path = GetCommandLineValue("-m1CapturePath");
            if (string.IsNullOrWhiteSpace(path)) path = System.IO.Path.Combine(Application.persistentDataPath, "M1-preview.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("M1_CAPTURE_OK path=" + path);
            Application.Quit(0);
        }

        private static string GetCommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private bool AllDiceSleeping()
        {
            BattleDie[] dice = FindObjectsOfType<BattleDie>();
            for (int i = 0; i < dice.Length; i++)
            {
                Rigidbody2D body = dice[i].Body;
                if (body != null && (body.velocity.sqrMagnitude > 0.035f || Mathf.Abs(body.angularVelocity) > 4f)) return false;
            }
            return true;
        }

        private static void FreezeAllDice()
        {
            BattleDie[] dice = FindObjectsOfType<BattleDie>();
            for (int i = 0; i < dice.Length; i++)
            {
                dice[i].KeepMovingForAcceptance = false;
                if (dice[i].Body == null) continue;
                dice[i].Body.velocity = Vector2.zero;
                dice[i].Body.angularVelocity = 0f;
                dice[i].Body.Sleep();
            }
        }

        private bool TryFinishBattle()
        {
            if (FirstLivingEnemy() == null)
            {
                if (_phase.Current != BattlePhase.Victory) ForceTerminal(BattlePhase.Victory, "胜利");
                return true;
            }
            if (!_player.IsAlive)
            {
                if (_phase.Current != BattlePhase.Defeat) ForceTerminal(BattlePhase.Defeat, "失败");
                return true;
            }
            return false;
        }

        private void ForceTerminal(BattlePhase terminal, string message)
        {
            if (!_phase.TryTransition(terminal)) _phase.Reset(terminal);
            _resultMessage = message;
            FreezeAllDice();
            AddLog(message + "：剩余行动已取消");
        }

        public void TryMerge(BattleDie first, BattleDie second)
        {
            if (_phase == null || (_phase.Current != BattlePhase.PlayerAim && _phase.Current != BattlePhase.PhysicsResolution)) return;
            if (!_mergeService.CanMerge(first, second)) return;
            MergeRecord record = _mergeService.LockAndCreateRecord(first, second);
            if (!_effectQueue.EnqueueUnique(record)) return;
            Vector2 position = ((Vector2)first.transform.position + (Vector2)second.transform.position) * 0.5f;
            Vector2 velocity = (first.Body.velocity + second.Body.velocity) * 0.5f;
            DiceKind kind = first.Kind;
            Sprite sprite = first.SpriteRenderer.sprite;
            Destroy(first.gameObject);
            Destroy(second.gameObject);
            BattleDie merged = SpawnDie(kind, record.ResultLevel, position, true, sprite);
            merged.Body.velocity = velocity + Vector2.up * 1.25f;
            AddLog("合并 " + KindShort(kind) + " D" + record.SourceLevel + " → D" + record.ResultLevel + " / 连锁 " + record.ChainDepth);
        }

        private BattleDie SpawnDie(DiceKind kind, int level, Vector2 position, bool protect, Sprite overrideSprite = null)
        {
            GameObject dieObject = new GameObject(KindShort(kind) + " D" + level);
            dieObject.transform.position = position;
            dieObject.transform.localScale = Vector3.one * 1.15f;
            BattleDie die = dieObject.AddComponent<BattleDie>();
            die.Initialize(this, kind, level, overrideSprite != null ? overrideSprite : _diceSprites[(int)kind], protect);
            return die;
        }

        private void LoadScenario(AcceptanceScenario scenario)
        {
            if (_roundRoutine != null) StopCoroutine(_roundRoutine);
            StopAllCoroutines();
            foreach (BattleDie die in FindObjectsOfType<BattleDie>()) Destroy(die.gameObject);
            for (int i = 0; i < _enemyViews.Count; i++) if (_enemyViews[i] != null) Destroy(_enemyViews[i].gameObject);
            _enemyViews.Clear();
            _enemies.Clear();
            _effectQueue.Clear();
            _mergeService.Reset();
            _battleLog.Clear();
            _scenario = scenario;
            _random = new System.Random(BattleBalance.Seed + (int)scenario * 97);
            _player = new CombatantModel("游侠", BattleBalance.PlayerMaxHealth);
            _phase = new BattlePhaseMachine(BattlePhase.RoundPreparation);
            _round = 0;
            _nextKind = DiceKind.Attack;
            _nextLevel = 1;
            _presentationSpeed = 1f;
            _resultMessage = string.Empty;
            _physicsTimedOut = false;
            _debugGold = 0;

            _enemies.Add(new EnemyModel(EnemyKind.Goblin, "哥布林", 30, new[] { 8, 10, 0 }));
            _enemies.Add(new EnemyModel(EnemyKind.Archer, "弓手", 22, new[] { 5, 7, 7 }));

            switch (scenario)
            {
                case AcceptanceScenario.Attack:
                    SpawnResting(DiceKind.Attack, 1, new Vector2(-5.5f, -3.2f));
                    SpawnResting(DiceKind.Attack, 1, new Vector2(0.6f, -3.2f));
                    break;
                case AcceptanceScenario.HealCap:
                    _player.Reset("游侠", 60, 55, 0);
                    _nextKind = DiceKind.Heal;
                    _nextLevel = 2;
                    SpawnResting(DiceKind.Heal, 2, new Vector2(-5.2f, -3.2f));
                    SpawnResting(DiceKind.Heal, 2, new Vector2(0.4f, -3.2f));
                    break;
                case AcceptanceScenario.ShieldAbsorb:
                    _nextKind = DiceKind.Shield;
                    _nextLevel = 2;
                    _enemies.RemoveAt(1);
                    _enemies[0].ForceIntent(IntentKind.Attack, 8);
                    SpawnResting(DiceKind.Shield, 2, new Vector2(-5.2f, -3.2f));
                    SpawnResting(DiceKind.Shield, 2, new Vector2(0.4f, -3.2f));
                    break;
                case AcceptanceScenario.Chain:
                    _enemies[0].Unit.Reset("哥布林", 80, 80, 0);
                    _enemies[1].Unit.Reset("弓手", 60, 60, 0);
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-6.0f, 1.0f), Vector2.right * 2f);
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-4.8f, 1.0f), Vector2.left * 2f);
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-1.4f, 1.0f), Vector2.right * 2f);
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-0.2f, 1.0f), Vector2.left * 2f);
                    break;
                case AcceptanceScenario.VictoryInterrupt:
                    _enemies.Clear();
                    _enemies.Add(new EnemyModel(EnemyKind.Goblin, "哥布林", 1, new[] { 99 }));
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-4.5f, -2.8f), Vector2.right * 2f);
                    SpawnMoving(DiceKind.Attack, 1, new Vector2(-3.2f, -2.8f), Vector2.left * 2f);
                    break;
                case AcceptanceScenario.DefeatInterrupt:
                    _player.Reset("游侠", 60, 1, 0);
                    _enemies.RemoveAt(1);
                    _enemies[0].ForceIntent(IntentKind.Attack, 8);
                    break;
                case AcceptanceScenario.PhysicsTimeout:
                    BattleDie moving = SpawnMoving(DiceKind.Shield, 1, new Vector2(-2f, 1f), Vector2.right * 0.2f);
                    moving.KeepMovingForAcceptance = true;
                    break;
            }

            CreateCharacterViews();
            AddLog("已载入：" + ScenarioShort(scenario) + " / 随机种子 " + (BattleBalance.Seed + (int)scenario * 97));
            BeginRound();
        }

        private void SpawnResting(DiceKind kind, int level, Vector2 position)
        {
            BattleDie die = SpawnDie(kind, level, position, false);
            die.Body.Sleep();
        }

        private BattleDie SpawnMoving(DiceKind kind, int level, Vector2 position, Vector2 velocity)
        {
            BattleDie die = SpawnDie(kind, level, position, false);
            die.Body.velocity = velocity;
            return die;
        }

        private void CreateCharacterViews()
        {
            CreateCharacterView("Player", _playerSprite, new Vector2(4.6f, -2.75f), new Color(0.72f, 0.9f, 1f), 5);
            for (int i = 0; i < _enemies.Count; i++)
            {
                Sprite sprite = _enemies[i].Kind == EnemyKind.Goblin ? _goblinSprite : _archerSprite;
                SpriteRenderer renderer = CreateCharacterView(_enemies[i].Unit.Name, sprite, new Vector2(4.6f, 1.55f - i * 1.55f), Color.white, 5);
                _enemyViews.Add(renderer);
            }
            RefreshEnemyViews();
        }

        private static SpriteRenderer CreateCharacterView(string name, Sprite sprite, Vector2 position, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject(name + " View");
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * 2.0f;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void RefreshEnemyViews()
        {
            for (int i = 0; i < _enemyViews.Count && i < _enemies.Count; i++)
                _enemyViews[i].color = _enemies[i].Unit.IsAlive ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.45f);
        }

        private EnemyModel FirstLivingEnemy()
        {
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Unit.IsAlive) return _enemies[i];
            return null;
        }

        private void RollNextDie()
        {
            _nextKind = (DiceKind)_random.Next(0, 3);
            _nextLevel = _random.NextDouble() < 0.78 ? 1 : 2;
        }

        private void AddLog(string message)
        {
            _battleLog.Insert(0, message);
            if (_battleLog.Count > 8) _battleLog.RemoveAt(_battleLog.Count - 1);
        }

        private static string KindShort(DiceKind kind)
        {
            return kind == DiceKind.Attack ? "攻击" : kind == DiceKind.Heal ? "治疗" : "护盾";
        }

        private static string PhaseShort(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.RoundPreparation: return "回合准备";
                case BattlePhase.PlayerAim: return "玩家瞄准";
                case BattlePhase.PlayerLaunch: return "玩家投掷";
                case BattlePhase.PhysicsResolution: return "物理收尾";
                case BattlePhase.PlayerEffects: return "玩家结算";
                case BattlePhase.EnemyActions: return "敌方行动";
                case BattlePhase.RoundRefresh: return "回合刷新";
                case BattlePhase.Victory: return "胜利";
                case BattlePhase.Defeat: return "失败";
                default: return "未知阶段";
            }
        }

        private static string ScenarioShort(AcceptanceScenario scenario)
        {
            switch (scenario)
            {
                case AcceptanceScenario.Attack: return "M1-A 基础攻击";
                case AcceptanceScenario.HealCap: return "M1-B 治疗上限";
                case AcceptanceScenario.ShieldAbsorb: return "M1-C 护盾吸收";
                case AcceptanceScenario.Chain: return "M1-D 连锁合并";
                case AcceptanceScenario.VictoryInterrupt: return "M1-E 胜利中断";
                case AcceptanceScenario.DefeatInterrupt: return "M1-F 失败中断";
                case AcceptanceScenario.PhysicsTimeout: return "M1-G 物理超时";
                default: return "标准战斗";
            }
        }

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null) return;
            _panelTexture = MakeTexture(new Color(0.02f, 0.035f, 0.07f, 0.92f));
            _buttonTexture = MakeTexture(new Color(0.13f, 0.32f, 0.58f, 0.98f));
            _selectedButtonTexture = MakeTexture(new Color(0.68f, 0.40f, 0.10f, 0.98f));
            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTexture } };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 30, 24, 38), fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 48, 16, 23), fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.86f, 0.38f) } };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 62, 13, 18), normal = { textColor = new Color(0.9f, 0.94f, 1f) } };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Clamp(Screen.height / 65, 13, 18), fontStyle = FontStyle.Bold, normal = { background = _buttonTexture, textColor = Color.white }, hover = { background = _buttonTexture, textColor = Color.yellow } };
            _selectedButtonStyle = new GUIStyle(_buttonStyle) { normal = { background = _selectedButtonTexture, textColor = Color.white } };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            DrawHeader();
            DrawBattlePanel();
            DrawAimGuide();
            if (_debugOpen) DrawAcceptancePanel();
            if (_phase.Current == BattlePhase.Victory || _phase.Current == BattlePhase.Defeat) DrawResult();
        }

        private void DrawHeader()
        {
            float margin = 12f;
            GUI.Box(new Rect(margin, margin, Screen.width - margin * 2, 116f), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(132f, 18f, 430f, 44f), "符文骰子 · 战斗切片", _titleStyle);
            GUI.Label(new Rect(132f, 66f, 470f, 30f), "第 " + _round + " 回合  |  " + PhaseShort(_phase.Current) + "  |  " + ScenarioShort(_scenario), _labelStyle);
            GUI.Label(new Rect(132f, 96f, 600f, 24f), "在场地内按住鼠标左键瞄准，松开后投掷。F10：验收模式", _smallStyle);
            GUI.Label(new Rect(Screen.width - 250f, 24f, 220f, 26f), BattleBalance.Version, _labelStyle);
            GUI.Label(new Rect(Screen.width - 250f, 58f, 220f, 24f), "随机种子：" + (BattleBalance.Seed + (int)_scenario * 97), _smallStyle);
            GUI.Label(new Rect(Screen.width - 250f, 84f, 220f, 24f), "下一枚：" + KindShort(_nextKind) + " D" + _nextLevel, _smallStyle);
        }

        private void DrawBattlePanel()
        {
            float x = Screen.width * 0.78f;
            float y = 142f;
            float width = Screen.width - x - 12f;
            GUI.Box(new Rect(x, y, width, Screen.height - y - 12f), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(x + 14f, y + 10f, width - 28f, 28f), "玩家", _labelStyle);
            GUI.Label(new Rect(x + 14f, y + 42f, width - 28f, 24f), "生命 " + _player.Health + "/" + _player.MaxHealth + "    护盾 " + _player.Shield, _smallStyle);
            float lineY = y + 82f;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyModel enemy = _enemies[i];
                string intent = enemy.Unit.IsAlive ? (enemy.IntentKind == IntentKind.Attack ? "攻击 " + enemy.IntentValue : "等待") : "已击败";
                GUI.Label(new Rect(x + 14f, lineY, width - 28f, 25f), enemy.Unit.Name + "  生命 " + enemy.Unit.Health + "/" + enemy.Unit.MaxHealth, _smallStyle);
                GUI.Label(new Rect(x + 14f, lineY + 24f, width - 28f, 24f), "行动预告：" + intent, _labelStyle);
                lineY += 58f;
            }
            GUI.Label(new Rect(x + 14f, lineY + 4f, width - 28f, 26f), "合并队列  " + _effectQueue.Count, _labelStyle);
            GUI.Label(new Rect(x + 14f, lineY + 32f, width - 28f, 24f), "场上骰子：" + FindObjectsOfType<BattleDie>().Length + "   连锁：" + _mergeService.CurrentChainDepth, _smallStyle);
            lineY += 70f;
            GUI.Label(new Rect(x + 14f, lineY, width - 28f, 26f), "战斗记录", _labelStyle);
            lineY += 30f;
            for (int i = 0; i < _battleLog.Count; i++)
            {
                GUI.Label(new Rect(x + 14f, lineY + i * 23f, width - 28f, 23f), _battleLog[i], _smallStyle);
            }
            if (_physicsTimedOut) GUI.Label(new Rect(x + 14f, Screen.height - 48f, width - 28f, 24f), "已触发物理超时保护", _labelStyle);
        }

        private void DrawAimGuide()
        {
            Vector3 originScreen = _camera.WorldToScreenPoint(new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, 0f));
            float y = Screen.height - originScreen.y;
            GUI.Label(new Rect(originScreen.x - 70f, y - 28f, 180f, 24f), "从这里投掷", _smallStyle);
            if (!_isAiming) return;
            Vector3 targetScreen = _camera.WorldToScreenPoint(_aimWorld);
            DrawLine(new Vector2(originScreen.x, Screen.height - originScreen.y), new Vector2(targetScreen.x, Screen.height - targetScreen.y), new Color(1f, 0.82f, 0.2f), 5f);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color old = GUI.color;
            GUI.color = color;
            float angle = Vector3.Angle(b - a, Vector2.right);
            if (a.y > b.y) angle = -angle;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, (b - a).magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = old;
        }

        private void DrawAcceptancePanel()
        {
            float width = Mathf.Min(620f, Screen.width * 0.40f);
            float x = Screen.width * 0.37f;
            float y = 142f;
            GUI.Box(new Rect(x, y, width, Screen.height - y - 12f), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(x + 14f, y + 10f, width - 28f, 30f), "F10 验收模式（调试数据）", _labelStyle);
            GUI.Label(new Rect(x + 14f, y + 40f, width - 28f, 24f), "场景：战斗场景   阶段：" + PhaseShort(_phase.Current), _smallStyle);
            GUI.Label(new Rect(x + 14f, y + 64f, width - 28f, 24f), "版本：" + BattleBalance.Version + "   随机种子：" + (BattleBalance.Seed + (int)_scenario * 97), _smallStyle);
            float by = y + 98f;
            AcceptanceScenario[] scenarios = (AcceptanceScenario[])Enum.GetValues(typeof(AcceptanceScenario));
            for (int i = 0; i < scenarios.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                Rect rect = new Rect(x + 14f + col * (width * 0.48f), by + row * 36f, width * 0.45f, 30f);
                if (GUI.Button(rect, ScenarioShort(scenarios[i]), scenarios[i] == _scenario ? _selectedButtonStyle : _buttonStyle)) LoadScenario(scenarios[i]);
            }
            by += 154f;
            GUI.Label(new Rect(x + 14f, by, width - 28f, 25f), "生成骰子", _labelStyle);
            by += 30f;
            DiceKind[] kinds = (DiceKind[])Enum.GetValues(typeof(DiceKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                if (GUI.Button(new Rect(x + 14f + i * (width - 40f) / 3f, by, (width - 54f) / 3f, 30f), KindShort(kinds[i]), _nextKind == kinds[i] ? _selectedButtonStyle : _buttonStyle)) _nextKind = kinds[i];
            }
            by += 36f;
            if (GUI.Button(new Rect(x + 14f, by, 42f, 30f), "-", _buttonStyle)) _nextLevel = Mathf.Max(1, _nextLevel - 1);
            GUI.Label(new Rect(x + 64f, by + 2f, 90f, 25f), "等级 D" + _nextLevel, _smallStyle);
            if (GUI.Button(new Rect(x + 150f, by, 42f, 30f), "+", _buttonStyle)) _nextLevel = Mathf.Min(6, _nextLevel + 1);
            if (GUI.Button(new Rect(x + 208f, by, 104f, 30f), "生成左侧", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector2(-5.5f, 2.3f));
            if (GUI.Button(new Rect(x + 320f, by, 120f, 30f), "生成中央", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector2(-2.2f, 2.3f));
            if (GUI.Button(new Rect(x + 448f, by, 104f, 30f), "生成右侧", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector2(1.1f, 2.3f));
            by += 44f;
            GUI.Label(new Rect(x + 14f, by, 128f, 25f), "玩家生命", _smallStyle);
            if (GUI.Button(new Rect(x + 135f, by, 42f, 28f), "-", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health - 1, _player.Shield);
            if (GUI.Button(new Rect(x + 181f, by, 42f, 28f), "+", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health + 1, _player.Shield);
            GUI.Label(new Rect(x + 240f, by, 100f, 25f), "护盾 " + _player.Shield, _smallStyle);
            if (GUI.Button(new Rect(x + 330f, by, 42f, 28f), "-", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health, Mathf.Max(0, _player.Shield - 1));
            if (GUI.Button(new Rect(x + 376f, by, 42f, 28f), "+", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health, _player.Shield + 1);
            by += 38f;
            GUI.Label(new Rect(x + 14f, by, 128f, 25f), "调试金币 " + _debugGold, _smallStyle);
            if (GUI.Button(new Rect(x + 135f, by, 42f, 28f), "-", _buttonStyle)) _debugGold = Mathf.Max(0, _debugGold - 10);
            if (GUI.Button(new Rect(x + 181f, by, 42f, 28f), "+", _buttonStyle)) _debugGold += 10;
            by += 38f;
            EnemyModel target = FirstLivingEnemy();
            if (target != null)
            {
                GUI.Label(new Rect(x + 14f, by, 160f, 25f), "敌人生命 " + target.Unit.Health, _smallStyle);
                if (GUI.Button(new Rect(x + 160f, by, 42f, 28f), "-", _buttonStyle)) target.Unit.Reset(target.Unit.Name, target.Unit.MaxHealth, target.Unit.Health - 1, target.Unit.Shield);
                if (GUI.Button(new Rect(x + 206f, by, 42f, 28f), "+", _buttonStyle)) target.Unit.Reset(target.Unit.Name, target.Unit.MaxHealth, target.Unit.Health + 1, target.Unit.Shield);
                if (GUI.Button(new Rect(x + 262f, by, 150f, 28f), "预告攻击 8", _buttonStyle)) target.ForceIntent(IntentKind.Attack, 8);
            }
            by += 40f;
            GUI.Label(new Rect(x + 14f, by, 110f, 25f), "表现速度", _smallStyle);
            float[] speeds = { 1f, 2f, 3f };
            for (int i = 0; i < speeds.Length; i++)
                if (GUI.Button(new Rect(x + 104f + i * 72f, by, 64f, 28f), speeds[i] + "x", Mathf.Approximately(_presentationSpeed, speeds[i]) ? _selectedButtonStyle : _buttonStyle)) _presentationSpeed = speeds[i];
            if (GUI.Button(new Rect(x + 330f, by, 120f, 28f), "重新挑战", _buttonStyle)) LoadScenario(_scenario);
            by += 36f;
            if (GUI.Button(new Rect(x + 14f, by, 180f, 28f), "清除调试数据", _buttonStyle))
            {
                PlayerPrefs.DeleteKey("RuneDice.Debug");
                PlayerPrefs.Save();
                _debugGold = 0;
                AddLog("已清除调试数据");
            }
            if (GUI.Button(new Rect(x + 202f, by, 210f, 28f), "复制日志路径", _buttonStyle))
            {
                GUIUtility.systemCopyBuffer = Application.consoleLogPath;
                AddLog("已复制日志路径");
            }
        }

        private void DrawResult()
        {
            Rect rect = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 105f, 440f, 210f);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUIStyle centered = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x, rect.y + 22f, rect.width, 55f), _resultMessage, centered);
            GUI.Label(new Rect(rect.x + 30f, rect.y + 78f, rect.width - 60f, 28f), ScenarioShort(_scenario) + " / " + BattleBalance.Version, _smallStyle);
            if (GUI.Button(new Rect(rect.x + 55f, rect.y + 125f, 150f, 48f), "重新挑战", _buttonStyle)) LoadScenario(_scenario);
            if (GUI.Button(new Rect(rect.x + 235f, rect.y + 125f, 150f, 48f), "标准战斗", _buttonStyle)) LoadScenario(AcceptanceScenario.Standard);
        }
    }
}
