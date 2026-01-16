using System;
using UnityEngine;
using UnityEngine.EventSystems;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;

namespace WorldGrid.Unity.Input
{
    /// <summary>
    /// Single source of truth for pointer -> world -> cell/chunk/local math.
    /// Produces WorldPointerHit and raises:
    /// - Hover* events when pointer is valid on the world plane
    /// - TileHover* events only when hovered cell contains a non-default tile
    /// - PointerDown/Up/Held/Clicked for mouse buttons (0..2)
    /// </summary>
    public sealed class WorldPointer2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;

        [Tooltip("Optional. If null, uses Camera.main.")]
        [SerializeField] private Camera cameraOverride;

        [Header("Picking")]
        [Tooltip("If true, pointer becomes invalid when mouse is over UI (EventSystem).")]
        [SerializeField] private bool ignoreWhenPointerOverUi = true;

        [Header("Click")]
        [SerializeField] private float clickMaxSeconds = 0.25f;

        // Plane hover (valid on world plane)
        public event Action<WorldPointerHit> HoverEntered;
        public event Action<WorldPointerHit> HoverExited;
        public event Action<WorldPointerHit, WorldPointerHit> HoverChanged;

        // Tile hover (valid AND non-default tile)
        public event Action<WorldPointerHit> TileHoverEntered;
        public event Action<WorldPointerHit> TileHoverExited;
        public event Action<WorldPointerHit, WorldPointerHit> TileHoverChanged;

        // Buttons
        public event Action<WorldPointerHit, int> PointerDown;
        public event Action<WorldPointerHit, int> PointerUp;
        public event Action<WorldPointerHit, int> PointerHeld;
        public event Action<WorldPointerHit, int> Clicked;

        /// <summary>
        /// Last sampled hit (plane-valid hover). Default when invalid.
        /// </summary>
        public WorldPointerHit CurrentHit { get; private set; }

        /// <summary>
        /// Last sampled hit that is over an "existing" tile (non-default tileId).
        /// Default when not hovering an existing tile.
        /// </summary>
        public WorldPointerHit CurrentTileHit { get; private set; }

        private SparseChunkWorld _world;
        private ButtonState[] _buttons;

