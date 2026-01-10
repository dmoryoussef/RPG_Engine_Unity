using UnityEngine;
using Player; // PlayerMover2D

public sealed class CameraRig2D : MonoBehaviour
{
    /* ===================================================================== */
    /* Settings (Inspector Organization)                                      */
    /* ===================================================================== */

    [System.Serializable]
    private sealed class BindingSettings
    {
        [Tooltip("Camera driven by this rig. If null, we try to find one in children on Awake.")]
        public UnityEngine.Camera Camera;

        [Tooltip("Transform to follow (usually the controlled player/pawn).")]
        public Transform FollowTarget;

        [Tooltip("Optional override for look direction source.\n" +
                 "If set, uses this mover's Facing.\n" +
                 "If null, we try to find PlayerMover2D on the FollowTarget.")]
        public PlayerMover2D PlayerMover;
    }

    [System.Serializable]
    private sealed class FollowSettings
    {
        [Tooltip("If false, position snaps to the resolved anchor each frame.")]
        public bool UseSmoothing = true;

        [Tooltip("How quickly the camera moves toward its target position.\nHigher = snappier.")]
        [Min(0f)]
        public float PositionLerp = 20f;
    }

    [System.Serializable]
    private sealed class PanSettings
    {
        [Tooltip("World-space offset applied to the camera center.\nDriven by external scripts (cutscenes, input adapters).")]
        public Vector2 PanWorld;

        [Tooltip("Enable applying PanWorld to the camera center.")]
        public bool PanEnabled = true;
    }

    [System.Serializable]
    private sealed class DeadZoneSettings
    {
        [Tooltip("If false, dead-zone behaves like size (0,0) => immediate follow.")]
        public bool DeadZoneEnabled = true;

        [Tooltip("Half-size of the dead-zone rectangle in world units.")]
        public Vector2 HalfSize = new Vector2(2.5f, 1.5f);
    }

    [System.Serializable]
    private sealed class LookSettings
    {
        [Tooltip("Enable reading look direction from PlayerMover2D.Facing.")]
        public bool LookDirectionEnabled = true;

        [Tooltip("Enable applying a bias offset to the camera zone center.")]
        public bool LookBiasEnabled = true;

        [Tooltip("Signed world-units to shift the zone center.\n" +
                 "Negative typically LEADS (see more ahead).\n" +
                 "Positive typically TRAILS (see more behind).")]
        public float BiasDistance = -1.5f;

        [Header("Direction + Bias Smoothing")]
        [Tooltip("Normal smoothing speed for look direction and bias offset.\nHigher responds faster.")]
        [Min(0f)]
        public float LookDirLerp = 12f;

        [Header("Facing Change Snap (Timed Blend)")]
        [Tooltip("If enabled, sharp facing changes trigger a timed blend.\n" +
                 "This blend is applied to the BIAS OFFSET (linear), preventing arc motion.")]
        public bool SnapOnFacingChange = true;

        [Tooltip("Degrees. Facing change >= this triggers a snap blend.")]
        [Range(0f, 180f)]
        public float SnapAngleDeg = 25f;

        [Tooltip("Duration (seconds) of the snap blend.\nThis is a true duration (3.0 means 3 seconds).")]
        [Min(0f)]
        public float SnapBlendSeconds = 0.35f;

        [Header("Snap Responsiveness (Optional)")]
        [Tooltip("If enabled, dead-zone is temporarily scaled down during snap blend.\n" +
                 "This makes the camera react immediately to bias changes instead of waiting for target to exit the zone.")]
        public bool ShrinkDeadZoneDuringSnap = true;

        [Tooltip("Dead-zone half-size multiplier during snap blend.\n" +
                 "0 = acts like no dead-zone during snap; 1 = unchanged.")]
        [Range(0f, 1f)]
        public float SnapDeadZoneScale = 0.25f;
    }

    [System.Serializable]
    private sealed class ZoomSettings
    {
        [Tooltip("Minimum allowed orthographic size (max zoom-in).")]
        [Min(0.01f)]
        public float MinOrthoSize = 2f;

        [Tooltip("Maximum allowed orthographic size (max zoom-out).")]
        [Min(0.01f)]
        public float MaxOrthoSize = 20f;

        [Tooltip("If false, zoom snaps to target each frame.")]
        public bool UseSmoothing = true;

        [Tooltip("How quickly the camera zooms toward the target size.\nHigher = snappier.")]
        [Min(0f)]
        public float ZoomLerp = 20f;
    }

    [System.Serializable]
    private sealed class GizmoSettings
    {
        [Tooltip("Draw gizmos even when the object isn't selected.")]
        public bool DrawAlways = false;

        [Tooltip("Length of look direction arrow in world units.")]
        [Min(0.01f)]
        public float DirLength = 2f;

