using UnityEngine;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Debug convenience: nudge ChunkWorldRenderer view window while playing.
    /// Keeps Sprint tight by avoiding camera/frustum work until later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldViewNudger : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ChunkWorldRenderer rendererSource;

        [Header("Input")]
        [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
        [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
        [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
        [SerializeField] private KeyCode downKey = KeyCode.DownArrow;

        [Tooltip("Hold this to nudge faster.")]
        [SerializeField] private KeyCode fastModifier = KeyCode.LeftShift;

        [Header("Step")]
        [SerializeField] private int stepChunks = 1;
        [SerializeField] private int fastStepChunks = 4;

        [Header("Behavior")]
        [Tooltip("If true, destroys chunk view GameObjects outside the current window when nudging.")]
        [SerializeField] private bool pruneViewsOutsideWindow = false;

        private void Awake()
        {
            if (rendererSource == null)
            {
                UnityEngine.Debug.LogError("WorldViewNudger: rendererSource not assigned.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!enabled)
                return;

            int step = UnityEngine.Input.GetKey(fastModifier) ? Mathf.Max(1, fastStepChunks) : Mathf.Max(1, stepChunks);

            int dx = 0;
            int dy = 0;

            if (UnityEngine.Input.GetKeyDown(leftKey)) dx -= step;
            if (UnityEngine.Input.GetKeyDown(rightKey)) dx += step;
            if (UnityEngine.Input.GetKeyDown(downKey)) dy -= step;
            if (UnityEngine.Input.GetKeyDown(upKey)) dy += step;

            if (dx == 0 && dy == 0)
                return;

            rendererSource.NudgeViewChunkMin(dx, dy, pruneViewsOutsideWindow);
        }
    }
}
