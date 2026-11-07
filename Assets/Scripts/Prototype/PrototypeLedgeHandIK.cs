using UnityEngine;

namespace Prototype
{
    [RequireComponent(typeof(Animator))]
    public sealed class PrototypeLedgeHandIK : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController _player;
        [SerializeField] private float _blendTime = 0.06f;
        private Animator _animator;
        private float _weight;
        private Vector3 _leftTarget;
        private Vector3 _rightTarget;
        private Quaternion _rotation;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_player == null || _animator == null || !_animator.isHuman) return;
            bool hanging = _player.State == PrototypePlayerController.MovementState.Hanging;
            _weight = Mathf.MoveTowards(_weight, hanging ? 1f : 0f, Time.deltaTime / Mathf.Max(0.001f, _blendTime));
            if (hanging)
            {
                _leftTarget = _player.LeftGrip;
                _rightTarget = _player.RightGrip;
                _rotation = Quaternion.LookRotation(-_player.LedgeNormal, Vector3.up);
            }
            // Capsule radius exceeds this dummy's torso depth. Lean the pose toward the grip,
            // instead of asking IK to stretch short arms across the full capsule-wall gap.
            _animator.bodyPosition += (_rotation * Vector3.forward) * (0.25f * _weight);
            SetHand(AvatarIKGoal.LeftHand, _leftTarget);
            SetHand(AvatarIKGoal.RightHand, _rightTarget);
        }

        private void SetHand(AvatarIKGoal goal, Vector3 target)
        {
            _animator.SetIKPositionWeight(goal, _weight);
            _animator.SetIKRotationWeight(goal, _weight * 0.5f);
            _animator.SetIKPosition(goal, target);
            _animator.SetIKRotation(goal, _rotation);
        }
    }
}
