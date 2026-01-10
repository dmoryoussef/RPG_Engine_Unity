using UnityEngine;
using Player; // PlayerMover2D

public sealed class CameraRig2D : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private UnityEngine.Camera _camera;
    [SerializeField] private Transform _followTarget;

    [Header("Optional (Inspector Override)")]
    [Tooltip("If assigned, look bias uses this mover's Facing. If null, we try to find it on the Follow Target.")]
    [SerializeField] private PlayerMover2D _playerMover;

    [Header("Follow Smoothing")]
    [Tooltip("Higher = snappier camera movement.")]
    [SerializeField] private float _followLerp = 20f;

    [Header("World Pan (Input-agnostic)")]
    [SerializeField] private Vector2 _panWorld;

    [Header("Zoom (Input-agnostic target)")]
    [SerializeField] private float _minOrthoSize = 2f;
    [SerializeField] private float _maxOrthoSize = 20f;
    [Tooltip("Higher = snappier zoom smoothing.")]
    [SerializeField] private float _zoomLerp = 20f;

    [Header("Dead Zone (World Units)")]
    [Tooltip("Half-size of the dead-zone rectangle in world units. Camera won't move while target stays inside.")]
    [SerializeField] private Vector2 _deadZoneHalfSize = new Vector2(2.5f, 1.5f);

    [Header("Look Bias")]
    [SerializeField] private bool _useLookBias = true;

    [Tooltip("Signed world-units to shift the dead-zone center in look direction.\n" +
             "Positive tends to TRAIL; negative tends to LEAD (show more ahead).")]
    [SerializeField] private float _lookBiasDistance = -1.5f;

    [Tooltip("How quickly look direction responds to changes (higher = snappier).")]
    [SerializeField] private float _lookDirLerp = 12f;

    [Header("Facing Change Snap")]
    [Tooltip("If enabled, bias direction snaps immediately when Facing changes sharply.")]
    [SerializeField] private bool _snapBiasOnFacingChange = true;

    [Tooltip("Degrees. If Facing direction changes more than this, we snap look direction.")]
    [SerializeField] private float _facingSnapAngleDeg = 25f;

    [Tooltip("Optional time window to hold the snapped direction (seconds). 0 = no hold.")]
    [SerializeField] private float _snapHoldSeconds = 0f;

    [Header("Debug Draw")]
    [SerializeField] private bool _debugDrawInGame = false;

    [Tooltip("Draw gizmos even when object is not selected.")]
    [SerializeField] private bool _debugDrawGizmosAlways = false;

    [Tooltip("Log one gizmo snapshot on selection (for diagnosis).")]
    [SerializeField] private bool _logGizmoSnapshotOnce = false;

    [Tooltip("Length of the look direction arrow in world units.")]
    [SerializeField] private float _debugDirLength = 2.0f;

    // Smoothed look direction (unit vector in XY).
    private Vector2 _smoothedLookDir = Vector2.right;

    // Facing tracking for snap logic.
    private Vector2 _lastFacing = Vector2.right;
    private float _snapUntilTime;

    // Zoom target we smooth toward.
    private float _targetOrthoSize;

    /// <summary>
    /// Bind rig to a camera and follow target.
    /// Also refreshes cached PlayerMover2D if not explicitly assigned in inspector.
    /// </summary>
    public void Bind(UnityEngine.Camera camera, Transform followTarget)
    {
        _camera = camera;
        SetFollowTarget(followTarget);
        EnsureCameraInitialized();
    }

    /// <summary>
    /// Set follow target (and refresh mover cache if mover not manually assigned).
    /// </summary>
    public void SetFollowTarget(Transform followTarget)
    {
        _followTarget = followTarget;

        // Respect inspector assignment: only auto-find if not set.
        if (_playerMover == null && _followTarget != null)
        {
            _playerMover = _followTarget.GetComponent<PlayerMover2D>();
        }
    }

    /// <summary>
    /// Add world-space pan offset (e.g., scripted camera pan).
    /// </summary>
    public void AddPanWorld(Vector2 deltaWorld) => _panWorld += deltaWorld;

    public void ClearPan() => _panWorld = Vector2.zero;

    /// <summary>
    /// Set desired orthographic zoom (orthographicSize).
    /// </summary>
    public void SetZoom(float orthoSize)
    {
        _targetOrthoSize = Mathf.Clamp(orthoSize, _minOrthoSize, _maxOrthoSize);
    }

    /// <summary>
    /// Adjust zoom by a delta amount (positive increases ortho size = zoom out).
    /// </summary>
    public void AddZoomDelta(float delta)
    {
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + delta, _minOrthoSize, _maxOrthoSize);
    }

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = GetComponentInChildren<UnityEngine.Camera>();
        }

        if (_playerMover == null && _followTarget != null)
        {
            _playerMover = _followTarget.GetComponent<PlayerMover2D>();
        }

        EnsureCameraInitialized();
    }

    private void EnsureCameraInitialized()
    {
        if (_camera == null)
        {
            return;
        }

        _camera.orthographic = true;

        // Critical: initialize zoom target so we never lerp toward 0 by accident.
        _targetOrthoSize = Mathf.Clamp(_camera.orthographicSize, _minOrthoSize, _maxOrthoSize);
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            return;
        }

        // --------------------------------------------------------------------
        // 1) Resolve look direction from PlayerMover2D.Facing
        // --------------------------------------------------------------------
        Vector2 facing = Vector2.zero;
        if (_useLookBias && _playerMover != null)
        {
            facing = _playerMover.Facing;
        }

        Vector2 desiredLookDir = _smoothedLookDir;

        if (_useLookBias && facing.sqrMagnitude > 0.0001f)
        {
            desiredLookDir = facing.normalized;

            if (_snapBiasOnFacingChange)
            {
                float angle = Vector2.Angle(_lastFacing, desiredLookDir);
                if (angle >= _facingSnapAngleDeg)
                {
                    // Snap immediately on sharp turns
                    _smoothedLookDir = desiredLookDir;
                    _snapUntilTime = Time.time + _snapHoldSeconds;
                }

                _lastFacing = desiredLookDir;
            }
        }

        // If we're in a snap-hold window, keep snapped; otherwise smooth normally.
        if (Time.time >= _snapUntilTime)
        {
            _smoothedLookDir = Vector2.Lerp(_smoothedLookDir, desiredLookDir, _lookDirLerp * Time.deltaTime);
            if (_smoothedLookDir.sqrMagnitude > 0.0001f)
            {
                _smoothedLookDir.Normalize();
            }
        }

        // --------------------------------------------------------------------
        // 2) Dead-zone follow (world units)
        // --------------------------------------------------------------------
        Vector3 current = transform.position;
        Vector2 anchor = new Vector2(current.x, current.y);

        Vector2 zoneCenter = anchor + _panWorld;

        // Signed bias distance: negative means "lead" in practice (show more ahead),
        // positive means "trail" in practice (camera lags more).
        if (_useLookBias && _smoothedLookDir.sqrMagnitude > 0.0001f && Mathf.Abs(_lookBiasDistance) > 0.0001f)
        {
            zoneCenter += _smoothedLookDir.normalized * _lookBiasDistance;
        }

        if (_followTarget != null)
        {
            Vector2 targetPos = (Vector2)_followTarget.position;
            Vector2 delta = targetPos - zoneCenter;

            float moveX = 0f;
            if (delta.x > _deadZoneHalfSize.x) moveX = delta.x - _deadZoneHalfSize.x;
            else if (delta.x < -_deadZoneHalfSize.x) moveX = delta.x + _deadZoneHalfSize.x;

            float moveY = 0f;
            if (delta.y > _deadZoneHalfSize.y) moveY = delta.y - _deadZoneHalfSize.y;
            else if (delta.y < -_deadZoneHalfSize.y) moveY = delta.y + _deadZoneHalfSize.y;

            anchor += new Vector2(moveX, moveY);
        }

        Vector3 desiredPos = new Vector3(anchor.x, anchor.y, current.z);
        transform.position = Vector3.Lerp(transform.position, desiredPos, _followLerp * Time.deltaTime);

        // --------------------------------------------------------------------
        // 3) Smooth zoom
        // --------------------------------------------------------------------
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetOrthoSize, _zoomLerp * Time.deltaTime);

        // --------------------------------------------------------------------
        // 4) Runtime debug drawing
        // --------------------------------------------------------------------
        if (_debugDrawInGame)
        {
            RuntimeDebugDraw(zoneCenter);
        }
    }

    private void RuntimeDebugDraw(Vector2 zoneCenter)
    {
        // Draw at Z=0 so it matches gameplay plane and is visible in 2D view.
        float z = 0f;

        Vector2 half = _deadZoneHalfSize;

        Vector3 a = new Vector3(zoneCenter.x - half.x, zoneCenter.y - half.y, z);
        Vector3 b = new Vector3(zoneCenter.x + half.x, zoneCenter.y - half.y, z);
        Vector3 c = new Vector3(zoneCenter.x + half.x, zoneCenter.y + half.y, z);
        Vector3 d = new Vector3(zoneCenter.x - half.x, zoneCenter.y + half.y, z);

        Debug.DrawLine(a, b);
        Debug.DrawLine(b, c);
        Debug.DrawLine(c, d);
        Debug.DrawLine(d, a);

        Vector2 dir = (_smoothedLookDir.sqrMagnitude > 0.0001f) ? _smoothedLookDir.normalized : Vector2.right;
        Vector3 center = new Vector3(zoneCenter.x, zoneCenter.y, z);
        Vector3 dirEnd = center + new Vector3(dir.x, dir.y, 0f) * _debugDirLength;

        Debug.DrawLine(center, dirEnd);

        if (_followTarget != null)
        {
            Vector3 target = new Vector3(_followTarget.position.x, _followTarget.position.y, z);
            Debug.DrawLine(center, target);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_debugDrawGizmosAlways)
        {
            return;
        }

        DrawGizmosInternal();
    }

    private void OnDrawGizmosSelected()
    {
        if (_debugDrawGizmosAlways)
        {
            return;
        }

        DrawGizmosInternal();
    }

    private void DrawGizmosInternal()
    {

        if (_deadZoneHalfSize.x <= 0f || _deadZoneHalfSize.y <= 0f)
        {
            return;
        }

        // Draw on the gameplay plane so it's visible in 2D Scene view even if camera is at Z=-10.
        float z = 0f;

        Vector2 anchor = new Vector2(transform.position.x, transform.position.y);
        Vector2 zoneCenter = anchor + _panWorld;

        if (Application.isPlaying && _useLookBias && _smoothedLookDir.sqrMagnitude > 0.0001f && Mathf.Abs(_lookBiasDistance) > 0.0001f)
        {
            zoneCenter += _smoothedLookDir.normalized * _lookBiasDistance;
        }

        Vector2 half = _deadZoneHalfSize;

        Vector3 a = new Vector3(zoneCenter.x - half.x, zoneCenter.y - half.y, z);
        Vector3 b = new Vector3(zoneCenter.x + half.x, zoneCenter.y - half.y, z);
        Vector3 c = new Vector3(zoneCenter.x + half.x, zoneCenter.y + half.y, z);
        Vector3 d = new Vector3(zoneCenter.x - half.x, zoneCenter.y + half.y, z);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        // Look direction arrow (debug)
        Vector2 dir = (Application.isPlaying && _smoothedLookDir.sqrMagnitude > 0.0001f)
            ? _smoothedLookDir.normalized
            : Vector2.right;

        Vector3 center = new Vector3(zoneCenter.x, zoneCenter.y, z);
        Vector3 dirEnd = center + new Vector3(dir.x, dir.y, 0f) * _debugDirLength;

        Gizmos.DrawLine(center, dirEnd);
        Gizmos.DrawWireSphere(center, 0.05f);
    }

#endif
}
