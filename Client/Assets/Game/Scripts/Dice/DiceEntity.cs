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
            GetComponent<SpriteRenderer>().color = GetTypeColor(type, level);
            UpdateTechnicalMarker();
        }

        private static Color GetTypeColor(DiceType diceType, int diceLevel)
        {
            Color baseColor = diceType == DiceType.Fire
                ? new Color(1f, .25f, .08f)
                : diceType == DiceType.Lightning
                    ? new Color(.15f, .8f, 1f)
                    : new Color(.42f, .55f, .72f);
            return Color.Lerp(baseColor, Color.white, Mathf.Clamp01((diceLevel - 1) * .12f));
        }

        private void UpdateTechnicalMarker()
        {
            Transform markerTransform = transform.Find("TypeMarker");
            GameObject markerObject = markerTransform == null ? new GameObject("TypeMarker") : markerTransform.gameObject;
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localPosition = new Vector3(0f, 0f, -.1f);
            markerObject.transform.localScale = Vector3.one * .32f;
            TextMesh marker = markerObject.GetComponent<TextMesh>();
            if (marker == null) marker = markerObject.AddComponent<TextMesh>();
            marker.text = type == DiceType.Fire ? "F" : type == DiceType.Lightning ? "L" : "B";
            marker.anchor = TextAnchor.MiddleCenter;
            marker.alignment = TextAlignment.Center;
            marker.fontSize = 48;
            marker.color = Color.white;
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
