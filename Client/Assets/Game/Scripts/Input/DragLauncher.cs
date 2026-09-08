using UnityEngine;
using UnityEngine.EventSystems;

namespace RuneDice.Game
{
    public sealed class DragLauncher : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float launchPower = 3.2f;
        [SerializeField] private float maxDragWorldDistance = 3f;
        private bool dragging;
        private int activePointerId;
        private Vector2 dragStart;

        private void Update()
        {
            if (controller == null || !controller.CanLaunch) return;
            if (TryPointerDown(out var down, out var pointerId))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId)) return;
                dragging = true;
                activePointerId = pointerId;
                dragStart = ScreenToWorld(down);
            }
            if (dragging && TryPointerUp(activePointerId, out var up))
            {
                dragging = false;
                Vector2 release = ScreenToWorld(up);
                Vector2 impulse = Vector2.ClampMagnitude(dragStart - release, maxDragWorldDistance) * launchPower;
                if (impulse.sqrMagnitude > 0.04f) controller.LaunchDice(impulse);
            }
        }

        private Vector2 ScreenToWorld(Vector2 screen) => worldCamera.ScreenToWorldPoint(screen);

        private static bool TryPointerDown(out Vector2 position, out int pointerId)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began) { position = touch.position; pointerId = touch.fingerId; return true; }
            }
            position = Input.mousePosition;
            pointerId = -1;
            return Input.GetMouseButtonDown(0);
        }

        private static bool TryPointerUp(int pointerId, out Vector2 position)
        {
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId == pointerId && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                    { position = touch.position; return true; }
                }
                position = default;
                return false;
            }
            position = Input.mousePosition;
            return Input.GetMouseButtonUp(0);
        }
    }
}
