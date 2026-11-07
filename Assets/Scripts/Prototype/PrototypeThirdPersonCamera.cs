using UnityEngine;

namespace Prototype
{
    public sealed class PrototypeThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _target;
        [Header("Orbit")]
        [SerializeField, Min(0.1f)] private float _distance = 6f;
        [SerializeField] private float _targetHeight = 1f;
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minimumPitch = -15f;
        [SerializeField] private float _maximumPitch = 75f;
        [SerializeField] private float _initialPitch = 25f;
        [Header("Obstruction")]
        [SerializeField] private LayerMask _obstructionMask = ~0;
        [SerializeField] private float _collisionRadius = 0.25f;
        [SerializeField] private float _collisionPadding = 0.1f;
        private readonly RaycastHit[] _obstructionHits = new RaycastHit[32];
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _yaw = transform.eulerAngles.y;
            _pitch = _initialPitch;
        }

        private void OnEnable()
        {
            SetCursorCaptured(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorCaptured(false);
            else if (Application.isFocused && Input.GetMouseButtonDown(0))
                SetCursorCaptured(true);

            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked)
                ApplyOrbit(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));
        }

        public void ApplyOrbit(Vector2 mouseDelta)
        {
            _yaw = Mathf.Repeat(_yaw + mouseDelta.x * _mouseSensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * _mouseSensitivity, _minimumPitch, _maximumPitch);
        }

        private void LateUpdate()
        {
            FollowTarget();
        }

        public void FollowTarget()
        {
            if (_target == null)
                return;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = _target.position + Vector3.up * _targetHeight;
            Vector3 direction = -(rotation * Vector3.forward);
            float distance = _distance;
            int count = Physics.SphereCastNonAlloc(pivot, _collisionRadius, direction, _obstructionHits,
                _distance, _obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _obstructionHits[i];
                if (hit.collider != null && !hit.collider.transform.IsChildOf(_target))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - _collisionPadding));
            }
            // Direct following also snaps correctly after player respawns.
            transform.SetPositionAndRotation(pivot + direction * distance, rotation);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            SetCursorCaptured(false);
        }

        private static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
