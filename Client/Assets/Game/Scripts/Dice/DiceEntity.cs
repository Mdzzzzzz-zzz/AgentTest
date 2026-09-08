using UnityEngine;

namespace RuneDice.Game
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public sealed class DiceEntity : MonoBehaviour
    {
        [SerializeField] private DiceType type = DiceType.Ember;
        [SerializeField, Min(1)] private int level = 1;
        private bool pendingRemoval;
        private GameController controller;

        public DiceData Data => new DiceData(type, level);
        public bool IsPendingRemoval => pendingRemoval;

        public void Initialize(GameController owner, DiceData data)
        {
            controller = owner;
            type = data.Type;
            level = data.Level;
            pendingRemoval = false;
            gameObject.name = $"Dice_{type}_L{level}";
            transform.localScale = Vector3.one * (0.72f + Mathf.Min(level, 5) * 0.08f);
            GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(1f, .35f, .12f), Color.yellow, Mathf.Clamp01((level - 1) * .2f));
        }

        public void MarkPendingRemoval()
        {
            pendingRemoval = true;
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (pendingRemoval || controller == null) return;
            var other = collision.collider.GetComponent<DiceEntity>();
            if (other != null) controller.ReportDiceCollision(this, other);
        }
    }
}