        [Tooltip("Z for gizmo drawing. Use 0 for 2D visibility.")]
        public float DrawZ = 0f;
    }

    [Header("Camera Rig Settings")]
    [SerializeField] private BindingSettings _binding = new();
    [SerializeField] private FollowSettings _follow = new();
    [SerializeField] private PanSettings _pan = new();
    [SerializeField] private DeadZoneSettings _deadZone = new();
    [SerializeField] private LookSettings _look = new();
    [SerializeField] private ZoomSettings _zoom = new();
    [SerializeField] private GizmoSettings _gizmos = new();

    /* ===================================================================== */
    /* Runtime State                                                          */
    /* ===================================================================== */

    // Smoothed facing direction (unit vector). Used for debug arrow and for desired bias computation.
    private Vector2 _smoothedLookDir = Vector2.right;
    private Vector2 _lastFacing = Vector2.right;

    // Bias offset (world units) actually applied to zone center.
    // This is what we blend linearly during snap to avoid arc motion.
    private Vector2 _biasOffset;

    // Snap blending for bias offset (linear world-space blend).
    private bool _isBiasBlending;
    private float _biasBlendStartTime;
    private Vector2 _biasBlendFrom;
    private Vector2 _biasBlendTo;

    private float _targetOrthoSize;

    /* ===================================================================== */
    /* Public Accessors (for adapter scripts)                                  */
    /* ===================================================================== */

    public UnityEngine.Camera ViewCamera => _binding.Camera;

    public Vector2 PanWorld
    {
        get => _pan.PanWorld;
        set => _pan.PanWorld = value;
    }

    public bool IsSnapBlending => _isBiasBlending;

    /* ===================================================================== */
    /* Public API                                                             */
    /* ===================================================================== */

    public void Bind(UnityEngine.Camera camera, Transform followTarget)
    {
        _binding.Camera = camera;
        SetFollowTarget(followTarget);
        EnsureCameraInitialized();
    }

    public void SetFollowTarget(Transform followTarget)
    {
        _binding.FollowTarget = followTarget;

        if (_binding.PlayerMover == null && _binding.FollowTarget != null)
        {
            _binding.PlayerMover = _binding.FollowTarget.GetComponent<PlayerMover2D>();
        }
    }

    public void AddPanWorld(Vector2 deltaWorld)
    {
        _pan.PanWorld += deltaWorld;
    }

    public void ClearPan()
    {
        _pan.PanWorld = Vector2.zero;
    }

    public void SetZoom(float orthoSize)
    {
        _targetOrthoSize = Mathf.Clamp(orthoSize, _zoom.MinOrthoSize, _zoom.MaxOrthoSize);
    }

