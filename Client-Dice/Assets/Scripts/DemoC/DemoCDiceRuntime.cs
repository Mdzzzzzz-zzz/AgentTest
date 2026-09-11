using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiceDemo.DemoC
{
    /// <summary>
    /// 演示阶段丙：管理多枚原作三维骰子的投掷、占位、碰撞、掉落与同级合并。
    /// </summary>
    public sealed class DemoCDiceRuntime : MonoBehaviour
    {
        public const float OriginalGravity = -30f;
        public const float OriginalCameraSize = 12f;
        public const float OriginalMass = 10f;
        public const float OriginalLinearDamping = 0.005f;
        public const float OriginalAngularDamping = 0.05f;
        public const int MaximumDice = 12;
        public static readonly Vector3 OriginalColliderSize = new Vector3(1.3f, 1.3f, 1.3f);

        [SerializeField] private DemoCDie _diceTemplate;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _diceRoot;

        private readonly List<DemoCDie> _activeDice = new List<DemoCDie>();
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _titleStyle;
        private Texture2D _panelTexture;
        private int _collisionCount;
        private int _dropCount;
        private int _mergeCount;
        private int _nextIdentifier = 1;
        private int _selectedLevel = 1;
        private int _throwCount;
        private string _status = "等待投掷";

        public DemoCDie DiceTemplate => ResolveDiceTemplate();
        public int ActiveDiceCount { get { PruneDiceList(); return _activeDice.Count; } }
        public int CollisionCount => _collisionCount;
        public int DropCount => _dropCount;
        public int MergeCount => _mergeCount;
        public int SelectedLevel => _selectedLevel;
        public int ThrowCount => _throwCount;
        public string StatusText => _status;

        public void Configure(DemoCDie diceTemplate, Transform spawnPoint, Transform diceRoot)
        {
            _diceTemplate = diceTemplate;
            _spawnPoint = spawnPoint;
            _diceRoot = diceRoot;
        }

        private DemoCDie ResolveDiceTemplate()
        {
            if (_diceTemplate != null) return _diceTemplate;

            // 编辑器测试打开场景时，停用的场景对象引用可能暂时表现为空；从当前场景恢复模板。
            DemoCDie[] candidates = Resources.FindObjectsOfTypeAll<DemoCDie>();
            for (int i = 0; i < candidates.Length; i++)
            {
                DemoCDie candidate = candidates[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene) continue;
                if (candidate.gameObject.name != "原作三维骰子模板") continue;
                _diceTemplate = candidate;
                return _diceTemplate;
            }
            return null;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics.gravity = new Vector3(0f, OriginalGravity, 0f);
            Physics.defaultMaxAngularSpeed = 8f;
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 3;
        }

        private IEnumerator Start()
        {
            yield return null;
            DemoCDie template = ResolveDiceTemplate();
            bool valid = template != null && template.Body != null &&
                         template.Collider != null && template.VisualVariants.Length == 9 &&
                         template.CountFaces(1) == 6 && FindObjectsOfType<Rigidbody2D>().Length == 0;
            if (valid)
                Debug.Log("DEMO_C_RUNTIME_OK 多骰系统已启动，最大骰子=12，视觉等级=9，面数=6，二维刚体=0");
            else
                Debug.LogError("DEMO_C_RUNTIME_FAILED 多骰系统配置不完整");

            if (HasArgument("-demoCSmokeTest"))
            {
                yield return RunSmokeTest();
                yield break;
            }

            CreateInitialLayout();
            string capturePath = GetArgument("-demoCCapturePath");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                ThrowSelectedDie();
                yield return new WaitForSeconds(0.30f);
                ThrowSelectedDie();
                yield return new WaitForSeconds(1.25f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(capturePath);
                Debug.Log("DEMO_C_CAPTURE_REQUESTED path=" + capturePath);
                yield return new WaitForSeconds(0.6f);
                if (HasArgument("-demoCQuitAfterCapture")) Application.Quit();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) ThrowSelectedDie();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeSelectedLevel(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeSelectedLevel(1);
            if (Input.GetKeyDown(KeyCode.Return)) StartMergeDemonstration();
            if (Input.GetKeyDown(KeyCode.Delete)) ClearAllDice();
        }

        public void ThrowSelectedDie()
        {
            PruneDiceList();
            if (_activeDice.Count >= MaximumDice)
            {
                _status = "牌桌已满，请先清空";
                return;
            }

            Vector3 spawn = _spawnPoint == null ? new Vector3(0f, 3.2f, 5f) : _spawnPoint.position;
            spawn.x += ((_throwCount % 5) - 2) * 0.34f;
            DemoCDie die = CreateDie(_selectedLevel, spawn, UnityEngine.Random.rotation);
            if (die == null) return;

            Vector3 impulse = new Vector3(UnityEngine.Random.Range(-14f, 14f), 30f, 42f);
            Vector3 spin = new Vector3(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f));
            if (spin.sqrMagnitude < 18f) spin += new Vector3(5f, -4f, 6f);
            die.Launch(impulse, spin);
            _throwCount++;
            _status = "已投掷" + ToChineseLevel(_selectedLevel) + "骰子";
        }

        public DemoCDie SpawnPlacedDie(int level, Vector3 position)
        {
            DemoCDie die = CreateDie(level, position, Quaternion.identity);
            if (die != null) die.PlaceAndSleep();
            return die;
        }

        public DemoCDie SpawnMovingDie(int level, Vector3 position, Vector3 velocity)
        {
            DemoCDie die = CreateDie(level, position, UnityEngine.Random.rotation);
            if (die != null) die.SetMotion(velocity, new Vector3(0f, 3.5f, 0f));
            return die;
        }

        public void ChangeSelectedLevel(int offset)
        {
            _selectedLevel = ((_selectedLevel - 1 + offset) % 9 + 9) % 9 + 1;
            _status = "已选择" + ToChineseLevel(_selectedLevel);
        }

        public void StartMergeDemonstration()
        {
            ClearAllDice();
            _selectedLevel = 1;
            DemoCDie left = SpawnMovingDie(1, new Vector3(-1.35f, 4.5f, 0.2f), new Vector3(3.4f, 0f, 0f));
            DemoCDie right = SpawnMovingDie(1, new Vector3(1.35f, 4.5f, 0.2f), new Vector3(-3.4f, 0f, 0f));
            if (left != null && right != null)
            {
                _status = "两枚一级骰子正在真实碰撞";
                Debug.Log("DEMO_C_DEMO_START left=" + left.transform.position + " right=" + right.transform.position +
                          " leftBody=" + (left.Body != null) + " rightBody=" + (right.Body != null));
            }
        }

        public bool CanMerge(DemoCDie first, DemoCDie second)
        {
            return first != null && second != null && first != second &&
                   !first.IsMerging && !second.IsMerging &&
                   first.Level == second.Level && first.Level < 9;
        }

        public bool TryMerge(DemoCDie first, DemoCDie second)
        {
            if (!CanMerge(first, second)) return false;

            first.MarkMerging();
            second.MarkMerging();
            int resultLevel = first.Level + 1;
            Vector3 mergePosition = (first.transform.position + second.transform.position) * 0.5f + Vector3.up * 0.35f;
            RemoveDie(first);
            RemoveDie(second);

            DemoCDie result = CreateDie(resultLevel, mergePosition, UnityEngine.Random.rotation);
            if (result != null)
                result.SetMotion(new Vector3(0f, 1.8f, 0f), new Vector3(2.4f, -2f, 1.7f));

            _mergeCount++;
            _status = "同级碰撞成功，合成为" + ToChineseLevel(resultLevel);
            Debug.Log("DEMO_C_MERGE_OK level=" + resultLevel + " active=" + ActiveDiceCount);
            return true;
        }

        public void RegisterCollision(DemoCDie source, Collision collision)
        {
            _collisionCount++;
            if (source == null || collision == null) return;
            DemoCDie other = collision.collider.GetComponentInParent<DemoCDie>();
            Debug.Log("DEMO_C_COLLISION source=" + source.Identifier + " other=" +
                      (other == null ? "无" : other.Identifier.ToString()) + " point=" + collision.transform.position);
            if (other != null) TryMerge(source, other);
        }

        public void RegisterDrop(DemoCDie die)
        {
            if (die == null || die.IsMerging) return;
            _dropCount++;
            Debug.Log("DEMO_C_DROP id=" + die.Identifier + " position=" + die.transform.position);
            _status = "一枚骰子掉出牌桌";
            RemoveDie(die);
        }

        public void RegisterSettled(DemoCDie die)
        {
            if (die != null && !die.IsMerging)
                _status = ToChineseLevel(die.Level) + "骰子已停稳占位";
        }

        public DemoCDie[] GetDiceSnapshot()
        {
            PruneDiceList();
            return _activeDice.ToArray();
        }

        public void ClearAllDice()
        {
            for (int i = _activeDice.Count - 1; i >= 0; i--)
                DestroyDieObject(_activeDice[i]);
            _activeDice.Clear();
            _status = "牌桌已清空";
        }

        private DemoCDie CreateDie(int level, Vector3 position, Quaternion rotation)
        {
            DemoCDie template = ResolveDiceTemplate();
            if (template == null) return null;
            PruneDiceList();
            if (_activeDice.Count >= MaximumDice) return null;

            Transform parent = _diceRoot == null ? transform : _diceRoot;
            DemoCDie die = Instantiate(template, position, rotation, parent);
            die.name = "场上骰子 " + _nextIdentifier;
            die.Initialize(this, _nextIdentifier, Mathf.Clamp(level, 1, 9));
            die.gameObject.SetActive(true);
            _nextIdentifier++;
            _activeDice.Add(die);
            return die;
        }

        private void CreateInitialLayout()
        {
            if (ActiveDiceCount > 0) return;
            SpawnPlacedDie(1, new Vector3(-2.2f, 0.78f, 0.2f));
            SpawnPlacedDie(2, new Vector3(0f, 0.78f, 0.2f));
            SpawnPlacedDie(3, new Vector3(2.2f, 0.78f, 0.2f));
            _status = "三枚骰子已稳定占位";
        }

        private IEnumerator RunSmokeTest()
        {
            StartMergeDemonstration();
            float deadline = Time.time + 5f;
            while (_mergeCount == 0 && Time.time < deadline) yield return null;
            bool success = _mergeCount == 1 && ActiveDiceCount == 1 && _activeDice[0].Level == 2 && _collisionCount > 0;
            if (success) Debug.Log("DEMO_C_SMOKE_OK 碰撞合并=1，结果等级=2，场上骰子=1");
            else Debug.LogError("DEMO_C_SMOKE_FAILED 合并=" + _mergeCount + "，碰撞=" + _collisionCount + "，场上=" + ActiveDiceCount);
            yield return new WaitForSeconds(0.25f);
            Application.Quit(success ? 0 : 3);
        }

        private void RemoveDie(DemoCDie die)
        {
            if (die == null) return;
            _activeDice.Remove(die);
            DestroyDieObject(die);
        }

        private static void DestroyDieObject(DemoCDie die)
        {
            if (die == null) return;
            die.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(die.gameObject);
            else DestroyImmediate(die.gameObject);
        }

        private void PruneDiceList()
        {
            for (int i = _activeDice.Count - 1; i >= 0; i--)
                if (_activeDice[i] == null) _activeDice.RemoveAt(i);
        }

        private int CountSettledDice()
        {
            int count = 0;
            foreach (DemoCDie die in GetDiceSnapshot()) if (die.IsSettled) count++;
            return count;
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ToChineseLevel(int level)
        {
            string[] values = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            return level >= 1 && level <= values.Length ? values[level - 1] + "级" : level + "级";
        }

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null) return;
            _panelTexture = MakeTexture(new Color(0.025f, 0.045f, 0.065f, 0.94f));
            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTexture } };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.86f, 0.4f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, wordWrap = true, normal = { textColor = new Color(0.78f, 0.88f, 0.92f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 0.9f, 0.45f) }
            };
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
            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float width = Mathf.Min(500f, Screen.width * 0.39f);
            Rect panel = new Rect(margin, margin, width, 420f);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, width - 36f, 38f), "演示阶段丙　多骰碰撞与合并", _titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 53f, width - 36f, 44f),
                "交付目标：连续投掷、稳定占位、骰间真实碰撞，并完成同级合并", _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 102f, width - 36f, 25f),
                "当前选择：" + ToChineseLevel(_selectedLevel) + "　场上骰子：" + ActiveDiceCount + "／十二", _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 130f, width - 36f, 25f),
                "已停稳：" + CountSettledDice() + "　投掷次数：" + _throwCount, _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 158f, width - 36f, 25f),
                "碰撞次数：" + _collisionCount + "　合并次数：" + _mergeCount + "　掉落次数：" + _dropCount, _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 187f, width - 36f, 25f), "当前状态：" + _status, _labelStyle);

            float buttonWidth = (width - 46f) / 2f;
            if (GUI.Button(new Rect(panel.x + 14f, panel.y + 226f, buttonWidth, 46f), "投掷选中等级", _buttonStyle)) ThrowSelectedDie();
            if (GUI.Button(new Rect(panel.x + 28f + buttonWidth, panel.y + 226f, buttonWidth, 46f), "同级碰撞合并", _buttonStyle)) StartMergeDemonstration();
            if (GUI.Button(new Rect(panel.x + 14f, panel.y + 282f, buttonWidth, 46f), "降低投掷等级", _buttonStyle)) ChangeSelectedLevel(-1);
            if (GUI.Button(new Rect(panel.x + 28f + buttonWidth, panel.y + 282f, buttonWidth, 46f), "提高投掷等级", _buttonStyle)) ChangeSelectedLevel(1);
            if (GUI.Button(new Rect(panel.x + 14f, panel.y + 338f, width - 28f, 42f), "清空牌桌", _buttonStyle)) ClearAllDice();
            GUI.Label(new Rect(panel.x + 18f, panel.y + 386f, width - 36f, 24f),
                "空格键投掷　左右方向键切换等级　回车键合并演示　删除键清空", _smallStyle);
        }
    }

}
