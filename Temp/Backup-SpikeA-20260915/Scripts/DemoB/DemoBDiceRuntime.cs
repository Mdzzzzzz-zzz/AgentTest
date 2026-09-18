using System;
using System.Collections;
using UnityEngine;

namespace DiceDemo.DemoB
{
    /// <summary>
    /// 演示阶段乙：单枚原作三维骰子的真实刚体投掷、碰撞和停稳识别。
    /// </summary>
    public sealed class DemoBDiceRuntime : MonoBehaviour
    {
        public const float OriginalGravity = -30f;
        public const float OriginalCameraSize = 12f;
        public const float OriginalMass = 10f;
        public const float OriginalLinearDamping = 0.005f;
        public const float OriginalAngularDamping = 0.05f;
        public static readonly Vector3 OriginalColliderSize = new Vector3(1.3f, 1.3f, 1.3f);

        [SerializeField] private Rigidbody _diceBody;
        [SerializeField] private BoxCollider _diceCollider;
        [SerializeField] private Transform[] _visualVariants;
        [SerializeField] private Transform _spawnPoint;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Texture2D _panelTexture;
        private int _activeLevel;
        private int _collisionCount;
        private int _dropCount;
        private float _stillTime;
        private float _throwTime;
        private bool _hasThrown;
        private bool _isResetting;
        private string _state = "待投掷";
        private int _topFace;

        public Rigidbody DiceBody => _diceBody;
        public BoxCollider DiceCollider => _diceCollider;
        public Transform[] VisualVariants => _visualVariants;
        public int ActiveLevel => _activeLevel + 1;
        public int CollisionCount => _collisionCount;
        public int DropCount => _dropCount;
        public int TopFace => _topFace;
        public string StateText => _state;

        public void Configure(Rigidbody diceBody, BoxCollider diceCollider, Transform[] visualVariants, Transform spawnPoint)
        {
            _diceBody = diceBody;
            _diceCollider = diceCollider;
            _visualVariants = visualVariants;
            _spawnPoint = spawnPoint;
            SetLevel(1);
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics.gravity = new Vector3(0f, OriginalGravity, 0f);
            Physics.defaultMaxAngularSpeed = 8f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            if (_diceBody != null) _diceBody.maxAngularVelocity = 8f;
        }

        private IEnumerator Start()
        {
            yield return null;
            Rigidbody2D[] legacyBodies = FindObjectsOfType<Rigidbody2D>();
            Camera[] cameras = FindObjectsOfType<Camera>();
            int sideCount = GetActiveSides().Length;
            bool valid = _diceBody != null && _diceCollider != null && _visualVariants != null &&
                         _visualVariants.Length == 9 && sideCount == 6 && legacyBodies.Length == 0 &&
                         cameras.Length == 1 && Camera.main != null && Camera.main.orthographic;
            if (valid)
            {
                Debug.Log("DEMO_B_RUNTIME_OK 原作三维骰子已启动，变体=9，面数=6，二维刚体=0，相机=1");
            }
            else
            {
                Debug.LogError("DEMO_B_RUNTIME_FAILED 变体=" +
                               (_visualVariants == null ? 0 : _visualVariants.Length) +
                               "，面数=" + sideCount + "，二维刚体=" + legacyBodies.Length +
                               "，相机=" + cameras.Length);
            }

            string capturePath = GetArgument("-demoBCapturePath");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                ThrowDice();
                yield return new WaitForSeconds(0.45f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(capturePath);
                Debug.Log("DEMO_B_CAPTURE_REQUESTED path=" + capturePath);
                yield return new WaitForSeconds(0.5f);
                if (HasArgument("-demoBQuitAfterCapture")) Application.Quit();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) ThrowDice();
            if (Input.GetKeyDown(KeyCode.R)) ResetDice();
            if (Input.GetKeyDown(KeyCode.L)) NextLevel();

            if (_diceBody == null) return;

            if (_diceBody.position.y < -4f && !_isResetting)
            {
                _dropCount++;
                _state = "已掉落";
                StartCoroutine(ResetAfterDelay());
                return;
            }

            _topFace = IdentifyTopFace();
            if (!_hasThrown) return;

            float speed = _diceBody.velocity.magnitude;
            float angularSpeed = _diceBody.angularVelocity.magnitude;
            if (Time.time - _throwTime < 0.18f || _diceBody.position.y > 1.05f)
            {
                _state = "飞行中";
                _stillTime = 0f;
            }
            else if (speed > 0.10f || angularSpeed > 0.18f)
            {
                _state = "滚动中";
                _stillTime = 0f;
            }
            else
            {
                _stillTime += Time.deltaTime;
                if (_stillTime >= 0.75f)
                {
                    _state = "已停稳";
                    _hasThrown = false;
                    _diceBody.Sleep();
                }
            }
        }

        public void ThrowDice()
        {
            if (_diceBody == null) return;

            Vector3 spawn = _spawnPoint == null ? new Vector3(0f, 3.2f, 5f) : _spawnPoint.position;
            _diceBody.position = spawn;
            _diceBody.rotation = Quaternion.Euler(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f));
            _diceBody.velocity = Vector3.zero;
            _diceBody.angularVelocity = Vector3.zero;
            _diceBody.WakeUp();

            Vector3 impulse = new Vector3(UnityEngine.Random.Range(-16f, 16f), 32f, 45f);
            Vector3 spin = new Vector3(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f));
            if (spin.sqrMagnitude < 18f) spin += new Vector3(5f, -4f, 6f);
            _diceBody.AddForce(impulse, ForceMode.Impulse);
            _diceBody.AddTorque(spin, ForceMode.VelocityChange);
            // 编辑器验收不推进物理帧；播放器中则完全交给 AddTorque 和物理解算。
            if (!Application.isPlaying)
                _diceBody.angularVelocity = Vector3.ClampMagnitude(spin, Physics.defaultMaxAngularSpeed);