    public void AddZoomDelta(float delta)
    {
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + delta, _zoom.MinOrthoSize, _zoom.MaxOrthoSize);
    }

    /* ===================================================================== */
    /* Unity Lifecycle                                                        */
    /* ===================================================================== */

    private void Awake()
    {
        if (_binding.Camera == null)
        {
            _binding.Camera = GetComponentInChildren<UnityEngine.Camera>();
        }

        if (_binding.PlayerMover == null && _binding.FollowTarget != null)
        {
            _binding.PlayerMover = _binding.FollowTarget.GetComponent<PlayerMover2D>();
        }

        EnsureCameraInitialized();
    }

    private void EnsureCameraInitialized()
    {
        if (_binding.Camera == null)
        {
            return;
        }

        _binding.Camera.orthographic = true;

        if (_zoom.MaxOrthoSize < _zoom.MinOrthoSize)
        {
            _zoom.MaxOrthoSize = _zoom.MinOrthoSize;
        }

        _targetOrthoSize = Mathf.Clamp(
            _binding.Camera.orthographicSize,
            _zoom.MinOrthoSize,
            _zoom.MaxOrthoSize
        );
    }

    private void LateUpdate()
    {
        if (_binding.Camera == null)
        {
            return;
        }

        float dt = Time.deltaTime;

        // Update look direction (smoothed) and start bias blending on sharp turns (optional)
        if (_look.LookDirectionEnabled)
        {
            UpdateLookDirectionAndMaybeStartBiasBlend(dt);
        }

        // Update bias offset (linear during snap; exponential otherwise)
        UpdateBiasOffset(dt);

        Vector2 anchor = GetAnchorFromTransform();
        Vector2 zoneCenter = ComputeZoneCenter(anchor);

        // Always resolve follow. DeadZoneEnabled=false => halfSize=(0,0) => immediate follow.
        Vector2 resolvedAnchor = ResolveAnchorWithDeadZone(zoneCenter, anchor);

        ApplyPosition(resolvedAnchor, dt);
        ApplyZoom(dt);
    }

    /* ===================================================================== */
    /* Look Direction + Bias Blend (No Arc Motion)                             */
    /* ===================================================================== */

    private void UpdateLookDirectionAndMaybeStartBiasBlend(float dt)
    {
        if (_binding.PlayerMover == null)
        {
            return;
        }

        Vector2 facing = _binding.PlayerMover.Facing;
        if (facing.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 desired = facing.normalized;

        // If this is a sharp direction change, start a LINEAR bias offset blend.
        if (_look.SnapOnFacingChange)
        {
            float angle = Vector2.Angle(_lastFacing, desired);
            if (angle >= _look.SnapAngleDeg)
            {
                StartBiasBlendTo(desired);
            }

            _lastFacing = desired;
        }

        // Keep a smoothed look direction for non-snap situations / debug arrows.
        // Exponential smoothing (frame-rate stable).
        float alpha = 1f - Mathf.Exp(-_look.LookDirLerp * dt);
        _smoothedLookDir = Vector2.Lerp(_smoothedLookDir, desired, alpha);

        if (_smoothedLookDir.sqrMagnitude > 0.0001f)
        {
            _smoothedLookDir.Normalize();
        }
    }

    private void StartBiasBlendTo(Vector2 newLookDirUnit)
    {
        if (!_look.LookBiasEnabled || Mathf.Abs(_look.BiasDistance) <= 0.0001f)
        {
            _isBiasBlending = false;
            return;
        }

        // Blend the WORLD OFFSET linearly from old -> new (prevents arc motion).
        _isBiasBlending = true;
        _biasBlendStartTime = Time.time;
        _biasBlendFrom = _biasOffset;
        _biasBlendTo = newLookDirUnit * _look.BiasDistance;
    }

    private void UpdateBiasOffset(float dt)
    {
        if (!_look.LookBiasEnabled || Mathf.Abs(_look.BiasDistance) <= 0.0001f)
        {
            _biasOffset = Vector2.zero;
            _isBiasBlending = false;
            return;
        }

        // Desired offset from current smoothed look dir
        Vector2 lookDir = (_smoothedLookDir.sqrMagnitude > 0.0001f) ? _smoothedLookDir.normalized : Vector2.right;
        Vector2 desiredOffset = lookDir * _look.BiasDistance;

        // If we're in a snap blend, bias offset moves LINEARLY from old -> new over time
        if (_isBiasBlending)
        {
            float dur = Mathf.Max(0.0001f, _look.SnapBlendSeconds);
            float t = Mathf.Clamp01((Time.time - _biasBlendStartTime) / dur);
            t = Mathf.SmoothStep(0f, 1f, t);

            _biasOffset = Vector2.Lerp(_biasBlendFrom, _biasBlendTo, t);

            if (t >= 1f)
            {
                _isBiasBlending = false;
            }

            return;
        }

        // Normal behavior: exponential smoothing toward desired offset
        float alpha = 1f - Mathf.Exp(-_look.LookDirLerp * dt);
        _biasOffset = Vector2.Lerp(_biasOffset, desiredOffset, alpha);
    }

    /* ===================================================================== */
    /* Zone Center                                                            */
    /* ===================================================================== */

    private Vector2 GetAnchorFromTransform()
    {
        Vector3 pos = transform.position;
        return new Vector2(pos.x, pos.y);
    }

    private Vector2 ComputeZoneCenter(Vector2 anchor)
    {
        Vector2 zoneCenter = anchor;

        if (_pan.PanEnabled)
        {
            zoneCenter += _pan.PanWorld;
        }

        // Apply bias offset (world-space). This is what we blend linearly to avoid arcs.
        if (_look.LookBiasEnabled && Mathf.Abs(_look.BiasDistance) > 0.0001f)
        {
            zoneCenter += _biasOffset;
        }

        return zoneCenter;
    }

    /* ===================================================================== */
    /* Dead Zone Follow                                                       */
    /* ===================================================================== */

    private Vector2 ResolveAnchorWithDeadZone(Vector2 zoneCenter, Vector2 anchor)
    {
        if (_binding.FollowTarget == null)
        {
            return anchor;
        }

        Vector2 half = _deadZone.DeadZoneEnabled ? _deadZone.HalfSize : Vector2.zero;

        // Optional: during snap, shrink dead-zone so bias changes move the camera immediately.
        if (_isBiasBlending && _look.ShrinkDeadZoneDuringSnap)
        {
            half *= _look.SnapDeadZoneScale;
        }

        Vector2 targetPos = (Vector2)_binding.FollowTarget.position;
        Vector2 delta = targetPos - zoneCenter;

        float moveX = 0f;
        if (delta.x > half.x) moveX = delta.x - half.x;
        else if (delta.x < -half.x) moveX = delta.x + half.x;

        float moveY = 0f;
        if (delta.y > half.y) moveY = delta.y - half.y;
        else if (delta.y < -half.y) moveY = delta.y + half.y;

        return anchor + new Vector2(moveX, moveY);
    }

    /* ===================================================================== */
    /* Apply Position / Zoom                                                  */
    /* ===================================================================== */

    private void ApplyPosition(Vector2 anchor, float dt)
    {
        Vector3 current = transform.position;
        Vector3 desired = new Vector3(anchor.x, anchor.y, current.z);

        if (_follow.UseSmoothing)
        {
            transform.position = Vector3.Lerp(transform.position, desired, _follow.PositionLerp * dt);
        }
        else
        {
            transform.position = desired;
        }
    }

    private void ApplyZoom(float dt)
    {
        float clampedTarget = Mathf.Clamp(_targetOrthoSize, _zoom.MinOrthoSize, _zoom.MaxOrthoSize);

        if (_zoom.UseSmoothing)
        {
            _binding.Camera.orthographicSize = Mathf.Lerp(_binding.Camera.orthographicSize, clampedTarget, _zoom.ZoomLerp * dt);
        }
        else
        {
            _binding.Camera.orthographicSize = clampedTarget;
        }
    }

    public void RequestZoomDelta(float deltaOrthoSize, Vector2 pivotScreenPos)
    {
        if (_binding.Camera == null)
        {
            return;
        }

        float oldTarget = Mathf.Clamp(_targetOrthoSize, _zoom.MinOrthoSize, _zoom.MaxOrthoSize);
        float newTarget = Mathf.Clamp(oldTarget + deltaOrthoSize, _zoom.MinOrthoSize, _zoom.MaxOrthoSize);

        if (Mathf.Approximately(newTarget, oldTarget))
        {
            return;
        }

        // If pan is disabled, we can't keep cursor anchored: fall back to center zoom.
        if (!_pan.PanEnabled)
        {
            _targetOrthoSize = newTarget;
            return;
        }

        Vector2 before = ScreenToWorldOrtho(_binding.Camera, oldTarget, pivotScreenPos);
        Vector2 after = ScreenToWorldOrtho(_binding.Camera, newTarget, pivotScreenPos);

        // panDelta represents how the CAMERA should move in world space.
        // But PanWorld shifts the zone center, which moves the camera in the opposite direction.
        // So we subtract here.
        Vector2 panDelta = before - after;
        _pan.PanWorld -= panDelta;

        _targetOrthoSize = newTarget;

    }

    private static Vector2 ScreenToWorldOrtho(UnityEngine.Camera cam, float orthoSize, Vector2 screenPos)
    {
        Rect r = cam.pixelRect;

        float vx = (screenPos.x - r.xMin) / Mathf.Max(1f, r.width);
        float vy = (screenPos.y - r.yMin) / Mathf.Max(1f, r.height);

        float aspect = (r.width > 0f && r.height > 0f) ? (r.width / r.height) : cam.aspect;

        float halfH = orthoSize;
        float halfW = orthoSize * aspect;

        float ox = (vx - 0.5f) * (2f * halfW);
        float oy = (vy - 0.5f) * (2f * halfH);

        Vector3 camPos = cam.transform.position;
        return new Vector2(camPos.x + ox, camPos.y + oy);
    }


    /* ===================================================================== */
    /* Gizmos                                                                 */
    /* ===================================================================== */

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_gizmos.DrawAlways)
        {
            DrawGizmosInternal();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!_gizmos.DrawAlways)
        {
            DrawGizmosInternal();
        }
    }

    private void DrawGizmosInternal()
    {
        float z = _gizmos.DrawZ;

        Vector2 half = _deadZone.DeadZoneEnabled ? _deadZone.HalfSize : Vector2.zero;

        Vector2 anchor = new Vector2(transform.position.x, transform.position.y);
        Vector2 zoneCenter = anchor;

        if (_pan.PanEnabled)
        {
            zoneCenter += _pan.PanWorld;
        }

        if (Application.isPlaying && _look.LookBiasEnabled)
        {
            zoneCenter += _biasOffset;
        }

        Vector3 a = new Vector3(zoneCenter.x - half.x, zoneCenter.y - half.y, z);
        Vector3 b = new Vector3(zoneCenter.x + half.x, zoneCenter.y - half.y, z);
        Vector3 c = new Vector3(zoneCenter.x + half.x, zoneCenter.y + half.y, z);
        Vector3 d = new Vector3(zoneCenter.x - half.x, zoneCenter.y + half.y, z);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        if (Application.isPlaying && _look.LookDirectionEnabled)
        {
            Vector3 center = new Vector3(zoneCenter.x, zoneCenter.y, z);
            Vector3 dirEnd = center + new Vector3(_smoothedLookDir.x, _smoothedLookDir.y, 0f) * _gizmos.DirLength;
            Gizmos.DrawLine(center, dirEnd);
            Gizmos.DrawWireSphere(center, 0.05f);
        }
    }
#endif
}
