using UnityEngine;

[ExecuteAlways]
public class PixelSnapCamera2D : MonoBehaviour
{
    [Header("Match your art setup")]
    public int pixelsPerUnit = 32;
    public int referenceHeight = 360; // your base vertical resolution (e.g., 640x360)

    Camera _cam;
    Vector3 _lastPos;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    void LateUpdate()
    {
        if (_cam == null || !_cam.orthographic) return;

        // world units per screen pixel at current ortho size:
        // pixelsPerWorldUnitOnScreen = Screen.height / (2 * orthoSize)
        // so worldUnitsPerPixel = 1 / pixelsPerWorldUnitOnScreen
        float worldUnitsPerPixel = (2f * _cam.orthographicSize) / Screen.height;

        // snap the XY position to the nearest pixel grid in world space
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / worldUnitsPerPixel) * worldUnitsPerPixel;
        pos.y = Mathf.Round(pos.y / worldUnitsPerPixel) * worldUnitsPerPixel;
        transform.position = pos;
    }
}