        private void Awake()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("WorldPointer2D: worldHost not assigned.", this);
                enabled = false;
                return;
            }

            _buttons = new ButtonState[3];
        }

        private void Start()
        {
            _world = worldHost.World;
            if (_world == null)
            {
                UnityEngine.Debug.LogError("WorldPointer2D: worldHost.World is null.", this);
                enabled = false;
                return;
            }

            CurrentHit = default;
            CurrentTileHit = default;
        }

        private void Update()
        {
            if (_world == null)
                return;

            if (ignoreWhenPointerOverUi && IsPointerOverUi())
            {
                UpdateHover(default);
                HandleButtons(default);
                return;
            }

            var sampled = SamplePointerHit();
            UpdateHover(sampled);
            HandleButtons(sampled);
        }

        private WorldPointerHit SamplePointerHit()
        {
            Camera cam = cameraOverride != null ? cameraOverride : Camera.main;
            if (cam == null)
                return default;

            float cellSize = worldHost.CellSize;
            if (cellSize <= 0f)
                return default;

            Transform root = worldHost.WorldRoot;
            if (root == null)
                return default;

            if (!TryGetMouseWorldOnPlaneZ0(cam, out var mouseWorld))
                return default;

            Vector3 local = root.InverseTransformPoint(mouseWorld);

            int cellX = Mathf.FloorToInt(local.x / cellSize);
            int cellY = Mathf.FloorToInt(local.y / cellSize);

            var cell = new CellCoord(cellX, cellY);

            var chunk = ChunkMath.WorldToChunk(cell, _world.ChunkSize);
            var localInChunk = ChunkMath.WorldToLocal(cell, _world.ChunkSize);

            int tileId = _world.GetTile(cellX, cellY);

            return new WorldPointerHit(
                valid: true,
                worldPoint: mouseWorld,
                localPoint: local,
                cell: cell,
                chunk: chunk,
                localX: localInChunk.X,
                localY: localInChunk.Y,
                tileId: tileId
            );
        }

        private void UpdateHover(WorldPointerHit next)
        {
            // -----------------------
            // Plane hover (existing behavior)
            // -----------------------
            var prev = CurrentHit;

            if (prev.Valid && !next.Valid)
                HoverExited?.Invoke(prev);

            if (!prev.Valid && next.Valid)
                HoverEntered?.Invoke(next);

            if (prev != next)
                HoverChanged?.Invoke(prev, next);

            CurrentHit = next;

            // -----------------------
            // Tile hover (non-default tile only)
            // -----------------------
            bool prevTileValid = IsExistingTileHit(CurrentTileHit);
            bool nextTileValid = IsExistingTileHit(next);

            if (prevTileValid && !nextTileValid)
            {
                TileHoverExited?.Invoke(CurrentTileHit);
                CurrentTileHit = default;
                return;
            }

            if (!prevTileValid && nextTileValid)
            {
                CurrentTileHit = next;
                TileHoverEntered?.Invoke(CurrentTileHit);
                return;
            }

            if (prevTileValid && nextTileValid)
            {
                // Only treat as "changed" when cell identity changes (stable + cheap).
                if (CurrentTileHit.Cell != next.Cell)
                {
                    var prevTile = CurrentTileHit;
                    CurrentTileHit = next;
                    TileHoverChanged?.Invoke(prevTile, CurrentTileHit);
                }
                else
                {
                    // Same cell; keep CurrentTileHit up-to-date (tileId may change due to writes).
                    CurrentTileHit = next;
                }
            }
        }

        private bool IsExistingTileHit(WorldPointerHit hit)
        {
            if (!hit.Valid)
                return false;

            // "Existing tile" definition: non-default tileId.
            return hit.TileId != _world.DefaultTileId;
        }

        private void HandleButtons(WorldPointerHit hit)
        {
            float now = Time.unscaledTime;

            for (int button = 0; button <= 2; button++)
            {
                if (UnityEngine.Input.GetMouseButtonDown(button))
                {
                    _buttons[button].IsDown = true;
                    _buttons[button].DownTime = now;
                    _buttons[button].DownHit = hit;

                    PointerDown?.Invoke(hit, button);
                }

                if (UnityEngine.Input.GetMouseButton(button))
                {
                    if (_buttons[button].IsDown)
                        PointerHeld?.Invoke(hit, button);
                }

                if (UnityEngine.Input.GetMouseButtonUp(button))
                {
                    PointerUp?.Invoke(hit, button);

                    if (_buttons[button].IsDown)
                    {
                        float dt = now - _buttons[button].DownTime;

                        if (dt <= clickMaxSeconds
                            && _buttons[button].DownHit.Valid
                            && hit.Valid
                            && _buttons[button].DownHit.Cell == hit.Cell)
                        {
                            Clicked?.Invoke(hit, button);
                        }
                    }

                    _buttons[button].IsDown = false;
                }
            }
        }

        private static bool TryGetMouseWorldOnPlaneZ0(Camera cam, out Vector3 world)
        {
            Ray r = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            var plane = new Plane(Vector3.forward, Vector3.zero);

            if (plane.Raycast(r, out float enter))
            {
                world = r.GetPoint(enter);
                return true;
            }

            world = default;
            return false;
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;

            return EventSystem.current.IsPointerOverGameObject();
        }

        [Serializable]
        private struct ButtonState
        {
            public bool IsDown;
            public float DownTime;
            public WorldPointerHit DownHit;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (clickMaxSeconds < 0.01f)
                clickMaxSeconds = 0.01f;
        }
#endif
    }
}
