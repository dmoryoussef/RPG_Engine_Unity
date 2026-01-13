using System;
using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;
using WorldGrid.Unity.Assets;
using WorldGrid.Unity.Input;

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Debug paint/erase tool for the tile world.
    /// - Hold LMB: paint selected tile
    /// - Hold RMB: erase (paint eraseTileId)
    ///
    /// Selection can be driven from a TileLibraryAsset by:
    /// - TileId
    /// - Exact Name
    /// - First Tag Match
    ///
    /// Rate-limited:
    /// - writes when hovered cell changes OR
    /// - after a fixed interval (minWriteIntervalSeconds)
    ///
    /// Optional square brush radius (0 => 1 cell, 1 => 3x3, 2 => 5x5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDebugPaintTool : MonoBehaviour
    {
        public enum PaintSelectionMode
        {
            TileId = 0,
            NameExact = 1,
            TagFirstMatch = 2
        }

        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private WorldPointer2D pointer;

        [Header("Optional Tile Library (Selection + Display)")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        [Header("Mode")]
        [SerializeField] private bool enablePainting = true;

        [Header("Paint Selection")]
        [SerializeField] private PaintSelectionMode paintSelectionMode = PaintSelectionMode.TileId;

        [Tooltip("Used when PaintSelectionMode=TileId, or as fallback if name/tag does not resolve.")]
        [SerializeField] private int paintTileId = 1;

        [Tooltip("Exact match against TileLibraryAsset.Entry.name (case-insensitive). Used when PaintSelectionMode=NameExact.")]
        [SerializeField] private string paintTileName = "";

        [Tooltip("First entry containing this tag wins (case-insensitive). Used when PaintSelectionMode=TagFirstMatch.")]
        [SerializeField] private string paintTag = "";

        [Header("Erase")]
        [Tooltip("TileId used when erasing (usually the world's DefaultTileId).")]
        [SerializeField] private int eraseTileId = 0;

        [Header("Brush")]
        [Tooltip("0 = single cell. 1 = 3x3. 2 = 5x5, etc.")]
        [SerializeField] private int brushRadius = 0;

        [Header("Rate Limiting")]
        [Tooltip("Minimum time between writes while staying on the same cell.")]
        [SerializeField] private float minWriteIntervalSeconds = 0.02f;

        [Tooltip("If true, always write immediately when the hovered cell changes.")]
        [SerializeField] private bool writeOnCellChange = true;

        [Header("Resolved (Read-Only)")]
        [SerializeField] private int resolvedPaintTileId;
        [SerializeField] private string resolvedPaintTileInfo;

        private TileLibrary _runtimeLibrary;

        private float _nextAllowedWriteTime;
        private bool _hasLastCell;
        private CellCoord _lastCell;

        private void Awake()
        {
            if (worldHost == null || pointer == null)
            {
                UnityEngine.Debug.LogError("WorldDebugPaintTool: Missing required references.", this);
                enabled = false;
                return;
            }

            BuildRuntimeLibraryIfPossible();
            ResolvePaintTileFromAsset();
        }

        private void OnEnable()
        {
            // In case references were assigned after Awake (rare but possible in editor).
            BuildRuntimeLibraryIfPossible();
            ResolvePaintTileFromAsset();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep inspector feedback up-to-date. Avoid throwing during edit-time.
            BuildRuntimeLibraryIfPossible();
            ResolvePaintTileFromAsset();
        }
#endif

        private void Update()
        {
            if (!enabled || !enablePainting)
                return;

            var hit = pointer.CurrentHit;
            if (!hit.Valid)
            {
                _hasLastCell = false;
                return;
            }

            bool paintHeld = UnityEngine.Input.GetMouseButton(0);
            bool eraseHeld = UnityEngine.Input.GetMouseButton(1);

            if (!paintHeld && !eraseHeld)
            {
                _hasLastCell = false;
                return;
            }

            // If user changes selection in inspector at runtime, keep it current.
            // Cheap: it’s a tiny list scan at most.
            ResolvePaintTileFromAsset();

            int targetTileId = paintHeld ? resolvedPaintTileId : eraseTileId;

            bool cellChanged = !_hasLastCell || hit.Cell != _lastCell;

            if (writeOnCellChange && cellChanged)
            {
                ApplyBrush(hit.Cell, targetTileId);
                _lastCell = hit.Cell;
                _hasLastCell = true;
                _nextAllowedWriteTime = Time.time + Mathf.Max(0f, minWriteIntervalSeconds);
                return;
            }

            if (Time.time >= _nextAllowedWriteTime)
            {
                ApplyBrush(hit.Cell, targetTileId);
                _lastCell = hit.Cell;
                _hasLastCell = true;
                _nextAllowedWriteTime = Time.time + Mathf.Max(0f, minWriteIntervalSeconds);
            }
        }

        private void ApplyBrush(CellCoord center, int tileId)
        {
            SparseChunkWorld world = worldHost.World;
            if (world == null)
                return;

            int r = Mathf.Max(0, brushRadius);

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = center.X + dx;
                    int y = center.Y + dy;
                    world.SetTile(x, y, tileId);
                }
            }
        }

        private void BuildRuntimeLibraryIfPossible()
        {
            if (tileLibraryAsset == null)
            {
                _runtimeLibrary = null;
                return;
            }

            try
            {
                // BuildRuntime validates atlas/settings; can throw if misconfigured.
                _runtimeLibrary = tileLibraryAsset.BuildRuntime();
            }
            catch
            {
                _runtimeLibrary = null;
            }
        }

        private void ResolvePaintTileFromAsset()
        {
            // Default/fallback
            resolvedPaintTileId = paintTileId;

            // If no asset, selection is just the id.
            if (tileLibraryAsset == null)
            {
                resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
                return;
            }

            var entries = tileLibraryAsset.entries;
            if (entries == null || entries.Count == 0)
            {
                resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
                return;
            }

            if (paintSelectionMode == PaintSelectionMode.TileId)
            {
                resolvedPaintTileId = paintTileId;
                resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
                return;
            }

            if (paintSelectionMode == PaintSelectionMode.NameExact)
            {
                string target = (paintTileName ?? string.Empty).Trim();
                if (target.Length > 0)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        if (e == null) continue;

                        if (string.Equals(e.name, target, StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedPaintTileId = e.tileId;
                            resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
                            return;
                        }
                    }
                }
            }
            else if (paintSelectionMode == PaintSelectionMode.TagFirstMatch)
            {
                string target = (paintTag ?? string.Empty).Trim();
                if (target.Length > 0)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        if (e == null) continue;
                        if (e.tags == null) continue;

                        for (int t = 0; t < e.tags.Count; t++)
                        {
                            if (string.Equals(e.tags[t], target, StringComparison.OrdinalIgnoreCase))
                            {
                                resolvedPaintTileId = e.tileId;
                                resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
                                return;
                            }
                        }
                    }
                }
            }

            // No match: fall back to id.
            resolvedPaintTileId = paintTileId;
            resolvedPaintTileInfo = _runtimeLibrary != null ? _runtimeLibrary.ToString(resolvedPaintTileId) : string.Empty;
        }
    }
}
