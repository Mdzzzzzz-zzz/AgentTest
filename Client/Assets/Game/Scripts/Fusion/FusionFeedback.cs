using System.Collections;
using UnityEngine;

namespace RuneDice.Game
{
    public static class FusionFeedback
    {
        private static readonly System.Collections.Generic.HashSet<GameObject> active = new System.Collections.Generic.HashSet<GameObject>();
        public static void ClearAll()
        {
            foreach (var marker in active) if (marker != null) Object.DestroyImmediate(marker);
            active.Clear();
        }
        public static IEnumerator Show(Vector2 position, float radius)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "FusionRangeFeedback";
            active.Add(marker);
            marker.transform.position = new Vector3(position.x, position.y, 1f);
            marker.transform.localScale = Vector3.one * radius * 2f;
            Object.Destroy(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.material.color = new Color(1f, .8f, .1f, .28f);
            float elapsed = 0f;
            while (elapsed < .18f)
            {
                elapsed += Time.deltaTime;
                marker.transform.localScale = Vector3.one * radius * 2f * (1f + elapsed);
                yield return null;
            }
            active.Remove(marker);
            Object.Destroy(marker);
        }
    }
}
