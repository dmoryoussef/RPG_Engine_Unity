using Core.Persistence;
using Persistence;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldGrid.Runtime.Persistence;
using WorldGrid.Runtime.SaveAdapters;
using WorldGrid.Unity;

public class SaveDriver : MonoBehaviour
{
    [SerializeField] private WorldHost worldHost;

    [SerializeField, Tooltip("Subfolder under Application.persistentDataPath")]
    private string saveFolder = "Saves";

    [SerializeField, Tooltip("Save file name including extension (.sav)")]
    private string saveFileName = "slot1.sav";

    private string SavePath
    {
        get
        {
            string root = Application.persistentDataPath;
            string folder = Path.Combine(root, saveFolder);

            // Ensure folder exists
            Directory.CreateDirectory(folder);

            return Path.Combine(folder, saveFileName);
        }
    }


    private void Awake()
    {
        if (worldHost == null)
            worldHost = FindFirstObjectByType<WorldHost>();
    }

    public void Save()
    {
        if (worldHost == null || worldHost.World == null)
        {
            Debug.LogError("Save failed: WorldHost or World is null.");
            return;
        }

        var ctx = new SaveContext();
        ctx.Set(worldHost.World);

        var sections = new List<IDataSection> { new ChunkWorldSection() };

        PersistenceIO.SaveToFile(SavePath, ctx, sections);

        var info = new System.IO.FileInfo(SavePath);
        Debug.Log($"[SAVE] Wrote: {SavePath} | Exists={info.Exists} | LastWrite={info.LastWriteTime}");

    }

    public void Load()
    {
        var info = new System.IO.FileInfo(SavePath);
        Debug.Log($"[LOAD] Reading: {SavePath} | Exists={info.Exists} | LastWrite={info.LastWriteTime}");
        if (!info.Exists) return;

        if (!File.Exists(SavePath))
        {
            Debug.LogWarning($"No save found at: {SavePath}");
            return;
        }

        if (worldHost == null || worldHost.World == null)
        {
            Debug.LogError("Load failed: WorldHost or World is null.");
            return;
        }

        // IMPORTANT: supply the existing world instance so the section fills it
        var ctx = new SaveContext();
        ctx.Set(worldHost.World);

        var registry = new Dictionary<string, IDataSection>
        {
            ["worldgrid/sparsechunkworld"] = new ChunkWorldSection()
        };

        PersistenceIO.LoadFromFile(SavePath, ctx, registry);

        // Render refresh:
        // ChunkWorldRenderer already rebuilds dirty chunks in LateUpdate.
        // AddChunkForLoad marks dirty coords, so next frame visuals update automatically.
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Save();
            Debug.Log($"Game saved. Path: {SavePath}");
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            Load();
            Debug.Log("Game loaded.");
        }
    }
}
