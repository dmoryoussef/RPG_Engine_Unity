using UnityEngine;

/// <summary>
/// Temporary input adapter that drives CameraRig2D zoom using the mouse wheel.
/// This can be removed or replaced when input is refactored.
/// </summary>
public sealed class CameraMouseWheelZoom : MonoBehaviour
{
    [SerializeField] private CameraRig2D _rig;

    [Header("Zoom Settings")]
    [Tooltip("How much orthographic size changes per scroll notch.")]
    [SerializeField] private float _zoomStep = 0.75f;

    [Tooltip("Invert scroll direction if desired.")]
    [SerializeField] private bool _invert;

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

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.001f)
        {
            return;
        }

        float direction = _invert ? scroll : -scroll;

        _rig.AddZoomDelta(direction * _zoomStep);
    }
}
