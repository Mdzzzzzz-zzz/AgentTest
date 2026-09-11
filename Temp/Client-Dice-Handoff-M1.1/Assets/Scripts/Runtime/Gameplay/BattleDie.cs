using System;
using UnityEngine;

namespace DiceDemo.M1
{
    public sealed class BattleDie : MonoBehaviour
    {
        [SerializeField] private Rigidbody _body;
        [SerializeField] private BoxCollider _collider;
        [SerializeField] private Transform[] _visualVariants;

        private BattleFlow _flow;
        private float _protectedUntil;
        private bool _hasMoved;
        private bool _isSettled;
        private float _stillTime;

        public DiceKind Kind { get; private set; }
        public int Level { get; private set; }
        public bool IsMergeLocked { get; set; }
        public bool KeepMovingForAcceptance { get; set; }
        public Rigidbody Body => _body;
        public BoxCollider Collider => _collider;
        public Transform[] VisualVariants => _visualVariants ?? Array.Empty<Transform>();
        public bool IsProtected => Time.time < _protectedUntil;
        public bool IsSettled => _isSettled;

        public void ConfigureTemplate(Rigidbody body, BoxCollider collider, Transform[] visualVariants)
        {
            _body = body;
            _collider = collider;
            _visualVariants = visualVariants;
            SetLevel(1);
        }

        public void Initialize(BattleFlow flow, DiceKind kind, int level, bool protectFromMerge)
        {
            _flow = flow;
            Kind = kind;
            Level = Mathf.Clamp(level, 1, BattleBalance.MaxDiceLevel);
            if (_body != null)
            {
                _body.mass = 10f;
                _body.drag = 0.005f;
                _body.angularDrag = 0.05f;
                _body.useGravity = true;
                _body.interpolation = RigidbodyInterpolation.Interpolate;
                _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _body.maxAngularVelocity = 8f;
            }
            _hasMoved = false;
            _isSettled = false;
            _stillTime = 0f;
            SetLevel(Level);
            _protectedUntil = protectFromMerge ? Time.time + BattleBalance.MergeProtectionTime : 0f;
        }

        private void Update()
        {
            if (_body == null || IsMergeLocked) return;
            if (_body.position.y < -3f)
            {
                _flow.RegisterDrop(this);
                return;
            }

            if (_body.velocity.magnitude > 0.10f || _body.angularVelocity.magnitude > 0.18f)
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
            Level = Mathf.Clamp(level, 1, BattleBalance.MaxDiceLevel);
            if (_visualVariants == null) return;
            for (int i = 0; i < _visualVariants.Length; i++)
                if (_visualVariants[i] != null) _visualVariants[i].gameObject.SetActive(i == Level - 1);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsMergeLocked) return;
            _hasMoved = true;
            _isSettled = false;
            _stillTime = 0f;
            BattleDie other = collision.collider.GetComponentInParent<BattleDie>();
            if (other != null && GetInstanceID() < other.GetInstanceID()) _flow.TryMerge(this, other);
        }
    }
}
