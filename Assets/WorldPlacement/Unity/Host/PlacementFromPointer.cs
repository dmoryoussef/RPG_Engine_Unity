using UnityEngine;
using WorldPlacement.Runtime.Grid;
using WorldPlacement.Unity.Assets;
using WorldPlacement.Unity.Host;
using WorldGrid.Unity.Input; // WorldPointer2D

namespace WorldPlacement.Unity
{
    [DisallowMultipleComponent]
    public sealed class PlacementFromPointer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private WorldPlacementHost host;

        [Header("Selection")]
        [SerializeField] private PlacementDefAsset selected;

        [Header("Input")]
        [SerializeField] private KeyCode placeKey = KeyCode.P;
        [SerializeField] private KeyCode rotateKey = KeyCode.R;
        [SerializeField] private KeyCode removeKey = KeyCode.Backspace;

        [Header("Placement Settings")]
        [SerializeField] private Rotation4 rotation = Rotation4.R0;
        [SerializeField] private bool requireNonDefaultTile = true;

        [Header("Debug")]
        [SerializeField] private bool logSanityLineOnPlace = true;


        private void Awake()
        {
            if (pointer == null)
                UnityEngine.Debug.LogError("PlacementFromPointer: WorldPointer2D not assigned.", this);

            if (host == null)
                UnityEngine.Debug.LogError("PlacementFromPointer: WorldPlacementHost not assigned.", this);
        }

        private void Update()
        {
            // Rotate selection (cheap but very useful for validating footprint math)
            if (Input.GetKeyDown(rotateKey))
            {
                rotation = (Rotation4)(((int)rotation + 1) & 3);
                UnityEngine.Debug.Log($"WorldPlacement: Rotation set to {rotation}", this);
            }

            // Remove the instance under the cursor (debug convenience)
            if (Input.GetKeyDown(removeKey))
            {
                TryRemoveUnderPointer();
            }

            // Place
            if (Input.GetKeyDown(placeKey))
            {
                TryPlaceAtPointer();
            }
        }

        public Rotation4 Rotation => rotation;
        public bool RequireNonDefaultTile => requireNonDefaultTile;
        public PlacementDefAsset Selected => selected;

        private bool TryGetHoveredCell(out Cell2i cell)
        {
            cell = default;

            if (pointer == null)
            {
                UnityEngine.Debug.LogWarning("WorldPlacement: Pointer not assigned.", this);
                return false;
            }

            var hit = pointer.CurrentHit;
            if (!hit.Valid)
            {
                UnityEngine.Debug.LogWarning("WorldPlacement: No valid hover hit (pointer not over world plane).", this);
                return false;
            }

            cell = new Cell2i(hit.Cell.X, hit.Cell.Y);
            return true;
        }

        private void TryPlaceAtPointer()
        {
            // Pointer / wiring errors only
            if (selected == null)
            {
                UnityEngine.Debug.LogWarning("WorldPlacement: No placeable set (selected is null).", this);
                return;
            }

            if (host == null || host.System == null)
            {
                UnityEngine.Debug.LogWarning("WorldPlacement: Host/System not ready.", this);
                return;
            }

            if (!TryGetHoveredCell(out var cell))
                return;

            if (logSanityLineOnPlace)
                UnityEngine.Debug.Log($"WorldPlacement: PlaceAttempt id='{selected.Id}' at {cell} rot={rotation}", this);

            // Runtime def (MVP: rebuild each press; safe & simple)
            var def = selected.ToRuntime();

            // Use placement report exclusively for placement reasons
            var report = host.System.Evaluate(def, cell, rotation, requireNonDefaultTile);
            if (!report.Allowed)
            {
                UnityEngine.Debug.LogWarning($"WorldPlacement: BLOCKED '{def.Id}' at {cell} rot={rotation}.", this);
                for (int i = 0; i < report.Reasons.Count; i++)
                {
                    var reason = report.Reasons[i];
                    UnityEngine.Debug.LogWarning($"  - {reason.Message} @ {reason.Cell}", this);
                }
                return;
            }

            if (host.System.TryPlace(def, cell, rotation, requireNonDefaultTile, out var inst))
            {
                UnityEngine.Debug.Log($"WorldPlacement: Placed '{inst.DefId}' id={inst.InstanceId} at {cell} rot={rotation}", this);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"WorldPlacement: Placement unexpectedly failed after successful evaluation at {cell}.", this);
            }
        }

        private void TryRemoveUnderPointer()
        {
            if (host == null || host.System == null)
            {
                UnityEngine.Debug.LogWarning("WorldPlacement: Host/System not ready.", this);
                return;
            }

            if (!TryGetHoveredCell(out var cell))
                return;

            if (!host.System.TryGetAt(cell, out var inst) || inst == null)
            {
                UnityEngine.Debug.Log($"WorldPlacement: No placed instance at {cell} to remove.", this);
                return;
            }

            if (host.System.RemoveInstance(inst.InstanceId))
            {
                UnityEngine.Debug.Log($"WorldPlacement: Removed '{inst.DefId}' id={inst.InstanceId} (from cell {cell})", this);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"WorldPlacement: Failed to remove instance id={inst.InstanceId}", this);
            }
        }
    }
}
