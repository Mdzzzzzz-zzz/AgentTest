using System;
using UnityEngine;

namespace DiceDemo.DemoC
{
    public sealed class DemoCDie : MonoBehaviour
    {
        [SerializeField] private Rigidbody _body;
        [SerializeField] private BoxCollider _collider;
        [SerializeField] private Transform[] _visualVariants;
        [SerializeField] private DemoCDiceRuntime _runtime;
        [SerializeField] private int _identifier;
        [SerializeField] private int _level = 1;

        private bool _hasMoved;
        private bool _isMerging;
        private bool _isSettled;
        private float _stillTime;

        public Rigidbody Body => _body;
        public BoxCollider Collider => _collider;
        public Transform[] VisualVariants => _visualVariants ?? Array.Empty<Transform>();
        public int Identifier => _identifier;
        public int Level => _level;
        public bool IsMerging => _isMerging;
        public bool IsSettled => _isSettled;

        public void ConfigureTemplate(Rigidbody body, BoxCollider collider, Transform[] visualVariants)
        {
            _body = body;
            _collider = collider;
            _visualVariants = visualVariants;
            SetLevel(1);
        }

        public void Initialize(DemoCDiceRuntime runtime, int identifier, int level)
        {
            _runtime = runtime;
            _identifier = identifier;
            _isMerging = false;
            _isSettled = false;
            _hasMoved = false;
            _stillTime = 0f;
            SetLevel(level);
            if (_body != null) _body.maxAngularVelocity = 8f;
        }

        private void Update()
        {
            if (_body == null || _isMerging) return;
            if (_body.position.y < -4f)
            {
                _runtime?.RegisterDrop(this);
                return;
            }

            float speed = _body.velocity.magnitude;
            float angularSpeed = _body.angularVelocity.magnitude;
            if (speed > 0.10f || angularSpeed > 0.18f)
            {
                _hasMoved = true;
                _isSettled = false;
                _stillTime = 0f;
                return;
            }

            if (!_hasMoved || _isSettled) return;
            _stillTime += Time.deltaTime;
            if (_stillTime < 0.75f) return;
            _isSettled = true;
            _body.Sleep();
            _runtime?.RegisterSettled(this);
        }

        public void Launch(Vector3 impulse, Vector3 angularVelocity)
        {
            if (_body == null) return;
            _hasMoved = true;
            _isSettled = false;
            _stillTime = 0f;
            _body.velocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
            _body.AddForce(impulse, ForceMode.Impulse);
            _body.AddTorque(angularVelocity, ForceMode.VelocityChange);
            if (!Application.isPlaying)
            {
                _body.velocity = impulse / Mathf.Max(_body.mass, 0.0001f);
                _body.angularVelocity = Vector3.ClampMagnitude(angularVelocity, Physics.defaultMaxAngularSpeed);
            }
        }

        public void SetMotion(Vector3 velocity, Vector3 angularVelocity)
        {
            if (_body == null) return;
            _hasMoved = true;
            _isSettled = false;
            _stillTime = 0f;
            _body.velocity = velocity;
            _body.angularVelocity = angularVelocity;
            _body.WakeUp();
        }

        public void PlaceAndSleep()
        {
            if (_body == null) return;
            _hasMoved = true;
            _isSettled = true;
            _stillTime = 0.75f;
            _body.velocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.Sleep();
        }

        public void MarkMerging()
        {
            _isMerging = true;
        }

        public int CountFaces(int level)
        {
            if (_visualVariants == null || level < 1 || level > _visualVariants.Length || _visualVariants[level - 1] == null) return 0;
            int count = 0;
            foreach (Transform child in _visualVariants[level - 1].GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Side ", StringComparison.Ordinal)) count++;
            return count;
        }

        private void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 1, 9);
            if (_visualVariants == null) return;
            for (int i = 0; i < _visualVariants.Length; i++)
                if (_visualVariants[i] != null) _visualVariants[i].gameObject.SetActive(i == _level - 1);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isMerging) return;
            _isSettled = false;
            _hasMoved = true;
            _stillTime = 0f;
            _runtime?.RegisterCollision(this, collision);
        }
    }
}
