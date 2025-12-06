using System.Collections.Generic;
using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// Unified targeting system for the player.
    /// Owns the TargetingContextModel and all targeting logic.
    /// 
    /// - Hover: closest TargetableComponent anchor to mouse in screen space.
    /// - Lock-on: FOV + radius over all anchors, with cycling.
    /// 
    /// Other systems (Interactor, Inspector, Combat, Dialogue) should
    /// read from Model.CurrentTarget / Hover / Locked and never mutate it.
    /// </summary>
    /// 

    /*
    ---------------------------------------------------------------------
    TARGETING CHANNEL OVERVIEW
    ---------------------------------------------------------------------

    The targeting system exposes *three* conceptual channels of targets.
    These channels represent different levels of user intent and system
    priority. They are evaluated in this order:

        Current = Focus ?? Locked ?? Hover

    This ensures the “strongest” form of targeting always wins.

    ---------------------------------------------------------------------
    1. HOVER TARGET  (soft, passive)
    ---------------------------------------------------------------------
    - Determined by screen-space mouse position.
    - Represents "what the player is currently pointing at".
    - Updates every frame.
    - Very low commitment: simply the closest valid anchor under the mouse.

    Typical uses:
    - Highlighting
    - Showing interaction prompts
    - Soft auto-aim previews

    ---------------------------------------------------------------------
    2. LOCKED TARGET  (explicit, medium commitment)
    ---------------------------------------------------------------------
    - Set intentionally by the player (ex: right–click, tab cycle).
    - Chosen using field-of-view + radius filtering.
    - Persists until cleared or replaced.
    - Indicates "this is the entity I want to stay focused on".

    Typical uses:
    - Combat lock-on
    - Consistent camera framing
    - Multi-step interactions that require continuity

    ---------------------------------------------------------------------
    3. FOCUS TARGET  (system-owned, high commitment)
    ---------------------------------------------------------------------
    - Set by *systems* that temporarily claim exclusive targeting context.
    - Overrides both Hover and Locked.
    - Represents "the target that a subsystem has taken control over".

    Examples:
    - Inspection mode locking onto a clue
    - Dialogue initiating with an NPC
    - Cinematic sequences
    - Precision interaction states

    Focus is *not* set by player input. It is intended for subsystems that
    need temporary control of what the “true” target should be.

    ---------------------------------------------------------------------
    RESOLVED CURRENT TARGET
    ---------------------------------------------------------------------
    The system resolves the highest-priority channel:

        CurrentTarget = 
            Focus target  (if set)
            otherwise Locked target (if set)
            otherwise Hover target  (default)

    All game logic (interactions, combat, dialogue selection, UI prompts)
    should use *CurrentTarget* as the authoritative final target.

    ---------------------------------------------------------------------
    ANCHOR VS LOGICAL TARGET
    ---------------------------------------------------------------------
    Each FocusTarget contains:
    - LogicalTarget  (ITargetable)  → the actual entity/thing being targeted
    - Anchor         (TargetableComponent) → the specific subobject hit

    Example:
    - LogicalTarget = “Zombie #12”
    - Anchor = “ZombieHeadBone (anchor offset +2.1m)”

    This lets systems act on the entity while still knowing *exactly*
    which sub-component was pointed at.
    ---------------------------------------------------------------------
    */

    public class TargeterComponent : MonoBehaviour
    {
        [Header("Model (read-only to other systems)")]
        public TargetingContextModel Model { get; private set; }

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerCenter;

        [Header("Hover (Mouse) Settings")]
        [Tooltip("Max world-space distance from the player to consider a target for hover.")]
        [SerializeField] private float hoverMaxWorldDistance = 20f;

        [Tooltip("Max screen-space distance in pixels from mouse to anchor projection.")]
        [SerializeField] private float hoverMaxScreenDistance = 64f;

        [Header("Lock-On (FOV) Settings")]
        [SerializeField] private float lockRadius = 20f;

        [SerializeField, Range(1f, 180f)]
        private float lockFovDegrees = 60f;

        [Header("Input (example keys)")]
        [SerializeField] private KeyCode lockFromHoverKey = KeyCode.Mouse1; // right-click
        [SerializeField] private KeyCode cycleLockKey = KeyCode.Tab;        // cycle lock
        [SerializeField] private KeyCode clearLockKey = KeyCode.Escape;     // clear lock

        [Header("Debug")]
        [SerializeField] private bool logCurrentTargetChanges = false;

        // --------------------------------------------------------------------
        // Runtime current targets (not serialized – for code / debugging)
        // --------------------------------------------------------------------
        [HideInInspector] public FocusTarget CurrentHover;
        [HideInInspector] public FocusTarget CurrentLocked;
        [HideInInspector] public FocusTarget CurrentFocus;
        [HideInInspector] public FocusTarget Current;

        // --------------------------------------------------------------------
        // Inspector-visible debug labels (strings *are* serializable)
        // --------------------------------------------------------------------
        [Header("Debug - Current Targets (Editor View Only)")]
        [SerializeField] private string debugHoverLabel;
        [SerializeField] private string debugLockedLabel;
        [SerializeField] private string debugFocusLabel;
        [SerializeField] private string debugCurrentLabel;

        private float _hoverMaxScreenDistanceSqr;
        private readonly List<TargetableComponent> _lockCandidates = new();

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCenter == null)
                playerCenter = transform;

            _hoverMaxScreenDistanceSqr = hoverMaxScreenDistance * hoverMaxScreenDistance;

            Model = new TargetingContextModel();

            // Hook events to keep both runtime and string debug fields in sync
            Model.HoverChanged += OnHoverChanged;
            Model.LockedChanged += OnLockedChanged;
            Model.FocusChanged += OnFocusChanged;
            Model.CurrentTargetChanged += OnCurrentChanged;

            if (logCurrentTargetChanges)
            {
                Model.CurrentTargetChanged += t =>
                    Debug.Log($"[Targeting] Current -> {t?.TargetLabel ?? "null"}");
            }
        }

        private void OnDestroy()
        {
            if (Model == null) return;

            Model.HoverChanged -= OnHoverChanged;
            Model.LockedChanged -= OnLockedChanged;
            Model.FocusChanged -= OnFocusChanged;
            Model.CurrentTargetChanged -= OnCurrentChanged;
        }

        private void Update()
        {
            UpdateHoverFromMouse();
            HandleLockInput();
        }

        // ====================================================================
        // Event handlers – update runtime FocusTargets + inspector debug labels
        // ====================================================================

        private void OnHoverChanged(FocusChange change)
        {
            CurrentHover = change.Current;
            debugHoverLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnLockedChanged(FocusChange change)
        {
            CurrentLocked = change.Current;
            debugLockedLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnFocusChanged(FocusChange change)
        {
            CurrentFocus = change.Current;
            debugFocusLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnCurrentChanged(FocusTarget target)
        {
            Current = target;
            debugCurrentLabel = target?.TargetLabel ?? "(null)";
        }

        // ====================================================================
        // Hover: screen-space closest anchor to mouse
        // ====================================================================

        private void UpdateHoverFromMouse()
        {
            if (playerCamera == null)
                return;

            Vector3 mousePos = Input.mousePosition;
            FocusTarget bestTarget = null;
            float bestScore = float.PositiveInfinity;

            var anchors = TargetableComponent.AllAnchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                Vector3 worldPos = anchor.AnchorWorldPosition;
                float worldDist = Vector3.Distance(playerCenter.position, worldPos);
                if (worldDist > hoverMaxWorldDistance)
                    continue;

                Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0f)
                    continue; // behind camera

                Vector2 screen2 = new(screenPos.x, screenPos.y);
                Vector2 mouse2 = new(mousePos.x, mousePos.y);
                float screenDistSqr = (screen2 - mouse2).sqrMagnitude;

                if (screenDistSqr > _hoverMaxScreenDistanceSqr)
                    continue;

                if (screenDistSqr < bestScore)
                {
                    bestScore = screenDistSqr;
                    var logical = (ITargetable)anchor.LogicalRoot;

                    bestTarget = new FocusTarget(
                        logical,
                        anchor,
                        worldDist,
                        worldPos
                    );
                }
            }

            if (bestTarget != null)
                Model.SetHover(bestTarget);
            else
                Model.ClearHover();
        }

        // ====================================================================
        // Lock-On: FOV + radius + cycling
        // ====================================================================

        private void HandleLockInput()
        {
            if (Input.GetKeyDown(lockFromHoverKey))
                LockFromHover();

            if (Input.GetKeyDown(cycleLockKey))
                CycleLockFromFov();

            if (Input.GetKeyDown(clearLockKey))
                Model.ClearLocked();
        }

        private void LockFromHover()
        {
            var hover = Model.Hover;
            if (hover == null)
                return;

            var logical = hover.LogicalTarget;
            var anchor = hover.Anchor;

            if (logical == null || anchor == null)
                return;

            Vector3 worldPos = anchor.AnchorWorldPosition;
            float dist = Vector3.Distance(playerCenter.position, worldPos);

            var locked = new FocusTarget(
                logical,
                anchor,
                dist,
                worldPos
            );

            Model.SetLocked(locked);
        }

        private void CycleLockFromFov()
        {
            FindLockCandidates(_lockCandidates);
            if (_lockCandidates.Count == 0)
            {
                Model.ClearLocked();
                return;
            }

            TargetableComponent next = GetNextLockCandidate(_lockCandidates, Model.Locked);
            if (next == null)
            {
                Model.ClearLocked();
                return;
            }

            var logical = (ITargetable)next.LogicalRoot;
            Vector3 pos = next.AnchorWorldPosition;
            float dist = Vector3.Distance(playerCenter.position, pos);

            var locked = new FocusTarget(
                logical,
                next,
                dist,
                pos
            );

            Model.SetLocked(locked);
        }

        private void FindLockCandidates(List<TargetableComponent> results)
        {
            results.Clear();

            var anchors = TargetableComponent.AllAnchors;
            var origin = playerCenter.position;
            var forward = playerCenter.forward;
            float halfFov = lockFovDegrees * 0.5f;
            float radiusSqr = lockRadius * lockRadius;

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null) continue;

                Vector3 pos = anchor.AnchorWorldPosition;
                Vector3 to = pos - origin;
                float distSqr = to.sqrMagnitude;
                if (distSqr > radiusSqr)
                    continue;

                float dist = Mathf.Sqrt(distSqr);
                if (dist < 0.0001f)
                    continue;

                Vector3 dir = to / dist;
                float angle = Vector3.Angle(forward, dir);
                if (angle > halfFov)
                    continue;

                results.Add(anchor);
            }
        }

        private TargetableComponent GetNextLockCandidate(List<TargetableComponent> candidates, FocusTarget currentLocked)
        {
            if (candidates.Count == 0)
                return null;

            if (currentLocked?.Anchor == null)
                return GetBestAligned(candidates);

            var currentAnchor = currentLocked.Anchor;
            int index = candidates.IndexOf(currentAnchor);

            if (index < 0)
                return GetBestAligned(candidates);

            int nextIndex = (index + 1) % candidates.Count;
            return candidates[nextIndex];
        }

        private TargetableComponent GetBestAligned(List<TargetableComponent> candidates)
        {
            if (candidates.Count == 0)
                return null;

            var origin = playerCenter.position;
            var forward = playerCenter.forward;

            TargetableComponent best = null;
            float bestDot = float.NegativeInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                var anchor = candidates[i];
                if (anchor == null) continue;

                Vector3 dir = (anchor.AnchorWorldPosition - origin).normalized;
                float dot = Vector3.Dot(forward, dir);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = anchor;
                }
            }

            return best;
        }
    }
}
