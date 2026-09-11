using System;
using System.Collections;
using UnityEngine;

namespace DiceDemo.DemoA
{
    /// <summary>
    /// Demo-A 的牌桌验收运行时。这里只负责牌桌环境与数据展示，不承载骰子玩法。
    /// </summary>
    public sealed class DemoADeskRuntime : MonoBehaviour
    {
        public const float OriginalGravity = -30f;
        public const float OriginalCameraSize = 12f;

        [SerializeField] private Transform _spawnPointRoot;
        [SerializeField] private Transform _frontDropTrigger;
        [SerializeField] private int _spawnPointCount;
        [SerializeField] private int _staticBoundaryCount;
        [SerializeField] private int _dropTriggerCount;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private Texture2D _panelTexture;
        private int _dropCount;
        private string _lastDrop = "暂无";

        public Transform SpawnPointRoot => _spawnPointRoot;
        public int SpawnPointCount => _spawnPointCount;
        public int StaticBoundaryCount => _staticBoundaryCount;
        public int DropTriggerCount => _dropTriggerCount;
        public int DropCount => _dropCount;
        public Camera DeskCamera { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics.gravity = new Vector3(0f, OriginalGravity, 0f);
            Physics.defaultMaxAngularSpeed = 8f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            DeskCamera = Camera.main;
        }

        private IEnumerator Start()
        {
            // 等一帧再检查，确保所有 AfterSceneLoad 启动器都已经执行。
            yield return null;
            Rigidbody2D[] legacyBodies = FindObjectsOfType<Rigidbody2D>();
            Camera[] cameras = FindObjectsOfType<Camera>();
            if (legacyBodies.Length > 0)
            {
                Debug.LogError("DEMO_A_RUNTIME_FAILED 检测到旧二维刚体数量=" + legacyBodies.Length);
            }
            else if (cameras.Length != 1 || Camera.main == null || !Camera.main.orthographic)
            {
                Debug.LogError("DEMO_A_RUNTIME_FAILED 相机数量=" + cameras.Length);
            }
            else
            {
                Debug.Log("DEMO_A_RUNTIME_OK 三维牌桌已独立启动，二维刚体=0，相机=1");
            }

            string capturePath = GetArgument("-demoACapturePath");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(capturePath);
                Debug.Log("DEMO_A_CAPTURE_REQUESTED path=" + capturePath);
            }
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public void Configure(Transform spawnPointRoot, Transform frontDropTrigger, int spawnPointCount,
            int staticBoundaryCount, int dropTriggerCount)
        {
            _spawnPointRoot = spawnPointRoot;
            _frontDropTrigger = frontDropTrigger;
            _spawnPointCount = spawnPointCount;
            _staticBoundaryCount = staticBoundaryCount;
            _dropTriggerCount = dropTriggerCount;
        }

        public void RegisterDrop(GameObject droppedObject)
        {
            _dropCount++;
            _lastDrop = droppedObject == null ? "未知物体" : droppedObject.name;
        }

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelTexture = MakeTexture(new Color(0.025f, 0.045f, 0.065f, 0.92f));
            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTexture } };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.4f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.82f, 0.9f, 0.95f) }
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
            float width = Mathf.Min(470f, Screen.width * 0.36f);
            float height = 282f;
            Rect panel = new Rect(margin, margin, width, height);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, width - 36f, 38f),
                "Demo-A  原作牌桌基线", _titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 58f, width - 36f, 25f),
                "阶段目标：验证牌桌空间与碰撞边界", _labelStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 96f, width - 36f, 22f),
                "牌桌：森林桌面（原作资源）", _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 122f, width - 36f, 22f),
                "相机：正交，尺寸 " + OriginalCameraSize.ToString("0.0"), _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 148f, width - 36f, 22f),
                "重力：0，" + OriginalGravity.ToString("0.0") + "，0", _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 174f, width - 36f, 22f),
                "静态边界：" + _staticBoundaryCount + " 个    掉落触发器：" + _dropTriggerCount + " 个", _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 200f, width - 36f, 22f),
                "预生成点：" + _spawnPointCount + " 个    掉落检测：" + _dropCount + " 次", _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 226f, width - 36f, 22f),
                "最近掉落：" + _lastDrop, _smallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 252f, width - 36f, 22f),
                "当前阶段不生成骰子，下一阶段接入三维骰子", _smallStyle);
        }
    }

    public sealed class DemoAFallDetector : MonoBehaviour
    {
        private DemoADeskRuntime _desk;

        public void Configure(DemoADeskRuntime desk)
        {
            _desk = desk;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_desk != null && other.attachedRigidbody != null)
            {
                _desk.RegisterDrop(other.attachedRigidbody.gameObject);
            }
        }
    }
}
