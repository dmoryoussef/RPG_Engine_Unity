using UnityEngine;
using WorldGrid.Unity;
using WorldGrid.Unity.UI;

public class PanelSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _panelPrefab;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private WorldHost _worldHost;

    private GameObject panelInstance;

    public void Awake()
    {
        SpawnPanel();
    }
    public void SpawnPanel()
    {
        if (_panelPrefab == null || _canvas == null)
        {
            Debug.LogError("Panel Prefab or Canvas is not assigned.");
            return;
        }
        panelInstance = Instantiate(_panelPrefab, _canvas.transform);
    }

    private void Start()
    {
        panelInstance.GetComponent<TilePaletteUI>().SetWorld(_worldHost);
    }
}
