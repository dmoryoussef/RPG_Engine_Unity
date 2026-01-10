using UnityEngine;

/// <summary>
/// Input adapter: mouse wheel zoom drives CameraRig2D zoom target.
/// Works in Editor and builds.
/// Gated so it only zooms when the mouse is over the rig's camera pixelRect and the app is focused.
/// </summary>
public sealed class CameraMouseWheelZoom2D : MonoBehaviour
{
    [SerializeField] private CameraRig2D _rig;

    [Header("Zoom Settings")]
    [Tooltip("How much orthographic size changes per scroll unit.\nHigher = faster zoom.")]
    [Min(0f)]
    [SerializeField] private float _zoomStep = 0.75f;

    [Tooltip("Invert scroll direction if desired.")]
    [SerializeField] private bool _invert;

    [Tooltip("If true, only zoom while the application window is focused (recommended).")]
    [SerializeField] private bool _requireAppFocus = true;

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
            return;
        }

        Vector3 mousePos = Input.mousePosition;

        // Only zoom when cursor is over THIS camera's render rect.
        if (!IsMouseOverCamera(cam, mousePos))
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
        {
            return;
        }

        float direction = _invert ? scroll : -scroll;
        float deltaSize = direction * _zoomStep;

        _rig.RequestZoomDelta(deltaSize, Input.mousePosition);

    }

    private static bool IsMouseOverCamera(UnityEngine.Camera cam, Vector3 mousePos)
    {
        // Guard odd cases (alt-tab / multi-display) where mousePos can be outside screen bounds.
        if (mousePos.x < 0f || mousePos.y < 0f || mousePos.x > Screen.width || mousePos.y > Screen.height)
        {
            return false;
        }

        return cam.pixelRect.Contains(mousePos);
    }
}
