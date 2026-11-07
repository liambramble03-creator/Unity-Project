using UnityEngine;

namespace Prototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PrototypeMovingPlatform : MonoBehaviour
    {
        public enum MotionMode { Smooth, AbruptReversal }
        [SerializeField] private Vector3 _travel = new Vector3(4f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float _period = 4f;
        [SerializeField] private MotionMode _mode;
        private Vector3 _origin;
        private float _elapsed;
        private bool _initialized;
        public Vector3 Velocity { get; private set; }
        public Vector3 PreviousVelocity { get; private set; }
        public Vector3 Displacement { get; private set; }
        public float Elapsed => _elapsed;

        private void Awake() { Initialize(); }

        private void Initialize()
        {
            if (_initialized) return;
            _origin = transform.position;
            _initialized = true;
        }

        // Called once per shared player/platform substep, before the player's collision queries.
        public void Advance(float deltaTime)
        {
            Initialize();
            if (!isActiveAndEnabled || deltaTime <= 0f) { Displacement = Vector3.zero; return; }
            PreviousVelocity = Velocity;
            Vector3 before = transform.position;
            _elapsed += deltaTime;
            float phase = Mathf.Repeat(_elapsed / Mathf.Max(0.1f, _period), 1f);
            float fraction = _mode == MotionMode.Smooth
                ? 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f)
                : (phase < 0.5f ? phase * 2f : (1f - phase) * 2f);
            transform.position = _origin + _travel * fraction;
            Displacement = transform.position - before;
            Velocity = _mode == MotionMode.Smooth
                ? _travel * (Mathf.PI / _period) * Mathf.Sin(phase * Mathf.PI * 2f)
                : _travel * ((phase < 0.5f ? 2f : -2f) / _period);
        }

        public void ResetMotion()
        {
            Initialize();
            _elapsed = 0f;
            transform.position = _origin;
            Velocity = PreviousVelocity = Displacement = Vector3.zero;
        }
    }
}
