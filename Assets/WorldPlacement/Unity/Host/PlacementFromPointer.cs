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

        [Header("Placement Settings")]
        [SerializeField] private Rotation4 rotation = Rotation4.R0;
        [SerializeField] private bool requireNonDefaultTile = true;

        private void Awake()
        {
            if (pointer == null)
                Debug.LogError("PlacementFromPointer: WorldPointer2D not assigned.", this);

            if (host == null)
                Debug.LogError("PlacementFromPointer: WorldPlacementHost not assigned.", this);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(placeKey))
                return;

            // --- Pointer / Wiring Errors Only ---

            if (selected == null)
            {
                Debug.LogWarning("WorldPlacement: No placeable set (selected is null).", this);
                return;
            }

            if (host == null || host.System == null)
            {
                Debug.LogWarning("WorldPlacement: Host/System not ready.", this);
                return;
            }

            if (pointer == null)
            {
                Debug.LogWarning("WorldPlacement: Pointer not assigned.", this);
                return;
            }

            var hit = pointer.CurrentHit;

            if (!hit.Valid)
            {
                Debug.LogWarning("WorldPlacement: No valid hover hit (pointer not over world plane).", this);
                return;
            }

            // --- Placement Logic ---

            var def = selected.ToRuntime(); // MVP: rebuild each time (safe & simple)
            var cell = new Cell2i(hit.Cell.X, hit.Cell.Y);

            var report = host.System.Evaluate(def, cell, rotation, requireNonDefaultTile);

            if (!report.Allowed)
            {
                Debug.LogWarning(
                    $"WorldPlacement: BLOCKED '{def.Id}' at {cell} rot={rotation}.",
                    this);

                for (int i = 0; i < report.Reasons.Count; i++)
                {
                    var reason = report.Reasons[i];
                    Debug.LogWarning($"  - {reason.Message} @ {reason.Cell}", this);
                }

                return;
            }

            // Placement is valid — commit
            if (host.System.TryPlace(def, cell, rotation, requireNonDefaultTile, out var inst))
            {
                Debug.Log(
                    $"WorldPlacement: Placed '{inst.DefId}' id={inst.InstanceId} at {cell} rot={rotation}",
                    this);
            }
            else
            {
                // Should not normally happen (Evaluate just passed),
                // but protects against race conditions in future systems.
                Debug.LogWarning(
                    $"WorldPlacement: Placement unexpectedly failed after successful evaluation at {cell}.",
                    this);
            }
        }
    }
}
