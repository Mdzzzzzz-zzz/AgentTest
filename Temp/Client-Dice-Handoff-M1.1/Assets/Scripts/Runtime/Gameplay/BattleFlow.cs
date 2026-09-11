using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiceDemo.M1
{
    public sealed class BattleFlow : MonoBehaviour
    {
        private const string SceneName = "BattleM1";
        private readonly DiceMergeService _mergeService = new DiceMergeService();
        private readonly MergeEffectQueue _effectQueue = new MergeEffectQueue();
        private readonly List<EnemyModel> _enemies = new List<EnemyModel>();
        private readonly List<SpriteRenderer> _enemyViews = new List<SpriteRenderer>();
        private readonly List<string> _battleLog = new List<string>();
        private System.Random _random;
        private Camera _camera;
        private BattleDie _diceTemplate;
        private CombatantModel _player;
        private BattlePhaseMachine _phase;
        private Sprite _playerSprite, _goblinSprite, _archerSprite;
        private Texture2D _panelTexture, _buttonTexture, _selectedButtonTexture;
        private GUIStyle _panelStyle, _titleStyle, _labelStyle, _smallStyle, _buttonStyle, _selectedButtonStyle;
        private Coroutine _roundRoutine;
        private AcceptanceScenario _scenario;
        private DiceKind _nextKind = DiceKind.Attack;
        private int _nextLevel = 1, _round, _debugGold;
        private bool _isAiming, _debugOpen, _physicsTimedOut;
        private Vector3 _aimWorld;
        private float _physicsStartedAt, _allSleepingSince = -1f, _presentationSpeed = 1f;
        private string _resultMessage = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { if (SceneManager.GetActiveScene().name == SceneName && FindObjectOfType<BattleFlow>() == null) new GameObject("战斗流程控制器").AddComponent<BattleFlow>(); }
        public BattlePhase CurrentPhase => _phase == null ? BattlePhase.RoundPreparation : _phase.Current;
        public int PendingEffects => _effectQueue.Count;

        private void Awake()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true;
            Physics.gravity = new Vector3(0f, -30f, 0f); Physics.defaultMaxAngularSpeed = 8f; Physics.defaultSolverIterations = 10; Physics.defaultSolverVelocityIterations = 3;
            _camera = Camera.main; LoadArt(); ResolveTemplate(); LoadScenario(AcceptanceScenario.Standard);
            if (HasArgument("-m1SmokeTest")) StartCoroutine(RunCommandLineSmokeTest()); else if (HasArgument("-m1Capture")) StartCoroutine(RunCommandLineCapture());
        }

        private void ResolveTemplate()
        {
            if (_diceTemplate != null) return;
            foreach (BattleDie item in Resources.FindObjectsOfTypeAll<BattleDie>()) if (item != null && item.gameObject.scene == gameObject.scene && item.gameObject.name == "原作三维骰子模板") { _diceTemplate = item; return; }
        }
        private void LoadArt() { _playerSprite = LoadSprite("BattleM1/1_archer_64x64_idle_1", 64f); _goblinSprite = LoadSprite("BattleM1/goblinwarrior_64x64_idle_1", 64f); _archerSprite = LoadSprite("BattleM1/goblinarcher_64x64_idle_2", 64f); }
        private static Sprite LoadSprite(string path, float ppu) { Texture2D texture = Resources.Load<Texture2D>(path); return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10)) _debugOpen = !_debugOpen;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (_debugOpen) _debugOpen = false; else Application.Quit(); }
            if (_phase == null || _phase.Current != BattlePhase.PlayerAim || IsPointerOverGui() || _camera == null) return;
            Vector3 world; if (!TryGetBoardPoint(Input.mousePosition, out world)) return;
            if (Input.GetMouseButtonDown(0)) { _isAiming = true; _aimWorld = world; }
            if (_isAiming && Input.GetMouseButton(0)) _aimWorld = world;
            if (_isAiming && Input.GetMouseButtonUp(0)) { _isAiming = false; LaunchToward(world); }
        }
        private bool TryGetBoardPoint(Vector3 screen, out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(screen); Plane plane = new Plane(Vector3.up, new Vector3(0f, 0.8f, 0f)); float enter;
            if (!plane.Raycast(ray, out enter)) { point = Vector3.zero; return false; } point = ray.GetPoint(enter); point.x = Mathf.Clamp(point.x, BattleBalance.BoardLeft, BattleBalance.BoardRight); point.z = Mathf.Clamp(point.z, 1f, 8f); return true;
        }
        private bool IsPointerOverGui() { return Input.mousePosition.y > Screen.height - 138f || Input.mousePosition.x > Screen.width * 0.78f || (_debugOpen && Input.mousePosition.x > Screen.width * 0.57f); }

        private void LaunchToward(Vector3 target)
        {
            if (!_phase.TryTransition(BattlePhase.PlayerLaunch)) return;
            Vector3 origin = new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, 1.4f), delta = target - origin; if (delta.sqrMagnitude < 0.2f) delta = Vector3.forward;
            BattleDie die = SpawnDie(_nextKind, _nextLevel, origin, true); if (die == null) return;
            die.Launch(delta.normalized * Mathf.Clamp(delta.magnitude * 2.2f, 7f, 16f), new Vector3(RandomSigned(7f), RandomSigned(7f), RandomSigned(7f)));
            AddLog("已投掷：" + KindShort(_nextKind) + "，第" + _nextLevel + "级"); RollNextDie(); _phase.TryTransition(BattlePhase.PhysicsResolution); _physicsStartedAt = Time.time; _allSleepingSince = -1f; _physicsTimedOut = false; _roundRoutine = StartCoroutine(ResolveRound());
        }
        private float RandomSigned(float max) { return (float)(_random.NextDouble() * max * 2f - max); }
        private IEnumerator ResolveRound()
        {
            while (_phase.Current == BattlePhase.PhysicsResolution)
            {
                float elapsed = Time.time - _physicsStartedAt; bool settled = elapsed >= BattleBalance.MinimumPhysicsTime && AllDiceSleeping();
                if (settled) { if (_allSleepingSince < 0f) _allSleepingSince = Time.time; if (Time.time - _allSleepingSince >= 0.35f) break; } else _allSleepingSince = -1f;
                if (elapsed >= BattleBalance.PhysicsTimeout) { _physicsTimedOut = true; FreezeAllDice(); AddLog("物理超时：已安全收尾"); break; } yield return null;
            }
            if (!_phase.TryTransition(BattlePhase.PlayerEffects)) yield break; yield return ExecutePlayerEffects(); if (TryFinishBattle()) yield break;
            if (!_phase.TryTransition(BattlePhase.EnemyActions)) yield break; yield return ExecuteEnemyActions(); if (TryFinishBattle()) yield break;
            if (!_phase.TryTransition(BattlePhase.RoundRefresh)) yield break; yield return PresentationWait(0.35f); if (!_phase.TryTransition(BattlePhase.RoundPreparation)) yield break; BeginRound();
        }
        private IEnumerator ExecutePlayerEffects()
        {
            MergeRecord record; while (_effectQueue.TryDequeue(out record)) { int amount = BattleRules.GetScaledEffect(record.Kind, record.SourceLevel, record.ChainDepth); if (record.Kind == DiceKind.Attack) { EnemyModel target = FirstLivingEnemy(); if (target == null) yield break; DamageResult result = target.Unit.ApplyDamage(amount); AddLog("连锁" + record.ChainDepth + "：攻击" + amount + "，" + target.Unit.Name + "生命-" + result.HealthLost); RefreshEnemyViews(); } else if (record.Kind == DiceKind.Heal) AddLog("连锁" + record.ChainDepth + "：治疗+" + _player.Heal(amount)); else AddLog("连锁" + record.ChainDepth + "：护盾+" + _player.AddShield(amount)); if (TryFinishBattle()) yield break; yield return PresentationWait(0.45f); }
        }
        private IEnumerator ExecuteEnemyActions()
        {
            for (int i = 0; i < _enemies.Count; i++) { EnemyModel enemy = _enemies[i]; if (!enemy.Unit.IsAlive) continue; if (enemy.IntentKind == IntentKind.Attack) { DamageResult result = _player.ApplyDamage(enemy.IntentValue); AddLog(enemy.Unit.Name + "攻击" + enemy.IntentValue + "，护盾-" + result.ShieldLost + "，生命-" + result.HealthLost); } else AddLog(enemy.Unit.Name + "等待"); enemy.AdvanceIntent(); if (TryFinishBattle()) yield break; yield return PresentationWait(0.45f); }
        }
        private void BeginRound() { _round++; _player.ClearShield(); AddLog("第" + _round + "回合：拖动瞄准线并松开投掷"); StartCoroutine(BeginAimAfterDelay()); }
        private IEnumerator BeginAimAfterDelay() { yield return PresentationWait(0.2f); if (_phase.Current == BattlePhase.RoundPreparation) _phase.TryTransition(BattlePhase.PlayerAim); }
        private IEnumerator PresentationWait(float seconds) { float until = Time.unscaledTime + seconds / Mathf.Max(1f, _presentationSpeed); while (Time.unscaledTime < until) yield return null; }
        private bool AllDiceSleeping() { foreach (BattleDie die in FindObjectsOfType<BattleDie>()) if (die.Body != null && (die.Body.velocity.sqrMagnitude > 0.01f || die.Body.angularVelocity.sqrMagnitude > 0.03f)) return false; return true; }
        private static void FreezeAllDice() { foreach (BattleDie die in FindObjectsOfType<BattleDie>()) { if (die.Body == null) continue; die.Body.velocity = Vector3.zero; die.Body.angularVelocity = Vector3.zero; die.Body.Sleep(); } }
        private bool TryFinishBattle() { if (FirstLivingEnemy() == null) { ForceTerminal(BattlePhase.Victory, "胜利"); return true; } if (!_player.IsAlive) { ForceTerminal(BattlePhase.Defeat, "失败"); return true; } return false; }
        private void ForceTerminal(BattlePhase terminal, string message) { if (_phase.Current == terminal) return; if (!_phase.TryTransition(terminal)) _phase.Reset(terminal); _resultMessage = message; FreezeAllDice(); AddLog(message + "：剩余行动已取消"); }

        public void TryMerge(BattleDie first, BattleDie second)
        {
            if (_phase == null || (_phase.Current != BattlePhase.PlayerAim && _phase.Current != BattlePhase.PhysicsResolution) || !_mergeService.CanMerge(first, second)) return;
            MergeRecord record = _mergeService.LockAndCreateRecord(first, second); if (!_effectQueue.EnqueueUnique(record)) return; Vector3 position = (first.transform.position + second.transform.position) * 0.5f + Vector3.up * 0.35f; Vector3 velocity = (first.Body.velocity + second.Body.velocity) * 0.5f; DiceKind kind = first.Kind; Destroy(first.gameObject); Destroy(second.gameObject); BattleDie merged = SpawnDie(kind, record.ResultLevel, position, true); if (merged != null) merged.SetMotion(velocity + Vector3.up * 1.25f, new Vector3(2f, -1.5f, 1.5f)); AddLog("合并" + KindShort(kind) + "，第" + record.SourceLevel + "级→第" + record.ResultLevel + "级，连锁" + record.ChainDepth);
        }
        public void RegisterDrop(BattleDie die) { if (die != null) Destroy(die.gameObject); }
        private BattleDie SpawnDie(DiceKind kind, int level, Vector3 position, bool protect) { ResolveTemplate(); if (_diceTemplate == null) return null; BattleDie die = Instantiate(_diceTemplate, position, UnityEngine.Random.rotation, transform); die.name = KindShort(kind) + "骰子" + level; die.Initialize(this, kind, level, protect); die.gameObject.SetActive(true); return die; }
        private void SpawnResting(DiceKind kind, int level, Vector3 position) { BattleDie die = SpawnDie(kind, level, position, false); if (die != null) die.PlaceAndSleep(); }
        private void SpawnMoving(DiceKind kind, int level, Vector3 position, Vector3 velocity) { BattleDie die = SpawnDie(kind, level, position, false); if (die != null) die.SetMotion(velocity, new Vector3(0f, 2f, 0f)); }

        private void LoadScenario(AcceptanceScenario scenario)
        {
            if (_roundRoutine != null) StopCoroutine(_roundRoutine); StopAllCoroutines(); foreach (BattleDie die in FindObjectsOfType<BattleDie>()) Destroy(die.gameObject); foreach (SpriteRenderer view in _enemyViews) if (view != null) Destroy(view.gameObject); _enemyViews.Clear(); _enemies.Clear(); _effectQueue.Clear(); _mergeService.Reset(); _battleLog.Clear();
            _scenario = scenario; _random = new System.Random(BattleBalance.Seed + (int)scenario * 97); _player = new CombatantModel("游侠", BattleBalance.PlayerMaxHealth); _phase = new BattlePhaseMachine(BattlePhase.RoundPreparation); _round = 0; _nextKind = DiceKind.Attack; _nextLevel = 1; _presentationSpeed = 1f; _resultMessage = string.Empty; _physicsTimedOut = false; _debugGold = 0;
            _enemies.Add(new EnemyModel(EnemyKind.Goblin, "哥布林", 30, new[] { 8, 10, 0 })); _enemies.Add(new EnemyModel(EnemyKind.Archer, "弓手", 22, new[] { 5, 7, 7 }));
            switch (scenario) { case AcceptanceScenario.Attack: SpawnResting(DiceKind.Attack, 1, new Vector3(-3.1f, 1.5f, 2f)); SpawnResting(DiceKind.Attack, 1, new Vector3(-0.8f, 1.5f, 2f)); break; case AcceptanceScenario.HealCap: _player.Reset("游侠", 60, 55, 0); _nextKind = DiceKind.Heal; _nextLevel = 2; SpawnResting(DiceKind.Heal, 2, new Vector3(-3.1f, 1.5f, 2f)); SpawnResting(DiceKind.Heal, 2, new Vector3(-0.8f, 1.5f, 2f)); break; case AcceptanceScenario.ShieldAbsorb: _nextKind = DiceKind.Shield; _nextLevel = 2; _enemies.RemoveAt(1); _enemies[0].ForceIntent(IntentKind.Attack, 8); SpawnResting(DiceKind.Shield, 2, new Vector3(-3.1f, 1.5f, 2f)); SpawnResting(DiceKind.Shield, 2, new Vector3(-0.8f, 1.5f, 2f)); break; case AcceptanceScenario.Chain: _enemies[0].Unit.Reset("哥布林", 80, 80, 0); _enemies[1].Unit.Reset("弓手", 60, 60, 0); SpawnMoving(DiceKind.Attack, 1, new Vector3(-3.8f, 1.5f, 3f), Vector3.right * 2f); SpawnMoving(DiceKind.Attack, 1, new Vector3(-2.4f, 1.5f, 3f), Vector3.left * 2f); SpawnMoving(DiceKind.Attack, 1, new Vector3(-0.8f, 1.5f, 3f), Vector3.right * 2f); SpawnMoving(DiceKind.Attack, 1, new Vector3(0.6f, 1.5f, 3f), Vector3.left * 2f); break; case AcceptanceScenario.VictoryInterrupt: _enemies.Clear(); _enemies.Add(new EnemyModel(EnemyKind.Goblin, "哥布林", 1, new[] { 99 })); SpawnMoving(DiceKind.Attack, 1, new Vector3(-2.8f, 1.5f, 2f), Vector3.right * 2f); SpawnMoving(DiceKind.Attack, 1, new Vector3(-1.4f, 1.5f, 2f), Vector3.left * 2f); break; case AcceptanceScenario.DefeatInterrupt: _player.Reset("游侠", 60, 1, 0); _enemies.RemoveAt(1); _enemies[0].ForceIntent(IntentKind.Attack, 8); break; case AcceptanceScenario.PhysicsTimeout: SpawnMoving(DiceKind.Shield, 1, new Vector3(-2f, 1.5f, 2.5f), Vector3.right * 0.2f); break; }
            CreateCharacterViews(); AddLog("已载入：" + ScenarioShort(scenario) + "，随机种子" + (BattleBalance.Seed + (int)scenario * 97)); BeginRound();
        }
        private void CreateCharacterViews() { CreateCharacterView("玩家", _playerSprite, new Vector3(3.4f, 1.6f, 1.8f), new Color(0.72f, 0.9f, 1f)); for (int i = 0; i < _enemies.Count; i++) _enemyViews.Add(CreateCharacterView(_enemies[i].Unit.Name, _enemies[i].Kind == EnemyKind.Goblin ? _goblinSprite : _archerSprite, new Vector3(3.5f, 1.7f, 4.8f + i * 1.35f), Color.white)); RefreshEnemyViews(); }
        private static SpriteRenderer CreateCharacterView(string name, Sprite sprite, Vector3 position, Color color) { GameObject obj = new GameObject(name + "表现"); obj.transform.position = position; obj.transform.localScale = Vector3.one * 2.2f; SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = 5; return renderer; }
        private void RefreshEnemyViews() { for (int i = 0; i < _enemyViews.Count && i < _enemies.Count; i++) _enemyViews[i].color = _enemies[i].Unit.IsAlive ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.45f); }
        private EnemyModel FirstLivingEnemy() { for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Unit.IsAlive) return _enemies[i]; return null; }
        private void RollNextDie() { _nextKind = (DiceKind)_random.Next(0, 3); _nextLevel = _random.NextDouble() < 0.78 ? 1 : 2; }
        private void AddLog(string message) { _battleLog.Insert(0, message); if (_battleLog.Count > 8) _battleLog.RemoveAt(_battleLog.Count - 1); }

        private IEnumerator RunCommandLineSmokeTest() { float deadline = Time.realtimeSinceStartup + 3f; while (_phase.Current != BattlePhase.PlayerAim && Time.realtimeSinceStartup < deadline) yield return null; if (_phase.Current != BattlePhase.PlayerAim) { Debug.LogError("M1三维冒烟失败：未进入瞄准阶段"); Application.Quit(2); yield break; } LaunchToward(new Vector3(-0.8f, 0.8f, 4f)); deadline = Time.realtimeSinceStartup + 8f; while (_round < 2 && _phase.Current != BattlePhase.Victory && _phase.Current != BattlePhase.Defeat && Time.realtimeSinceStartup < deadline) yield return null; bool passed = _round >= 2 || _phase.Current == BattlePhase.Victory || _phase.Current == BattlePhase.Defeat; if (passed) { Debug.Log("M1三维冒烟通过 阶段=" + PhaseShort(_phase.Current) + " 回合=" + _round); Application.Quit(0); } else { Debug.LogError("M1三维冒烟失败：回合未收尾"); Application.Quit(3); } }
        private IEnumerator RunCommandLineCapture() { _debugOpen = true; float deadline = Time.realtimeSinceStartup + 3f; while (_phase.Current != BattlePhase.PlayerAim && Time.realtimeSinceStartup < deadline) yield return null; yield return new WaitForSecondsRealtime(0.5f); string path = GetArgumentValue("-m1CapturePath"); if (string.IsNullOrWhiteSpace(path)) path = System.IO.Path.Combine(Application.persistentDataPath, "M1-preview.png"); ScreenCapture.CaptureScreenshot(path); yield return new WaitForSecondsRealtime(1f); Debug.Log("M1截图完成 路径=" + path); Application.Quit(0); }
        private static bool HasArgument(string expected) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        private static string GetArgumentValue(string name) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }

        private void EnsureGuiStyles() { if (_panelStyle != null) return; _panelTexture = MakeTexture(new Color(0.02f, 0.035f, 0.07f, 0.92f)); _buttonTexture = MakeTexture(new Color(0.13f, 0.32f, 0.58f, 0.98f)); _selectedButtonTexture = MakeTexture(new Color(0.68f, 0.40f, 0.10f, 0.98f)); _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTexture } }; _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }; _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.86f, 0.38f) } }; _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.9f, 0.94f, 1f) } }; _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { background = _buttonTexture, textColor = Color.white }, hover = { background = _buttonTexture, textColor = Color.yellow } }; _selectedButtonStyle = new GUIStyle(_buttonStyle) { normal = { background = _selectedButtonTexture, textColor = Color.white } }; }
        private static Texture2D MakeTexture(Color color) { Texture2D texture = new Texture2D(2, 2); texture.SetPixels(new[] { color, color, color, color }); texture.Apply(); return texture; }
        private void OnGUI() { EnsureGuiStyles(); DrawHeader(); DrawBattlePanel(); DrawAimGuide(); if (_debugOpen) DrawAcceptancePanel(); if (_phase.Current == BattlePhase.Victory || _phase.Current == BattlePhase.Defeat) DrawResult(); }
        private void DrawHeader() { GUI.Box(new Rect(12f, 12f, Screen.width - 24f, 116f), GUIContent.none, _panelStyle); GUI.Label(new Rect(132f, 18f, 560f, 42f), "符文骰子·三维战斗切片", _titleStyle); GUI.Label(new Rect(132f, 66f, 650f, 28f), "第" + _round + "回合｜" + PhaseShort(_phase.Current) + "｜" + ScenarioShort(_scenario), _labelStyle); GUI.Label(new Rect(132f, 96f, 700f, 24f), "在牌桌内按住鼠标左键瞄准，松开后投掷。按F10打开验收模式", _smallStyle); GUI.Label(new Rect(Screen.width - 300f, 24f, 280f, 26f), BattleBalance.Version, _labelStyle); GUI.Label(new Rect(Screen.width - 300f, 58f, 280f, 24f), "随机种子：" + (BattleBalance.Seed + (int)_scenario * 97), _smallStyle); GUI.Label(new Rect(Screen.width - 300f, 84f, 280f, 24f), "下一枚：" + KindShort(_nextKind) + "，第" + _nextLevel + "级", _smallStyle); }
        private void DrawBattlePanel() { float x = Screen.width * 0.78f, y = 142f, width = Screen.width - x - 12f; GUI.Box(new Rect(x, y, width, Screen.height - y - 12f), GUIContent.none, _panelStyle); GUI.Label(new Rect(x + 14f, y + 10f, width - 28f, 28f), "玩家", _labelStyle); GUI.Label(new Rect(x + 14f, y + 42f, width - 28f, 24f), "生命 " + _player.Health + "/" + _player.MaxHealth + "　护盾 " + _player.Shield, _smallStyle); float lineY = y + 82f; for (int i = 0; i < _enemies.Count; i++) { EnemyModel enemy = _enemies[i]; string intent = enemy.Unit.IsAlive ? (enemy.IntentKind == IntentKind.Attack ? "攻击 " + enemy.IntentValue : "等待") : "已击败"; GUI.Label(new Rect(x + 14f, lineY, width - 28f, 25f), enemy.Unit.Name + "　生命 " + enemy.Unit.Health + "/" + enemy.Unit.MaxHealth, _smallStyle); GUI.Label(new Rect(x + 14f, lineY + 24f, width - 28f, 24f), "行动预告：" + intent, _labelStyle); lineY += 58f; } GUI.Label(new Rect(x + 14f, lineY + 4f, width - 28f, 26f), "合并队列　" + _effectQueue.Count, _labelStyle); GUI.Label(new Rect(x + 14f, lineY + 32f, width - 28f, 24f), "场上骰子：" + FindObjectsOfType<BattleDie>().Length + "　连锁：" + _mergeService.CurrentChainDepth, _smallStyle); lineY += 70f; GUI.Label(new Rect(x + 14f, lineY, width - 28f, 26f), "战斗记录", _labelStyle); lineY += 30f; for (int i = 0; i < _battleLog.Count; i++) GUI.Label(new Rect(x + 14f, lineY + i * 23f, width - 28f, 23f), _battleLog[i], _smallStyle); if (_physicsTimedOut) GUI.Label(new Rect(x + 14f, Screen.height - 48f, width - 28f, 24f), "已触发物理超时保护", _labelStyle); }
        private void DrawAimGuide() { if (!_isAiming || _camera == null) return; Vector3 origin = _camera.WorldToScreenPoint(new Vector3(BattleBalance.LaunchX, BattleBalance.LaunchY, 1.4f)), target = _camera.WorldToScreenPoint(_aimWorld); DrawLine(new Vector2(origin.x, Screen.height - origin.y), new Vector2(target.x, Screen.height - target.y), new Color(1f, 0.82f, 0.2f), 5f); GUI.Label(new Rect(origin.x - 60f, Screen.height - origin.y - 28f, 160f, 24f), "从这里投掷", _smallStyle); }
        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width) { Matrix4x4 matrix = GUI.matrix; Color old = GUI.color; GUI.color = color; float angle = Vector3.Angle(b - a, Vector2.right); if (a.y > b.y) angle = -angle; GUIUtility.RotateAroundPivot(angle, a); GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, (b - a).magnitude, width), Texture2D.whiteTexture); GUI.matrix = matrix; GUI.color = old; }
        private void DrawAcceptancePanel() { float width = Mathf.Min(620f, Screen.width * 0.40f), x = Screen.width * 0.37f, y = 142f; GUI.Box(new Rect(x, y, width, Screen.height - y - 12f), GUIContent.none, _panelStyle); GUI.Label(new Rect(x + 14f, y + 10f, width - 28f, 30f), "F10验收模式（调试数据）", _labelStyle); GUI.Label(new Rect(x + 14f, y + 40f, width - 28f, 24f), "场景：三维战斗　阶段：" + PhaseShort(_phase.Current), _smallStyle); float by = y + 76f; AcceptanceScenario[] scenarios = (AcceptanceScenario[])Enum.GetValues(typeof(AcceptanceScenario)); for (int i = 0; i < scenarios.Length; i++) { int col = i % 2, row = i / 2; Rect rect = new Rect(x + 14f + col * width * 0.48f, by + row * 36f, width * 0.45f, 30f); if (GUI.Button(rect, ScenarioShort(scenarios[i]), scenarios[i] == _scenario ? _selectedButtonStyle : _buttonStyle)) LoadScenario(scenarios[i]); } by += 154f; GUI.Label(new Rect(x + 14f, by, width - 28f, 25f), "生成骰子", _labelStyle); by += 30f; DiceKind[] kinds = (DiceKind[])Enum.GetValues(typeof(DiceKind)); for (int i = 0; i < kinds.Length; i++) if (GUI.Button(new Rect(x + 14f + i * (width - 40f) / 3f, by, (width - 54f) / 3f, 30f), KindShort(kinds[i]), _nextKind == kinds[i] ? _selectedButtonStyle : _buttonStyle)) _nextKind = kinds[i]; by += 36f; if (GUI.Button(new Rect(x + 14f, by, 42f, 30f), "减", _buttonStyle)) _nextLevel = Mathf.Max(1, _nextLevel - 1); GUI.Label(new Rect(x + 64f, by + 2f, 90f, 25f), "第" + _nextLevel + "级", _smallStyle); if (GUI.Button(new Rect(x + 150f, by, 42f, 30f), "加", _buttonStyle)) _nextLevel = Mathf.Min(6, _nextLevel + 1); if (GUI.Button(new Rect(x + 208f, by, 104f, 30f), "生成左侧", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector3(-3.2f, 1.5f, 3f)); if (GUI.Button(new Rect(x + 320f, by, 120f, 30f), "生成中央", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector3(-1.1f, 1.5f, 3f)); if (GUI.Button(new Rect(x + 448f, by, 104f, 30f), "生成右侧", _buttonStyle)) SpawnResting(_nextKind, _nextLevel, new Vector3(1.1f, 1.5f, 3f)); by += 44f; GUI.Label(new Rect(x + 14f, by, 150f, 25f), "玩家生命 " + _player.Health, _smallStyle); if (GUI.Button(new Rect(x + 165f, by, 42f, 28f), "减", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health - 1, _player.Shield); if (GUI.Button(new Rect(x + 211f, by, 42f, 28f), "加", _buttonStyle)) _player.Reset(_player.Name, _player.MaxHealth, _player.Health + 1, _player.Shield); by += 38f; EnemyModel targetEnemy = FirstLivingEnemy(); if (targetEnemy != null) { GUI.Label(new Rect(x + 14f, by, 160f, 25f), "敌人生命 " + targetEnemy.Unit.Health, _smallStyle); if (GUI.Button(new Rect(x + 160f, by, 42f, 28f), "减", _buttonStyle)) targetEnemy.Unit.Reset(targetEnemy.Unit.Name, targetEnemy.Unit.MaxHealth, targetEnemy.Unit.Health - 1, targetEnemy.Unit.Shield); if (GUI.Button(new Rect(x + 206f, by, 42f, 28f), "加", _buttonStyle)) targetEnemy.Unit.Reset(targetEnemy.Unit.Name, targetEnemy.Unit.MaxHealth, targetEnemy.Unit.Health + 1, targetEnemy.Unit.Shield); if (GUI.Button(new Rect(x + 262f, by, 150f, 28f), "预告攻击八点", _buttonStyle)) targetEnemy.ForceIntent(IntentKind.Attack, 8); } by += 40f; GUI.Label(new Rect(x + 14f, by, 110f, 25f), "表现速度", _smallStyle); float[] speeds = { 1f, 2f, 3f }; string[] speedNames = { "一倍", "两倍", "三倍" }; for (int i = 0; i < speeds.Length; i++) if (GUI.Button(new Rect(x + 104f + i * 72f, by, 64f, 28f), speedNames[i], Mathf.Approximately(_presentationSpeed, speeds[i]) ? _selectedButtonStyle : _buttonStyle)) _presentationSpeed = speeds[i]; if (GUI.Button(new Rect(x + 330f, by, 120f, 28f), "重新挑战", _buttonStyle)) LoadScenario(_scenario); }
        private void DrawResult() { Rect rect = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 105f, 440f, 210f); GUI.Box(rect, GUIContent.none, _panelStyle); GUIStyle centered = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter }; GUI.Label(new Rect(rect.x, rect.y + 22f, rect.width, 55f), _resultMessage, centered); GUI.Label(new Rect(rect.x + 30f, rect.y + 78f, rect.width - 60f, 28f), ScenarioShort(_scenario) + "｜" + BattleBalance.Version, _smallStyle); if (GUI.Button(new Rect(rect.x + 55f, rect.y + 125f, 150f, 48f), "重新挑战", _buttonStyle)) LoadScenario(_scenario); if (GUI.Button(new Rect(rect.x + 235f, rect.y + 125f, 150f, 48f), "标准战斗", _buttonStyle)) LoadScenario(AcceptanceScenario.Standard); }
        private static string KindShort(DiceKind kind) { return kind == DiceKind.Attack ? "攻击" : kind == DiceKind.Heal ? "治疗" : "护盾"; }
        private static string PhaseShort(BattlePhase phase) { switch (phase) { case BattlePhase.RoundPreparation: return "回合准备"; case BattlePhase.PlayerAim: return "玩家瞄准"; case BattlePhase.PlayerLaunch: return "玩家投掷"; case BattlePhase.PhysicsResolution: return "物理收尾"; case BattlePhase.PlayerEffects: return "玩家结算"; case BattlePhase.EnemyActions: return "敌方行动"; case BattlePhase.RoundRefresh: return "回合刷新"; case BattlePhase.Victory: return "胜利"; case BattlePhase.Defeat: return "失败"; default: return "未知阶段"; } }
        private static string ScenarioShort(AcceptanceScenario scenario) { switch (scenario) { case AcceptanceScenario.Attack: return "M1-A基础攻击"; case AcceptanceScenario.HealCap: return "M1-B治疗上限"; case AcceptanceScenario.ShieldAbsorb: return "M1-C护盾吸收"; case AcceptanceScenario.Chain: return "M1-D连锁合并"; case AcceptanceScenario.VictoryInterrupt: return "M1-E胜利中断"; case AcceptanceScenario.DefeatInterrupt: return "M1-F失败中断"; case AcceptanceScenario.PhysicsTimeout: return "M1-G物理超时"; default: return "标准战斗"; } }
    }
}
