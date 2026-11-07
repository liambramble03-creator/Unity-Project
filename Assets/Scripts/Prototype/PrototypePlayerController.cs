using UnityEngine;

namespace Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _movementCamera;
        [SerializeField] private Animator _animator;
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int JumpParameter = Animator.StringToHash("Jump");
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [SerializeField, Min(0f)] private float _turnSpeed = 720f;
        [SerializeField, Min(0f)] private float _jumpHeight = 1.5f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _groundedVelocity = -2f;
        [Header("Respawn")]
        [SerializeField] private float _respawnHeight = -10f;

        [Header("Acceleration and Sprint")]
        [SerializeField] private float _walkAccelerationTime = 0.3f;
        [SerializeField] private float _runSpeed = 10f;
        [SerializeField] private float _sprintAccelerationTime = 0.15f;
        [SerializeField] private float _sprintReleaseTime = 0.2f;
        [SerializeField] private float _brakingTime = 0.15f;
        [SerializeField] private float _airAcceleration = 24f;
        [Header("Air Feel")]
        [SerializeField] private float _fallGravity = -15f;
        [SerializeField] private float _terminalFallSpeed = 20f;
        [SerializeField] private float _apexDuration = 0.1f;
        [SerializeField] private float _apexGravityScale = 0.3f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBufferTime = 0.1f;

        private CharacterController _controller;
        private Vector3 _spawnPosition;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _coyoteRemaining;
        private float _jumpBufferRemaining;
        private float _apexRemaining;
        private bool _apexUsed;
        private static readonly int VerticalSpeedParameter = Animator.StringToHash("VerticalSpeed");
        public Vector3 Velocity => _horizontalVelocity + _inheritedHorizontal + Vector3.up *
            (State == MovementState.Grounded && _support != null ? _supportVelocity.y : _verticalVelocity)
            + (State == MovementState.Grounded ? new Vector3(_supportVelocity.x, 0f, _supportVelocity.z) : Vector3.zero);
        public float HorizontalSpeed => _horizontalVelocity.magnitude;
        [Header("Moving Support")]
        [SerializeField] private PrototypeMovingPlatform[] _movingPlatforms = new PrototypeMovingPlatform[0];
        [SerializeField] private float _inheritedAirDrag = 2f;
        private Vector3 _inheritedHorizontal;
        private Collider _support;
        private PrototypeMovingPlatform _supportPlatform;
        private Vector3 _supportVelocity;
        private float _supportIgnoreRemaining;
        public Collider Support => _support;

        [Header("Parkour")]
        [SerializeField] private PrototypeParkourProbe _parkourProbe;
        [SerializeField] private float _wallJumpAwaySpeed = 7f;
        [SerializeField] private float _wallJumpSteeringLock = 0.12f;
        [SerializeField] private float _wallJumpCooldown = 0.2f;
        [SerializeField] private float _regrabCooldown = 0.5f;
        [Header("Air Dash")]
        [SerializeField] private float _airDashBoost = 13f;
        [SerializeField] private float _dashProtectionTime = 0.15f;
        [SerializeField] private float _dashVerticalHoldTime = 0.1f;
        private float _dashVerticalRemaining;
        public float DashVerticalRemaining => _dashVerticalRemaining;
        private float _dashRemaining;
        private bool _airDashAvailable = true;
        public bool AirDashAvailable => _airDashAvailable;
        private float _steeringLockRemaining;
        private float _wallCooldownRemaining;
        private float _regrabRemaining;
        private PrototypeParkourProbe.Ledge _ledge;
        private Collider _lastJumpWall;
        private Collider _impactWall;
        private Vector3 _impactVelocity;
        private float _impactRemaining;
        private Vector3 _preMoveHorizontal;
        [Header("Slide")]
        [SerializeField] private float _slideHoldTime = 0.25f;
        [SerializeField] private float _slideDeceleration = 60f;
        [SerializeField] private float _slideJumpProtectionTime = 0.12f;
        private float _slideHoldRemaining;
        private float _slideJumpRemaining;
        private bool _slideHeld;
        private bool _wasSlideHeld;
        private bool _slidePressed;
        private bool _slideLatched;
        private Vector3 _landingHorizontal;
        public bool IsSliding { get; private set; }
        private static readonly int SlidingParameter = Animator.StringToHash("Sliding");
        private bool _wasDownPressed;
        private static readonly int HangingParameter= Animator.StringToHash("Hanging");
        public enum MovementState { Grounded, Airborne, Hanging }
        public MovementState State { get; private set; }
        public Vector3 LeftGrip => _ledge.LeftGrip;
        public Vector3 RightGrip => _ledge.RightGrip;
        public Vector3 LedgeNormal => _ledge.Normal;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;
        }

        private void Update()
        {
            bool captureInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            Vector2 movement = captureInput
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
                : Vector2.zero;
            bool sprint = captureInput && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            bool slide = captureInput && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            SimulateMovement(movement, captureInput && Input.GetButtonDown("Jump"), sprint, slide, Time.deltaTime);
        }

        public void SimulateMovement(Vector2 input, bool jumpPressed, float deltaTime)
        {
            SimulateMovement(input, jumpPressed, false, deltaTime);
        }

        public void SimulateMovement(Vector2 input, bool jumpPressed, bool sprint, float deltaTime)
        {
            SimulateMovement(input, jumpPressed, sprint, false, deltaTime);
        }

        public void SimulateMovement(Vector2 input, bool jumpPressed, bool sprint, bool slideHeld, float deltaTime)
        {
            _slideHeld = slideHeld;
            if (_controller == null || !_controller.enabled || deltaTime <= 0f)
                return;
            // Substeps keep capsule collision, buffered jumps and push-off reliable at low frame rates.
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / (1f / 60f)));
            float dt = deltaTime / steps;
            for (int i = 0; i < steps; i++)
            {
                foreach (var platform in _movingPlatforms)
                    if (platform != null) platform.Advance(dt);
                if (_movingPlatforms.Length > 0) Physics.SyncTransforms();
                StepMovement(input, jumpPressed && i == 0, sprint, dt);
            }
        }

        private void StepMovement(Vector2 input, bool jumpPressed, bool sprint, float dt)
        {
            _slidePressed = _slideHeld && !_wasSlideHeld;
            if (!_slideHeld) _slideLatched = false;
            _wasSlideHeld = _slideHeld;
            _slideJumpRemaining = Mathf.Max(0f, _slideJumpRemaining - dt);
            _landingHorizontal = _horizontalVelocity + _inheritedHorizontal;
            UpdateSupportBeforeMove(dt);
            if (_support == null && State != MovementState.Hanging && _supportIgnoreRemaining <= 0f)
                ResolveSupport();
            if (IsSliding && _support == null)
            {
                IsSliding = false;
                _slideJumpRemaining = _slideJumpProtectionTime;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            float yaw= _movementCamera != null ? _movementCamera.eulerAngles.y : 0f;
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            _steeringLockRemaining = Mathf.Max(0f, _steeringLockRemaining - dt);
            _wallCooldownRemaining = Mathf.Max(0f, _wallCooldownRemaining - dt);
            _regrabRemaining = Mathf.Max(0f, _regrabRemaining - dt);
            _dashRemaining = Mathf.Max(0f, _dashRemaining - dt);
            bool downPressed = input.y < -0.5f;
            bool dropPressed = downPressed && !_wasDownPressed;
            _wasDownPressed = downPressed;
            if (State == MovementState.Hanging && _ledge.Wall != null)
            {
                _ledge.Refresh();
                if (!_parkourProbe.CanReachAnchor(_ledge.Anchor, false))
                    ReleaseLedge();
                else
                {
                    _controller.Move(_ledge.Anchor - transform.position);
                    transform.rotation = Quaternion.LookRotation(-_ledge.Normal);
                }
            }
            if (State == MovementState.Hanging)
            {
                if (_ledge.Wall == null|| !_ledge.Wall.enabled || !_ledge.Wall.gameObject.activeInHierarchy)
                    ReleaseLedge();
                else if (dropPressed)
                    ReleaseLedge();
                else if (jumpPressed && _parkourProbe.CanReachAnchor(_ledge.Anchor + Vector3.up * (_controller.height + 0.1f), false))
                {
                    ReleaseLedge();
                    float ledgeVerticalMomentum = _verticalVelocity;
                    LaunchJump();
                    _verticalVelocity += ledgeVerticalMomentum;
                    _steeringLockRemaining = dt * 1.1f;
                    jumpPressed = false;
                }
                else
                {
                    _horizontalVelocity = Vector3.zero;
                    _verticalVelocity = 0f;
                    UpdateAnimation();
                    return;
                }
            }
            bool grounded = _support != null || (_parkourProbe == null && _controller.isGrounded && _verticalVelocity <= 0f && _supportIgnoreRemaining <= 0f);
            _coyoteRemaining = grounded ? _coyoteTime : Mathf.Max(0f, _coyoteRemaining - dt);
            _jumpBufferRemaining= jumpPressed ? _jumpBufferTime : Mathf.Max(0f, _jumpBufferRemaining - dt);

            Vector3 incomingHorizontal = _horizontalVelocity + _inheritedHorizontal;
            _impactRemaining = Mathf.Max(0f, _impactRemaining - dt);
            if (grounded && _slidePressed && !_slideLatched && !IsSliding && HorizontalSpeed >= _runSpeed - 0.001f)
            {
                IsSliding = true;
                _slideLatched = true;
                _slideHoldRemaining = _slideHoldTime;
            }
            if (IsSliding && _jumpBufferRemaining > 0f)
            {
                IsSliding = false;
                _slideJumpRemaining = _slideJumpProtectionTime;
                LaunchJump();
                grounded = false;
                jumpPressed = false;
            }
            float targetSpeed= sprint ? _runSpeed : _moveSpeed;
            float acceleration= _moveSpeed / Mathf.Max(0.01f, _walkAccelerationTime);
            if (sprint && HorizontalSpeed >= _moveSpeed)
                acceleration = (_runSpeed - _moveSpeed) / Mathf.Max(0.01f, _sprintAccelerationTime);
            if (!sprint && HorizontalSpeed > _moveSpeed)
                acceleration = (_runSpeed - _moveSpeed) / Mathf.Max(0.01f, _sprintReleaseTime);
            if (input.sqrMagnitude < 0.001f)
                acceleration = _runSpeed / Mathf.Max(0.01f, _brakingTime);
            if (!grounded)
                acceleration = _airAcceleration;
            if (IsSliding)
            {
                float protectedTime = Mathf.Min(dt, _slideHoldRemaining);
                _slideHoldRemaining = Mathf.Max(0f, _slideHoldRemaining - dt);
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, _slideDeceleration * (dt - protectedTime));
                if (HorizontalSpeed < 0.01f) IsSliding = false;
            }
            else if (_steeringLockRemaining <= 0f && _dashRemaining <= 0f && _slideJumpRemaining <= 0f)
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, direction * targetSpeed, acceleration * dt);
            Vector3 facing = IsSliding || _steeringLockRemaining > 0f ? _horizontalVelocity : direction;
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(facing), _turnSpeed * dt);

            if (grounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = _groundedVelocity;
                _apexUsed = false;
            }
            if (_jumpBufferRemaining > 0f && _coyoteRemaining > 0f)
                LaunchJump();
            else if (!grounded && _jumpBufferRemaining > 0f && _parkourProbe != null
                && _parkourProbe.TryWall(out RaycastHit wall)
                && (wall.collider != _lastJumpWall || _wallCooldownRemaining <= 0f))
            {
                Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
                Vector3 approach = incomingHorizontal;
                if (_impactRemaining > 0f && _impactWall == wall.collider
                    && Vector3.Dot(approach, normal) >= -0.05f)
                    approach = _impactVelocity;
                float inward = Vector3.Dot(approach, normal);
                bool reflect = sprint && approach.sqrMagnitude > 0.01f && inward < -0.05f
                    && -inward / approach.magnitude <= Mathf.Sin(45f * Mathf.Deg2Rad) + 0.00001f;
                LaunchJump();
                _horizontalVelocity = reflect ? Vector3.Reflect(approach, normal) : normal * _wallJumpAwaySpeed;
                _inheritedHorizontal = Vector3.zero;
                _impactRemaining = 0f;
                _steeringLockRemaining = _wallJumpSteeringLock;
                _regrabRemaining = _regrabCooldown;
                _lastJumpWall = wall.collider;
                _wallCooldownRemaining = _wallJumpCooldown;
            }
            else if (!grounded && jumpPressed && _airDashAvailable
                && !(_slideHeld && incomingHorizontal.magnitude >= _runSpeed - 0.001f
                    && _verticalVelocity < 0f && _parkourProbe != null
                    && _parkourProbe.TrySupport(out var imminentLanding, Mathf.Max(0.07f, -_verticalVelocity * dt + 0.04f))))
            {
                Vector3 dashDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                float totalHorizontalSpeed = (_horizontalVelocity + _inheritedHorizontal).magnitude;
                _horizontalVelocity = dashDirection * (totalHorizontalSpeed + _airDashBoost);
                _inheritedHorizontal = Vector3.zero;
                _airDashAvailable = false;
                _dashRemaining = _dashProtectionTime;
                _dashVerticalRemaining = _dashVerticalHoldTime;
                _verticalVelocity = 0f;
                _apexRemaining = 0f;
                _apexUsed = true;
                _jumpBufferRemaining = 0f;
                _regrabRemaining = _regrabCooldown;
            }

            float gravity= _verticalVelocity > 0f ? _gravity : _fallGravity;
            if (!grounded && !_apexUsed && Mathf.Abs(_verticalVelocity) < 0.6f)
            {
                _apexUsed = true;
                _apexRemaining = _apexDuration;
            }
            if (_apexRemaining > 0f)
            {
                _apexRemaining = Mathf.Max(0f, _apexRemaining - dt);
                gravity *= _apexGravityScale;
            }
            float suspendedTime = Mathf.Min(dt, _dashVerticalRemaining);
            _dashVerticalRemaining = Mathf.Max(0f, _dashVerticalRemaining - dt);
            if (suspendedTime > 0f) _verticalVelocity = 0f;
            float gravityTime = dt - suspendedTime;
            _verticalVelocity = Mathf.Max(-_terminalFallSpeed, _verticalVelocity + gravity * gravityTime);
            _inheritedHorizontal = Vector3.MoveTowards(_inheritedHorizontal, Vector3.zero, _inheritedAirDrag * dt);
            _preMoveHorizontal = _horizontalVelocity + _inheritedHorizontal;
            Vector3 before = transform.position;
            CollisionFlags collisions = _controller.Move((_horizontalVelocity + _inheritedHorizontal) * dt + Vector3.up * _verticalVelocity * gravityTime);
            if ((collisions & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
                _verticalVelocity = _groundedVelocity;
            if ((collisions & CollisionFlags.Sides) != 0)
            {
                Vector3 displacement = transform.position - before;
                _inheritedHorizontal = Vector3.zero;
                _horizontalVelocity = new Vector3(displacement.x, 0f, displacement.z) / dt;
                _landingHorizontal = _horizontalVelocity;
            }
            ResolveSupport((collisions & CollisionFlags.Below) != 0);
            if (State == MovementState.Grounded)
            {
                _lastJumpWall = null;
                if (IsSliding && _jumpBufferRemaining > 0f)
                {
                    IsSliding = false;
                    _slideJumpRemaining = _slideJumpProtectionTime;
                    LaunchJump();
                }
            }
            if (State == MovementState.Airborne&& _regrabRemaining <= 0f && _parkourProbe != null
                && direction.sqrMagnitude > 0.1f && _parkourProbe.TryLedge(direction, out var ledge))
            {
                // The probe checked both a swept capsule and the destination before this bounded snap.
                _controller.Move(ledge.Anchor - transform.position);
                if (Vector3.Distance(transform.position, ledge.Anchor) < 0.04f)
                {
                    _ledge = ledge;
                    State = MovementState.Hanging;
                    IsSliding = false;
                    _slideHoldRemaining = _slideJumpRemaining = 0f;
                    _horizontalVelocity = Vector3.zero;
                    _inheritedHorizontal = Vector3.zero;
                    _support = null;
                    _supportPlatform = null;
                    _supportVelocity = Vector3.zero;
                    _verticalVelocity = 0f;
                    _dashVerticalRemaining = 0f;
                    _coyoteRemaining = _jumpBufferRemaining = _apexRemaining = 0f;
                    transform.rotation= Quaternion.LookRotation(-ledge.Normal);
                    if (_animator != null)
                        _animator.ResetTrigger(JumpParameter);
                }
            }
            if (transform.position.y < _respawnHeight)
                Respawn();
            UpdateAnimation();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (State != MovementState.Grounded && Mathf.Abs(hit.normal.y) < 0.2f
                && Vector3.Dot(_preMoveHorizontal, hit.normal) < -0.05f)
            {
                _impactWall = hit.collider;
                _impactVelocity = _preMoveHorizontal;
                _impactRemaining = 0.1f;
            }
        }

        private void UpdateSupportBeforeMove(float dt)
        {
            _supportIgnoreRemaining = Mathf.Max(0f, _supportIgnoreRemaining - dt);
            if (_support == null) return;
            if (!_support.enabled || !_support.gameObject.activeInHierarchy
                || (_supportPlatform != null && !_supportPlatform.isActiveAndEnabled))
            {
                DetachSupport();
                return;
            }
            if (_supportPlatform == null) return;
            Vector3 newVelocity = _supportPlatform.Velocity;
            // A surface can push up but cannot exert a downward pulling force.
            if (newVelocity.y < _supportVelocity.y + _fallGravity * dt - 0.02f)
            {
                DetachSupport();
                return;
            }
            Vector3 delta = _supportPlatform.Displacement;
            Vector3 before = transform.position;
            _controller.Move(delta);
            if (Vector3.Distance(transform.position - before, delta) > 0.04f)
            {
                DetachSupport();
                return;
            }
            _supportVelocity = newVelocity;
        }

        private void DetachSupport()
        {
            if (_support == null && _supportPlatform == null) return;
            _inheritedHorizontal += new Vector3(_supportVelocity.x, 0f, _supportVelocity.z);
            _verticalVelocity = _supportVelocity.y;
            _support = null;
            _supportPlatform = null;
            _supportVelocity = Vector3.zero;
            _supportIgnoreRemaining = 0.06f;
            State = MovementState.Airborne;
        }

        private void ResolveSupport(bool touchedGround = false)
        {
            if (_parkourProbe == null){ State = _controller.isGrounded && _verticalVelocity <= 0f ? MovementState.Grounded : MovementState.Airborne; return; }
            if (_supportIgnoreRemaining <= 0f && _parkourProbe.TrySupport(out var hit, 0.07f))
            {
                var platform = hit.collider.GetComponent<PrototypeMovingPlatform>();
                Vector3 supportVelocity = platform != null ? platform.Velocity : Vector3.zero;
                if (touchedGround || _verticalVelocity <= supportVelocity.y + 0.05f || _support == hit.collider)
                {
                    bool newLanding = _support == null;
                    Vector3 arrivalRelative = _landingHorizontal - new Vector3(supportVelocity.x, 0f, supportVelocity.z);
                    if (newLanding && _slideHeld && arrivalRelative.magnitude >= _runSpeed - 0.001f)
                    {
                        _horizontalVelocity = arrivalRelative;
                        IsSliding = true;
                        _slideLatched = true;
                        _slideHoldRemaining = _slideHoldTime;
                    }
                    _support = hit.collider;
                    _supportPlatform = platform;
                    _supportVelocity = supportVelocity;
                    _inheritedHorizontal = Vector3.zero;
                    _verticalVelocity = _groundedVelocity;
                    State = MovementState.Grounded;
                    _airDashAvailable = true;
                    _dashRemaining = 0f;
                    _dashVerticalRemaining = 0f;
                    return;
                }
            }
            if (_support != null) DetachSupport();
            State = MovementState.Airborne;
        }

        private void LaunchJump()
        {
            _dashVerticalRemaining = 0f;
            float inheritedY= _support != null ? _supportVelocity.y : 0f;
            DetachSupport();
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity) + inheritedY;
            _coyoteRemaining = 0f;
            _jumpBufferRemaining = 0f;
            _apexRemaining = 0f;
            _apexUsed = false;
            if (_animator != null)
                _animator.SetTrigger(JumpParameter);
        }

        public void Respawn()
        {
            _controller.enabled = false;
            transform.position = _spawnPosition;
            _horizontalVelocity = Vector3.zero;
            _inheritedHorizontal = Vector3.zero;
            _support = null;
            _supportPlatform = null;
            _supportVelocity = Vector3.zero;
            _supportIgnoreRemaining = 0f;
            _wasDownPressed = false;
            _verticalVelocity = 0f;
            _coyoteRemaining = _jumpBufferRemaining = _apexRemaining = 0f;
            _steeringLockRemaining= _wallCooldownRemaining = _regrabRemaining = _dashRemaining = 0f;
            _airDashAvailable = true;
            _dashVerticalRemaining = 0f;
            IsSliding = false;
            _slideHeld = _wasSlideHeld = _slidePressed = _slideLatched = false;
            _slideHoldRemaining = _slideJumpRemaining = _impactRemaining = 0f;
            _impactWall = null;
            _impactVelocity = Vector3.zero;
            _ledge = default;
            _lastJumpWall = null;
            State = MovementState.Airborne;
            _apexUsed = false;
            _controller.enabled = true;
            if (_animator != null)
            {
                _animator.ResetTrigger(JumpParameter);
                _animator.Play("Idle", 0, 0f);
            }
        }

        private void ReleaseLedge()
        {
            Vector3 inherited = Vector3.zero;
            if (_ledge.Wall != null && _ledge.Wall.TryGetComponent<PrototypeMovingPlatform>(out var platform))
                inherited = platform.Velocity;
            State = MovementState.Airborne;
            _regrabRemaining = _regrabCooldown;
            _coyoteRemaining = _jumpBufferRemaining = 0f;
            _horizontalVelocity = Vector3.zero;
            _inheritedHorizontal = new Vector3(inherited.x, 0f, inherited.z);
            _verticalVelocity = inherited.y;
            _supportIgnoreRemaining = 0.06f;
            _ledge = default;
        }

        private void UpdateAnimation()
        {
            if (_animator == null)
                return;
            _animator.SetFloat(SpeedParameter, HorizontalSpeed);
            _animator.SetBool(GroundedParameter, State == MovementState.Grounded);
            _animator.SetFloat(VerticalSpeedParameter, _verticalVelocity);
            _animator.SetBool(HangingParameter, State == MovementState.Hanging);
            _animator.SetBool(SlidingParameter, IsSliding);
        }
    }
}
