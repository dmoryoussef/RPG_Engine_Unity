using System;
using UnityEngine;
using UnityEngine.EventSystems;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;

namespace WorldGrid.Unity.Input
{
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

        public event Action<WorldPointerHit> HoverEntered;
        public event Action<WorldPointerHit> HoverExited;
        public event Action<WorldPointerHit, WorldPointerHit> HoverChanged;

        public event Action<WorldPointerHit, int> PointerDown;
        public event Action<WorldPointerHit, int> PointerUp;
        public event Action<WorldPointerHit, int> PointerHeld;
        public event Action<WorldPointerHit, int> Clicked;

        public WorldPointerHit CurrentHit { get; private set; }

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
            var prev = CurrentHit;

            if (prev.Valid && !next.Valid)
                HoverExited?.Invoke(prev);

            if (!prev.Valid && next.Valid)
                HoverEntered?.Invoke(next);

            if (prev != next)
                HoverChanged?.Invoke(prev, next);

            CurrentHit = next;
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
