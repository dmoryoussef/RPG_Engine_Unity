using UnityEngine;
using Core;

public class CameraRegistry : MonoBehaviour
{
    private void OnEnable()
    {
        Registry.Register<Camera>(GetComponent<Camera>());
    }

    private void OnDisable()
    {
        Registry.Unregister<Camera>(GetComponent<Camera>());
    }
}