            _collisionCount = 0;
            _stillTime = 0f;
            _throwTime = Time.time;
            _hasThrown = true;
            _isResetting = false;
            _state = "飞行中";
            _topFace = IdentifyTopFace();
        }

        public void ResetDice()
        {
            if (_diceBody == null) return;
            _isResetting = false;
            _hasThrown = false;
            _stillTime = 0f;
            _diceBody.velocity = Vector3.zero;
            _diceBody.angularVelocity = Vector3.zero;
            _diceBody.position = _spawnPoint == null ? new Vector3(0f, 3.2f, 5f) : _spawnPoint.position;
            _diceBody.rotation = Quaternion.identity;
            _diceBody.WakeUp();
            _state = "待投掷";
            _topFace = IdentifyTopFace();
        }

        public void NextLevel()
        {
            SetLevel((_activeLevel + 1) % 9 + 1);
        }

        public void SetLevel(int level)
        {
            if (_visualVariants == null || _visualVariants.Length == 0) return;
            _activeLevel = Mathf.Clamp(level - 1, 0, _visualVariants.Length - 1);
            for (int i = 0; i < _visualVariants.Length; i++)
            {
                if (_visualVariants[i] != null) _visualVariants[i].gameObject.SetActive(i == _activeLevel);
            }
            _topFace = IdentifyTopFace();
        }

        public Transform[] GetActiveSides()
        {
            if (_visualVariants == null || _visualVariants.Length == 0 ||
                _activeLevel < 0 || _activeLevel >= _visualVariants.Length || _visualVariants[_activeLevel] == null)
                return Array.Empty<Transform>();

            Transform variant = _visualVariants[_activeLevel];
            Transform[] found = new Transform[6];
            foreach (Transform child in variant.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Side ", StringComparison.Ordinal)) continue;
                if (int.TryParse(child.name.Substring(5), out int value) && value >= 1 && value <= 6)
                    found[value - 1] = child;
            }
            return found;
        }

        public int IdentifyTopFace()
        {
            Transform[] sides = GetActiveSides();
            float bestDot = float.NegativeInfinity;
            int bestFace = 0;
            for (int i = 0; i < sides.Length; i++)
            {
                Transform side = sides[i];
                if (side == null) continue;
                Transform numberText = FindChild(side, "Number Text");
                Vector3 direction = numberText == null
                    ? side.TransformDirection(Vector3.back)
                    : numberText.position - transform.position;
                float dot = Vector3.Dot(direction.normalized, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestFace = i + 1;
                }
            }
            return bestFace;
        }

        public void RegisterDrop(Collider other)
        {
            if (other == null || other.attachedRigidbody != _diceBody || _isResetting) return;
            _dropCount++;
            _state = "已掉落";
            StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            _isResetting = true;
            yield return new WaitForSeconds(0.65f);
            ResetDice();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasThrown) _collisionCount++;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
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

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null) return;
            _panelTexture = MakeTexture(new Color(0.025f, 0.045f, 0.065f, 0.92f));
            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTexture } };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.4f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17, normal = { textColor = Color.white }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
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

        private static string ToChineseLevel(int value)
        {
            string[] values = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            return value >= 1 && value <= values.Length ? values[value - 1] + "级" : value + "级";
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float width = Mathf.Min(440f, Screen.width * 0.34f);
            Rect panel = new Rect(margin, margin, width, 330f);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, width - 36f, 38f),
                "演示阶段乙　原作三维骰子", _titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 55f, width - 36f, 25f),
                "交付目标：单枚骰子可真实投掷并识别朝上面", _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 88f, width - 36f, 24f),
                "当前等级：" + ToChineseLevel(ActiveLevel) + "　当前状态：" + _state, _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 116f, width - 36f, 24f),
                "线速度：" + (_diceBody == null ? "零" : _diceBody.velocity.magnitude.ToString("0.00")), _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 144f, width - 36f, 24f),
                "角速度：" + (_diceBody == null ? "零" : _diceBody.angularVelocity.magnitude.ToString("0.00")), _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 172f, width - 36f, 24f),
                "当前朝上面：" + (_topFace == 0 ? "识别中" : _topFace.ToString()), _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 200f, width - 36f, 24f),
                "碰撞次数：" + _collisionCount + "　掉落次数：" + _dropCount, _labelStyle);

            float buttonY = panel.y + 242f;
            float buttonWidth = (width - 48f) / 3f;
            if (GUI.Button(new Rect(panel.x + 14f, buttonY, buttonWidth, 48f), "投掷骰子", _buttonStyle)) ThrowDice();
            if (GUI.Button(new Rect(panel.x + 24f + buttonWidth, buttonY, buttonWidth, 48f), "重新放置", _buttonStyle)) ResetDice();
            if (GUI.Button(new Rect(panel.x + 34f + buttonWidth * 2f, buttonY, buttonWidth, 48f), "切换等级", _buttonStyle)) NextLevel();
            GUI.Label(new Rect(panel.x + 18f, panel.y + 298f, width - 36f, 24f),
                "空格投掷　Ｒ键复位　Ｌ键切换等级", _labelStyle);
        }
    }

    public sealed class DemoBFallDetector : MonoBehaviour
    {
        [SerializeField] private DemoBDiceRuntime _runtime;

        public void Configure(DemoBDiceRuntime runtime)
        {
            _runtime = runtime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_runtime != null) _runtime.RegisterDrop(other);
        }
    }
}
