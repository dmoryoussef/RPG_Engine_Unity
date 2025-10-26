using UnityEngine;
using RPG.Foundation;

namespace RPG.Player
{
    /// <summary>
    /// Press a key to search for an IInteractable in front of the player and call OnInteract().
    /// - Red console logs (via LogRed) never pause the game.
    /// - No layers required by default (searches ALL).
    /// - Inspector shows all runtime info; gizmos remain.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMover2D))]
    public class PlayerInteract : MonoBehaviour
    {
        // ---------- References ----------
        [Header("References")]
        [SerializeField] private PlayerMover2D mover;

        // ---------- Input ----------
        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        // ---------- Probe Settings ----------
        public enum ProbeMode { Circle, Ray }
        [Header("Probe")]
        [SerializeField] private ProbeMode probeMode = ProbeMode.Circle;
        [SerializeField] private float probeOffset = 0.45f;
        [SerializeField] private float probeRadius = 0.45f;
        [SerializeField] private float rayDistance = 0.9f;
        [SerializeField] private bool useLayerMask = false;
        [SerializeField] private LayerMask interactableLayers = default;
        [SerializeField] private int maxResults = 4;

        // ---------- Selection ----------
        public enum SelectionSort { Nearest, First, BestFacing }
        [Header("Selection")]
        [SerializeField] private SelectionSort selectionSort = SelectionSort.Nearest;
        [Range(-1f, 1f)][SerializeField] private float minFacingDot = 0.0f;
        [SerializeField] private Vector2 idleFacing = Vector2.down;

        // ---------- Cooldown ----------
        [Header("Cooldown")]
        [SerializeField] private float cooldownSeconds = 0.0f;

        // ---------- Runtime Debug ----------
        [Header("Runtime (Read-Only)")]
        [SerializeField] private string validationStatus = "Not validated";
        [SerializeField] private bool validationPassed = true;
        [SerializeField] private Vector2 lastFacing = Vector2.down;
        [SerializeField] private Vector2 lastProbeCenter = Vector2.zero;
        [SerializeField] private string lastTargetId = "<none>";
        [SerializeField] private bool lastSuccess = false;
        [SerializeField] private float lastSuccessTime = -999f;

        private Collider2D[] _hits;
        private bool _warnedNoMover;
        private bool _warnedMaskZero;

        // ---------------- Validation ----------------
        private void OnValidate()
        {
            probeOffset = Mathf.Max(0f, probeOffset);
            probeRadius = Mathf.Max(0.05f, probeRadius);
            rayDistance = Mathf.Max(0.05f, rayDistance);
            maxResults = Mathf.Max(0, maxResults);
            int desired = Mathf.Max(1, maxResults == 0 ? 1 : maxResults);
            if (_hits == null || _hits.Length != desired) _hits = new Collider2D[desired];
            if (mover == null) mover = GetComponent<PlayerMover2D>();
            SoftValidate();
        }

        private void Awake()
        {
            SoftValidate();
            int desired = Mathf.Max(1, maxResults == 0 ? 1 : maxResults);
            if (_hits == null || _hits.Length != desired) _hits = new Collider2D[desired];
        }

        private void SoftValidate()
        {
            validationPassed = true;
            validationStatus = "OK";

            if (mover == null)
            {
                validationPassed = false;
                validationStatus = "Missing PlayerMover2D (using idleFacing).";
                if (!_warnedNoMover)
                {
                    _warnedNoMover = true;
                    LogRed($"[PlayerInteract] {validationStatus} (GameObject: {name})");
                }
            }

            if (useLayerMask && interactableLayers.value == 0)
            {
                validationPassed = false;
                validationStatus = "LayerMask is zero while enabled.";
                if (!_warnedMaskZero)
                {
                    _warnedMaskZero = true;
                    LogRed($"[PlayerInteract] {validationStatus} (GameObject: {name})");
                }
            }
        }

        // ---------------- Main Logic ----------------
        private void Update()
        {
            Vector2 facing = (mover ? mover.Facing : idleFacing);
            if (facing.sqrMagnitude < 0.0001f) facing = idleFacing;
            lastFacing = facing.normalized;

            if (!Input.GetKeyDown(interactKey)) return;

            float sinceSuccess = Time.realtimeSinceStartup - lastSuccessTime;
            if (cooldownSeconds > 0f && sinceSuccess < cooldownSeconds)
            {
                LogRed($"[PlayerInteract] On cooldown ({sinceSuccess:0.00}/{cooldownSeconds:0.00}s).");
                return;
            }

            int mask = useLayerMask ? interactableLayers.value : ~0;
            IInteractable target = null;
            Vector2 origin = transform.position;

            if (probeMode == ProbeMode.Circle)
            {
                Vector2 center = origin + lastFacing * probeOffset;
                lastProbeCenter = center;
                int count = Physics2D.OverlapCircleNonAlloc(center, probeRadius, _hits, mask);
                if (count > 0)
                    target = SelectBestInteractable(_hits, count, center, lastFacing);
            }
            else
            {
                lastProbeCenter = origin;
                RaycastHit2D hit = Physics2D.Raycast(origin, lastFacing, rayDistance, mask);
                if (hit.collider != null)
                    hit.collider.TryGetComponent<IInteractable>(out target);
            }

            if (target != null)
            {
                lastTargetId = target.InteractableId ?? target.ToString();
                lastSuccess = target.OnInteract();

                if (lastSuccess)
                {
                    lastSuccessTime = Time.realtimeSinceStartup;
                }
                else
                {
                    LogRed($"[PlayerInteract] Target '{lastTargetId}' returned false.");
                }
            }
            else
            {
                lastTargetId = "<none>";
                lastSuccess = false;
                LogRed("[PlayerInteract] Nothing to interact with.");
            }
        }

        private IInteractable SelectBestInteractable(Collider2D[] hits, int count, Vector2 center, Vector2 facing)
        {
            IInteractable best = null;
            float bestScore = float.NegativeInfinity;
            int limit = (maxResults == 0) ? count : Mathf.Min(count, maxResults);

            for (int i = 0; i < limit; i++)
            {
                var col = hits[i];
                if (col == null) continue;

                if (!col.TryGetComponent<IInteractable>(out var candidate))
                    continue;

                Vector2 to = ((Vector2)col.bounds.center - center).normalized;
                float dot = Vector2.Dot(facing, to);
                if (dot < minFacingDot) continue;

                float score = selectionSort switch
                {
                    SelectionSort.First => 0f,
                    SelectionSort.Nearest => -Vector2.SqrMagnitude((Vector2)col.bounds.center - center),
                    SelectionSort.BestFacing => dot * 10f - Vector2.SqrMagnitude((Vector2)col.bounds.center - center) * 0.1f,
                    _ => 0f
                };

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        // ---------------- Gizmos ----------------
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = (mover ? mover.Facing : idleFacing);
            if (facing.sqrMagnitude < 0.0001f) facing = idleFacing;
            facing.Normalize();

            Vector2 origin = transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + facing * Mathf.Max(probeOffset, rayDistance));

#if UNITY_EDITOR
            if (probeMode == ProbeMode.Circle)
            {
                Vector2 center = Application.isPlaying ? lastProbeCenter : origin + facing * probeOffset;
                UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.35f);
                UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, probeRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(center, probeRadius);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, origin + facing * rayDistance);
            }
#endif
        }

        // ---------- Helper ----------
        private void LogRed(string msg) => Debug.Log($"<color=red>{msg}</color>");
    }
}
