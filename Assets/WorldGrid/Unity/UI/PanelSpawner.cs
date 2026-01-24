using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity;
using WorldGrid.Unity.Rendering;
using WorldGrid.Unity.UI;

public class PanelSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _panelPrefab;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private WorldHost _worldHost;

    private GameObject panelInstance;

    private void Start()
    {
        SpawnPanel();
    }

    public void SpawnPanel()
    {
        if (_panelPrefab == null || _canvas == null || _worldHost == null)
        {
            Debug.LogError("PanelSpawner: Missing required references.");
            return;
        }

        // Instantiate inactive so injected dependencies are set before any OnEnable work.
        panelInstance = Instantiate(_panelPrefab, _canvas.transform);
        panelInstance.SetActive(false);

        // ---- Palette UI (button grid) ----
        var palette = panelInstance.GetComponent<TilePaletteUI>();
        if (palette == null)
        {
            Debug.LogError("Spawned panel has no TilePaletteUI.");
            Destroy(panelInstance);
            return;
        }

        // Inject world host so palette can resolve provider/view and build buttons.
        palette.SetWorld(_worldHost);

        // ---- Header UI (status/context) ----
        // Header is independent of TilePaletteUI, so we bind it directly with BrushState + TileView.
        var header = panelInstance.GetComponentInChildren<TilePaletteHeaderUI>(true);
        if (header != null)
        {
            // Palette already knows which key to show; header only needs the string label.
            var key = palette.TileLibraryKey;

            // Resolve the view directly from WorldHost + key (do not route through TilePaletteUI).
            var view = resolveTileView(_worldHost, key);

            header.Bind(
                brush: palette.BrushState,
                view: view,
                libraryKey: key.ToString()
            );
        }

        panelInstance.SetActive(true);
    }

    private static ITileLibraryView resolveTileView(WorldHost worldHost, TileLibraryKey key)
    {
        if (worldHost == null || key.IsEmpty)
            return null;

        // Find any component on the WorldHost that implements ITileLibrarySource
        var behaviours = worldHost.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not ITileLibrarySource src)
                continue;

            if (!src.Has(key))
                continue;

            // Prefer runtime-safe provider method when available.
            if (src is TileLibraryProvider provider)
            {
                if (provider.TryGet(key, out var view, out _))
                    return view;

                return null;
            }

            // Interface fallback: call Get only after Has.
            try
            {
                return src.Get(key);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
