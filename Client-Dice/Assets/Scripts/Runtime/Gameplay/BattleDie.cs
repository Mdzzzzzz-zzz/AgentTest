using UnityEngine;

namespace DiceDemo.M1
{
    public sealed class BattleDie : MonoBehaviour
    {
        private BattleFlow _flow;
        private TextMesh _levelLabel;
        private float _protectedUntil;

        public DiceKind Kind { get; private set; }
        public int Level { get; private set; }
        public bool IsMergeLocked { get; set; }
        public bool KeepMovingForAcceptance { get; set; }
        public Rigidbody2D Body { get; private set; }
        public SpriteRenderer SpriteRenderer { get; private set; }
        public bool IsProtected => Time.time < _protectedUntil;

        public void Initialize(BattleFlow flow, DiceKind kind, int level, Sprite sprite, bool protectFromMerge)
        {
            _flow = flow;
            Kind = kind;
            Level = Mathf.Clamp(level, 1, BattleBalance.MaxDiceLevel);
            Body = gameObject.AddComponent<Rigidbody2D>();
            Body.mass = 0.9f + Level * 0.08f;
            Body.drag = 0.22f;
            Body.angularDrag = 0.48f;
            Body.gravityScale = 1f;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.43f;

            SpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            SpriteRenderer.sprite = sprite;
            SpriteRenderer.sortingOrder = 10 + Level;

            CreateLevelLabel();
            _protectedUntil = protectFromMerge ? Time.time + BattleBalance.MergeProtectionTime : 0f;
        }

        private void CreateLevelLabel()
        {
            GameObject labelObject = new GameObject("Level");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, -0.02f, -0.2f);
            _levelLabel = labelObject.AddComponent<TextMesh>();
            _levelLabel.text = Level.ToString();
            _levelLabel.anchor = TextAnchor.MiddleCenter;
            _levelLabel.alignment = TextAlignment.Center;
            _levelLabel.fontSize = 56;
            _levelLabel.characterSize = 0.08f;
            _levelLabel.fontStyle = FontStyle.Bold;
            _levelLabel.color = Color.white;
            MeshRenderer meshRenderer = _levelLabel.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 100;
        }

        private void FixedUpdate()
        {
            if (KeepMovingForAcceptance && Body != null)
            {
                Body.AddTorque(2.5f, ForceMode2D.Force);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            BattleDie other = collision.gameObject.GetComponent<BattleDie>();
            if (other != null && GetInstanceID() < other.GetInstanceID())
            {
                _flow.TryMerge(this, other);
            }
        }
    }
}
