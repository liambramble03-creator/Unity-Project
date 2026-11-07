using UnityEngine;

namespace Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypeParkourProbe : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] private LayerMask _geometryMask = ~0;
        [SerializeField] private float _wallReach = 0.22f;
        [SerializeField] private float _maximumWallNormalY = 0.2f;
        [SerializeField] private float _minimumTopNormalY = 0.9f;
        [SerializeField] private float _handReachAboveHead = 0.35f;
        [SerializeField] private float _maximumHangSnap = 0.65f;
        [SerializeField] private float _edgeClearance = 0.08f;
        [Header("Calibrated Hanging Pose")]
        [SerializeField] private float _gripHeight = 1.65f;
        [SerializeField] private float _gripHalfWidth = 0.32f;
        private CharacterController _controller;
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];

        public struct Ledge
        {
            public Collider Wall;
            public Vector3 Normal;
            public Vector3 Anchor;
            public Vector3 TopDestination;
            public Vector3 LeftGrip;
            public Vector3 RightGrip;
            public Vector3 LocalAnchor;
            public Vector3 LocalLeftGrip;
            public Vector3 LocalRightGrip;
            public Vector3 LocalNormal;

            public void Refresh()
            {
                if (Wall == null) return;
                var t = Wall.transform;
                Anchor = t.TransformPoint(LocalAnchor);
                LeftGrip = t.TransformPoint(LocalLeftGrip);
                RightGrip = t.TransformPoint(LocalRightGrip);
                Normal = t.TransformDirection(LocalNormal).normalized;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private bool IsOwnCollider(Collider collider)
        {
            return collider == null || collider.transform.IsChildOf(transform);
        }

        private bool Cast(Vector3 origin, Vector3 direction, float length, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, length, _geometryMask, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (IsOwnCollider(_hits[i].collider) || _hits[i].distance >= distance)
                    continue;
                nearest = _hits[i];
                distance = nearest.distance;
            }
            return distance < float.PositiveInfinity;
        }

        public bool TryWall(out RaycastHit wall)
        {
            wall = default;
            float closest = float.PositiveInfinity;
            Vector3 center = transform.TransformPoint(_controller.center);
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward;
                if (Cast(center, direction, _controller.radius + _wallReach, out RaycastHit hit)
                    && Mathf.Abs(hit.normal.y) < _maximumWallNormalY && hit.distance < closest)
                {
                    wall = hit;
                    closest = hit.distance;
                }
            }
            return closest < float.PositiveInfinity;
        }

        public bool CapsuleFits(Vector3 footPosition)
        {
            float radius = Mathf.Max(0.01f, _controller.radius - _controller.skinWidth);
            Vector3 center = footPosition + _controller.center;
            float offset = Mathf.Max(0f, _controller.height * 0.5f - _controller.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * offset,
                center - Vector3.up * offset, radius, _overlaps, _geometryMask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length)
                return false;
            for (int i = 0; i < count; i++)
                if (!IsOwnCollider(_overlaps[i]))
                    return false;
            return true;
        }

        public bool CanReachAnchor(Vector3 anchor, bool limitSnap = true)
        {
            Vector3 delta = anchor - transform.position;
            if ((limitSnap && delta.magnitude > _maximumHangSnap) || !CapsuleFits(anchor))
                return false;
            if (delta.sqrMagnitude < 0.0001f)
                return true;
            Vector3 center = transform.TransformPoint(_controller.center);
            float offset = _controller.height * 0.5f - _controller.radius;
            int count = Physics.CapsuleCastNonAlloc(center + Vector3.up * offset,
                center - Vector3.up * offset, _controller.radius - _controller.skinWidth,
                delta.normalized, _hits, delta.magnitude, _geometryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!IsOwnCollider(_hits[i].collider))
                    return false;
            return count < _hits.Length;
        }

        public bool TryLedge(Vector3 approach, out Ledge ledge)
        {
            ledge = default;
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f)
                return false;
            approach.Normalize();
            Vector3 chest = transform.position + Vector3.up * (_controller.height * 0.6f);
            if (!Cast(chest, approach, _controller.radius + _wallReach, out RaycastHit wall)
                || Mathf.Abs(wall.normal.y) >= _maximumWallNormalY || Vector3.Dot(approach, -wall.normal) < 0.6f)
                return false;
            Vector3 normal = new Vector3(wall.normal.x, 0, wall.normal.z).normalized;
            Vector3 topOrigin = wall.point - normal * 0.15f;
            topOrigin.y = transform.position.y + _controller.height + _handReachAboveHead;
            if (!Cast(topOrigin, Vector3.down, 0.85f, out RaycastHit top)
                || top.normal.y < _minimumTopNormalY || top.collider != wall.collider)
                return false;
            Vector3 edge = wall.point;
            edge.y = top.point.y;
            Vector3 anchor = edge + normal * (_controller.radius + _edgeClearance) - Vector3.up * _gripHeight;
            Vector3 tangent = Vector3.Cross(Vector3.up, -normal);
            Vector3 leftGrip = edge - tangent * _gripHalfWidth - normal * 0.03f + Vector3.up * 0.02f;
            Vector3 rightGrip = edge + tangent * _gripHalfWidth - normal * 0.03f + Vector3.up * 0.02f;
            foreach (Vector3 grip in new[] { leftGrip, rightGrip })
                if (!Cast(grip + Vector3.up * 0.1f, Vector3.down, 0.2f, out var gripHit)
                    || gripHit.collider != wall.collider || gripHit.normal.y < _minimumTopNormalY)
                    return false;
            Vector3 destination = edge - normal * (_controller.radius + 0.2f) + Vector3.up * 0.04f;
            if (!Cast(destination + Vector3.up * 0.1f, Vector3.down, 0.2f, out RaycastHit support)
                || support.collider != top.collider || support.normal.y < _minimumTopNormalY
                || !CapsuleFits(destination) || !CanReachAnchor(anchor))
                return false;
            // Head must be able to rise on the outside before crossing the lip.
            if (!CapsuleFits(anchor + Vector3.up * (_controller.height + 0.1f)))
                return false;
            ledge = new Ledge
            {
                Wall = wall.collider, Normal = normal, Anchor = anchor, TopDestination = destination,
                LeftGrip = leftGrip, RightGrip = rightGrip,
                LocalAnchor = wall.collider.transform.InverseTransformPoint(anchor),
                LocalLeftGrip = wall.collider.transform.InverseTransformPoint(leftGrip),
                LocalRightGrip = wall.collider.transform.InverseTransformPoint(rightGrip),
                LocalNormal = wall.collider.transform.InverseTransformDirection(normal)
            };
            return true;
        }

        public bool TrySupport(out RaycastHit support, float distance = 0.12f)
        {
            // A tight foot ray prevents side contacts and large ground-snaps from refilling dash.
            return Cast(transform.position + Vector3.up * 0.08f, Vector3.down, distance + 0.08f, out support)
                && support.normal.y >= _minimumTopNormalY;
        }
    }
}
