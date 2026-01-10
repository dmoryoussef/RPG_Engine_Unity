using UnityEngine;

/// <summary>
/// Input adapter: middle-mouse drag pans the camera by writing to CameraRig2D.PanWorld.
/// Works in Editor and builds.
/// Gated so it only pans when the mouse is over the rig's camera pixelRect and the app is focused.
/// </summary>
public sealed class CameraMiddleMousePan2D : MonoBehaviour
{
    [SerializeField] private CameraRig2D _rig;

    [Header("Pan Settings")]
    [Tooltip("Pan sensitivity multiplier applied after pixel->world conversion.\nHigher = more panning per drag.")]
    [Min(0f)]
    [SerializeField] private float _panSensitivity = 1.0f;

    [Tooltip("Invert drag direction if desired.")]
    [SerializeField] private bool _invert;

    [Tooltip("If true, only pan while the application window is focused (recommended).")]
    [SerializeField] private bool _requireAppFocus = true;

    private bool _isPanning;
    private Vector3 _lastMousePos;

    private void Reset()
    {
        _rig = GetComponent<CameraRig2D>();
    }

    private void Update()
    {
        if (_rig == null)
        {
            return;
        }

        UnityEngine.Camera cam = _rig.ViewCamera;
        if (cam == null)
        {
            return;
        }

        if (_requireAppFocus && !Application.isFocused)
        {
            _isPanning = false;
            return;
        }

        Vector3 mousePos = Input.mousePosition;

        // Only start (and continue) pan if mouse is over this camera's render rect.
        if (!IsMouseOverCamera(cam, mousePos))
        {
            // If we leave the camera rect during a drag, stop cleanly.
            _isPanning = false;
            return;
        }

        if (Input.GetMouseButtonDown(2))
        {
            _isPanning = true;
            _lastMousePos = mousePos;
        }

        if (Input.GetMouseButtonUp(2))
        {
            _isPanning = false;
        }

        if (!_isPanning)
        {
            return;
        }

        Vector3 deltaPixels = mousePos - _lastMousePos;
        _lastMousePos = mousePos;

        if (cam.pixelHeight <= 0)
        {
            return;
        }

        // Orthographic: world units per screen pixel.
        float worldPerPixel = (cam.orthographicSize * 2f) / cam.pixelHeight;

        // Dragging right should pan camera left (world moves opposite mouse).
        Vector2 deltaWorld = new Vector2(-deltaPixels.x * worldPerPixel, -deltaPixels.y * worldPerPixel);

        if (_invert)
        {
            deltaWorld = -deltaWorld;
        }

        deltaWorld *= _panSensitivity;

        _rig.AddPanWorld(deltaWorld);
    }

    private static bool IsMouseOverCamera(UnityEngine.Camera cam, Vector3 mousePos)
    {
        // In some cases (alt-tab / multi-display), mousePos can be outside screen bounds.
        // Camera.pixelRect.Contains uses screen-space coordinates.
        if (mousePos.x < 0f || mousePos.y < 0f || mousePos.x > Screen.width || mousePos.y > Screen.height)
        {
            return false;
        }

        return cam.pixelRect.Contains(mousePos);
    }
}
