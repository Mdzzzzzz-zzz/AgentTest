using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiceDemo
{
    /// <summary>
    /// A self-contained physics merge demo. It deliberately does not depend on
    /// any of the decompiled Rune Dice scripts.
    /// </summary>
    public sealed class DiceMergeGame : MonoBehaviour
    {
        private const int MaxLevel = 6;
        private const int MaxDice = 42;
        private const float Left = -8.35f;
        private const float Right = 8.35f;
        private const float Bottom = -4.35f;
        private const float Top = 4.25f;

        private readonly Texture2D[] _diceTextures = new Texture2D[MaxLevel];
        private readonly Sprite[] _diceSprites = new Sprite[MaxLevel];

        private Camera _camera;
        private Texture2D _logo;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _buttonStyle;
        private int _score;
        private int _bestLevel = 1;
        private int _nextLevel = 1;
        private int _combo;
        private float _lastMergeTime;
        private bool _gameOver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (SceneManager.GetActiveScene().name == "DiceMergeDemo" && FindObjectOfType<DiceMergeGame>() == null)
            {
                new GameObject("Dice Merge Demo").AddComponent<DiceMergeGame>();
            }
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics2D.gravity = new Vector2(0f, -3.7f);

            LoadArt();
            CreateCameraAndBackground();
            CreateWalls();
            ResetGame();
        }

        private void LoadArt()
        {
            for (int i = 0; i < MaxLevel; i++)
            {
                Texture2D texture = Resources.Load<Texture2D>($"DiceDemo/Dice_Full_{i + 1}");
                _diceTextures[i] = texture;
                if (texture != null)
                {
                    _diceSprites[i] = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        texture.width);
                }
            }

            _logo = Resources.Load<Texture2D>("DiceDemo/Rune_Dice_Logo_01");
        }

        private void CreateCameraAndBackground()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5.25f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.025f, 0.045f, 0.07f);

            Texture2D backgroundTexture = Resources.Load<Texture2D>("DiceDemo/Main_Menu_Background_01");
            if (backgroundTexture == null)
            {
                return;
            }

            Sprite backgroundSprite = Sprite.Create(
                backgroundTexture,
                new Rect(0f, 0f, backgroundTexture.width, backgroundTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            GameObject background = new GameObject("Rune Dice Background");
            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = backgroundSprite;
            renderer.sortingOrder = -100;
            renderer.color = new Color(0.53f, 0.62f, 0.72f, 1f);

            float worldHeight = _camera.orthographicSize * 2f;
            float worldWidth = worldHeight * _camera.aspect;
            float scale = Mathf.Max(
                worldWidth / backgroundSprite.bounds.size.x,
                worldHeight / backgroundSprite.bounds.size.y);
            background.transform.localScale = Vector3.one * scale;
        }

        private static void CreateWall(string name, Vector2 position, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.position = position;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void CreateWalls()
        {
            CreateWall("Floor", new Vector2(0f, Bottom - 0.2f), new Vector2(18f, 0.4f));
            CreateWall("Ceiling", new Vector2(0f, Top + 0.2f), new Vector2(18f, 0.4f));
            CreateWall("Left Wall", new Vector2(Left - 0.2f, 0f), new Vector2(0.4f, 9f));
            CreateWall("Right Wall", new Vector2(Right + 0.2f, 0f), new Vector2(0.4f, 9f));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetGame();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                SpawnNext(new Vector2(Random.Range(-6.5f, 6.5f), 3.4f));
            }

            // IMGUI is drawn from the top while mouse coordinates start at the bottom.
            bool pointerBelowHeader = Input.mousePosition.y < Screen.height - 170f;
            if (Input.GetMouseButtonDown(0) && pointerBelowHeader && !_gameOver)
            {
                Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector2 position = new Vector2(
                    Mathf.Clamp(world.x, Left + 0.7f, Right - 0.7f),
                    Mathf.Clamp(world.y, Bottom + 0.8f, Top - 0.8f));
                SpawnNext(position);
            }
        }

        private void ResetGame()
        {
            foreach (DemoDie die in FindObjectsOfType<DemoDie>())
            {
                Destroy(die.gameObject);
            }

            foreach (MergePulse pulse in FindObjectsOfType<MergePulse>())
            {
                Destroy(pulse.gameObject);
            }

            _score = 0;
            _bestLevel = 1;
            _nextLevel = Random.Range(1, 3);
            _combo = 0;
            _gameOver = false;
            StartCoroutine(SeedBoard());
        }

        private IEnumerator SeedBoard()
        {
            yield return null;
            int[] levels = { 1, 1, 2, 2, 1, 3, 3, 2 };
            for (int i = 0; i < levels.Length; i++)
            {
                Vector2 position = new Vector2(-5.5f + i * 1.55f, 1.8f + (i % 2) * 0.8f);
                SpawnDie(levels[i], position, new Vector2(Random.Range(-1.3f, 1.3f), 0f));
            }
        }

        private void SpawnNext(Vector2 position)
        {
            if (_gameOver)
            {
                return;
            }

            if (FindObjectsOfType<DemoDie>().Length >= MaxDice)
            {
                _gameOver = true;
                return;
            }

            SpawnDie(_nextLevel, position, Random.insideUnitCircle * 2.4f + Vector2.up * 1.5f);
            _nextLevel = Random.value < 0.72f ? 1 : 2;
        }

        private DemoDie SpawnDie(int level, Vector2 position, Vector2 velocity)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            GameObject dieObject = new GameObject($"Dice {level}");
            dieObject.transform.position = position;
            dieObject.transform.localScale = Vector3.one * 1.18f;

            SpriteRenderer renderer = dieObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _diceSprites[level - 1];
            renderer.sortingOrder = level;

            Rigidbody2D body = dieObject.AddComponent<Rigidbody2D>();
            body.mass = 0.85f + level * 0.08f;
            body.drag = 0.14f;
            body.angularDrag = 0.35f;
            body.gravityScale = 1f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.velocity = velocity;
            body.angularVelocity = Random.Range(-170f, 170f);

            CircleCollider2D collider = dieObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.43f;

            DemoDie die = dieObject.AddComponent<DemoDie>();
            die.Initialize(this, level);
            return die;
        }

        internal void TryMerge(DemoDie first, DemoDie second)
        {
            if (first == null || second == null || first.IsMerging || second.IsMerging)
            {
                return;
            }

            if (first.Level != second.Level)
            {
                return;
            }

            first.IsMerging = true;
            second.IsMerging = true;

            Vector2 position = (first.transform.position + second.transform.position) * 0.5f;
            Vector2 velocity = (first.Body.velocity + second.Body.velocity) * 0.5f;
            int mergedLevel = first.Level + 1;

            if (Time.time - _lastMergeTime < 1.35f)
            {
                _combo++;
            }
            else
            {
                _combo = 1;
            }

            _lastMergeTime = Time.time;
            _score += (1 << (first.Level + 1)) * Mathf.Max(1, _combo);
            _bestLevel = Mathf.Max(_bestLevel, Mathf.Min(mergedLevel, MaxLevel));

            CreatePulse(first.Renderer.sprite, position);
            Destroy(first.gameObject);
            Destroy(second.gameObject);

            if (mergedLevel <= MaxLevel)
            {
                DemoDie merged = SpawnDie(mergedLevel, position, velocity + Vector2.up * 2.1f);
                merged.Body.angularVelocity *= 0.45f;
            }
            else
            {
                _score += 512 * Mathf.Max(1, _combo);
            }
        }

        private static void CreatePulse(Sprite sprite, Vector2 position)
        {
            GameObject pulseObject = new GameObject("Merge Pulse");
            pulseObject.transform.position = position;
            SpriteRenderer renderer = pulseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 50;
            renderer.color = new Color(1f, 0.9f, 0.38f, 0.9f);
            pulseObject.AddComponent<MergePulse>();
        }

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelTexture = MakeTexture(new Color(0.02f, 0.035f, 0.065f, 0.88f));
            _buttonTexture = MakeTexture(new Color(0.16f, 0.35f, 0.65f, 0.96f));

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTexture;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.Clamp(Screen.height / 24, 26, 48),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 38, 18, 30),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.38f) }
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 55, 14, 22),
                normal = { textColor = new Color(0.88f, 0.93f, 1f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Clamp(Screen.height / 50, 15, 23),
                fontStyle = FontStyle.Bold,
                normal = { background = _buttonTexture, textColor = Color.white },
                hover = { background = _buttonTexture, textColor = new Color(1f, 0.9f, 0.45f) },
                active = { background = _buttonTexture, textColor = Color.white }
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

            float margin = Mathf.Max(14f, Screen.width * 0.012f);
            float headerHeight = Mathf.Clamp(Screen.height * 0.145f, 115f, 165f);
            GUI.Box(new Rect(margin, margin, Screen.width - margin * 2f, headerHeight), GUIContent.none, _panelStyle);

            float logoHeight = headerHeight - 20f;
            float logoWidth = logoHeight;
            float textX = margin + logoWidth + 28f;
            GUI.Label(new Rect(textX, margin + 12f, 360f, 52f), "骰子合并原型", _titleStyle);
            GUI.Label(new Rect(textX, margin + 68f, 220f, 38f), $"分数  {_score}", _labelStyle);
            GUI.Label(new Rect(textX + 210f, margin + 68f, 220f, 38f), $"最高等级  D{_bestLevel}", _labelStyle);

            float buttonWidth = Mathf.Clamp(Screen.width * 0.11f, 130f, 210f);
            float buttonHeight = Mathf.Clamp(headerHeight * 0.42f, 48f, 66f);
            float buttonY = margin + (headerHeight - buttonHeight) * 0.5f;
            float resetX = Screen.width - margin - buttonWidth - 18f;
            float spawnX = resetX - buttonWidth - 14f;

            if (GUI.Button(new Rect(spawnX, buttonY, buttonWidth, buttonHeight), $"投掷  D{_nextLevel}", _buttonStyle))
            {
                SpawnNext(new Vector2(Random.Range(-5.5f, 5.5f), 3.3f));
            }

            if (GUI.Button(new Rect(resetX, buttonY, buttonWidth, buttonHeight), "重新开始  [R]", _buttonStyle))
            {
                ResetGame();
            }

            Rect helpRect = new Rect(margin, Screen.height - 92f - margin, 520f, 92f);
            GUI.Box(helpRect, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(helpRect.x + 15f, helpRect.y + 10f, helpRect.width - 30f, 30f),
                "点击场地或按空格键投掷骰子", _smallStyle);
            GUI.Label(new Rect(helpRect.x + 15f, helpRect.y + 43f, helpRect.width - 30f, 30f),
                "相同等级骰子碰撞后会合并升级", _smallStyle);

            if (_combo > 1 && Time.time - _lastMergeTime < 1.2f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 100f, headerHeight + 40f, 200f, 60f),
                    $"连击 x{_combo}", new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter });
            }

            if (_gameOver)
            {
                Rect over = new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.5f - 100f, 500f, 200f);
                GUI.Box(over, GUIContent.none, _panelStyle);
                GUI.Label(new Rect(over.x, over.y + 25f, over.width, 70f), "场地已满",
                    new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(over.x + 130f, over.y + 115f, 240f, 58f), "再玩一次", _buttonStyle))
                {
                    ResetGame();
                }
            }
        }
    }

    public sealed class DemoDie : MonoBehaviour
    {
        private DiceMergeGame _game;

        public int Level { get; private set; }
        public bool IsMerging { get; set; }
        public Rigidbody2D Body { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public void Initialize(DiceMergeGame game, int level)
        {
            _game = game;
            Level = level;
            Body = GetComponent<Rigidbody2D>();
            Renderer = GetComponent<SpriteRenderer>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            DemoDie other = collision.gameObject.GetComponent<DemoDie>();
            if (other != null && GetInstanceID() < other.GetInstanceID())
            {
                _game.TryMerge(this, other);
            }
        }
    }

    public sealed class MergePulse : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _age;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one * 0.75f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / 0.38f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 2.25f, t);
            Color color = _renderer.color;
            color.a = 1f - t;
            _renderer.color = color;
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
