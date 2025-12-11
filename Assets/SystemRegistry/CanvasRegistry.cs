using UnityEngine;
using Core;

public class CanvasRegistry : MonoBehaviour
{
    private void OnEnable()
    {
        Registry.Register<Canvas>(GetComponent<Canvas>());
    }

    private void OnDisable()
    {
        Registry.Unregister<Canvas>(GetComponent<Canvas>());
    }
}
