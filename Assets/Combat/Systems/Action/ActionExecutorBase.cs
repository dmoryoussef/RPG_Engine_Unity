// ActionExecutorBase.cs
// Purpose: Shared execution logic for combat action executors (melee, AoE, projectiles).
//
// Adds (clean up):
//  - Executor-owned MoveDef + input binding + actionId grouping
//  - Auto-register / unregister with ActionTimelineController
//
// Keeps existing behavior:
//  - Timeline-driven execution while controller is in Active
//  - Standalone execution when usePipeline == false
//  - Hit de-dupe + rate limiting per phase/action
//  - Payload -> DamageResolver -> DamagePipeline apply
//  - Debug instrumentation + phase hooks

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combat
{
    public enum DebugLevel { Off, Minimal, Verbose }

    [DefaultExecutionOrder(60)]
    public abstract class ActionExecutorBase : MonoBehaviour
    {
        [Header("Control")]
        [SerializeField] protected bool usePipeline = true;

        [Tooltip("Controller driving this executor (auto wired if on same GO).")]
        [SerializeField] protected ActionTimelineController controller;

        [Header("Authoring")]
        [Tooltip("The MoveDef this executor starts when its input/gates allow.")]
        [SerializeField] protected MoveDef moveDef;

        [Header("Input")]
        [Tooltip("Legacy KeyCode input for this executor (Press). Leave None if started externally.")]
        [SerializeField] protected KeyCode inputKey = KeyCode.None;

        [Tooltip("Executors with the same ActionId can be driven together for one action (gameplay + VFX + audio).")]
        [SerializeField] protected string actionId = "Attack";

        [Header("Debug")]
        [SerializeField] protected DebugLevel debugLevel = DebugLevel.Minimal;

        [Tooltip("If true, logs phase enter/exit and finish events from the controller.")]
        [SerializeField] protected bool debugActionEvents = true;

        [Tooltip("Optional pretty name for logs; defaults to GameObject name.")]
        [SerializeField] protected string logContextName = null;

        // Per-frame per-target de-dupe
        protected readonly HashSet<GameObject> _hitThisFrame = new();

        // Per-action-phase de-dupe + rate limiting
        protected readonly Dictionary<(uint actionId, ActionPhase phase), HashSet<GameObject>> _hitsByActionPhase = new();
        protected readonly Dictionary<((uint actionId, ActionPhase phase) key, GameObject target), int> _lastHitMs = new();

        // Cached attacker IActionTarget (found in parents)
        private IActionTarget _cachedAttackerTarget;

        public event System.Action<GameObject, Vector3, uint, ActionPhase> OnHitLogged;
        public event System.Action<ActionPhase, MoveDef.Phase> PhaseEntered;
        public event System.Action<ActionPhase, MoveDef.Phase> PhaseExited;
        public event System.Action<ActionPhase, MoveDef.Phase, float> PhaseTicked;
        public event System.Action<uint> ActionFinished;

        // Exposed for controller selection / inspection
        public ActionTimelineController Controller => controller;
        public MoveDef AuthoredMoveDef => moveDef;
        public KeyCode InputKey => inputKey;
        public string ActionId => actionId;

        protected virtual void OnPhaseEnterHook(ActionPhase phase, MoveDef.Phase phaseDef) { }
        protected virtual void OnPhaseExitHook(ActionPhase phase, MoveDef.Phase phaseDef) { }
        protected virtual void OnPhaseTickHook(ActionPhase phase, MoveDef.Phase phaseDef, float dt) { }
        protected virtual void OnActionFinishedHook(uint actionId) { }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle / wiring
        // ─────────────────────────────────────────────────────────────────────

        protected virtual void Reset()
        {
            AutoWireController();
            usePipeline = true;
        }

        protected virtual void Awake()
        {
            AutoWireController();
            usePipeline = true;
        }

        protected virtual void OnEnable()
        {
            AutoWireController();

            if (controller)
            {
                // Optional: controller keeps registry of executors
                controller.RegisterExecutor(this);

                controller.OnFinished += HandleFinished;
                controller.OnPhaseEnter += HandlePhaseEnter;
                controller.OnPhaseExit += HandlePhaseExit;
            }
        }

        protected virtual void OnDisable()
        {
            if (controller)
            {
                controller.UnregisterExecutor(this);

                controller.OnFinished -= HandleFinished;
                controller.OnPhaseEnter -= HandlePhaseEnter;
                controller.OnPhaseExit -= HandlePhaseExit;
            }
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
            {
                AutoWireController();
                usePipeline = true;
            }
        }
#endif

        protected void AutoWireController()
        {
            if (!controller && TryGetComponent(out ActionTimelineController found))
                controller = found;
        }

        /// <summary>Used by ActionTimelineController to drive only its own executors.</summary>
        public bool IsMyController(ActionTimelineController c) => controller == c;

        /// <summary>
        /// Controller calls this to see if this executor wants to start an action now.
        /// Override for holds/releases or for real input system integration.
        /// </summary>
        public virtual bool WantsToStart()
        {
            if (!enabled || !gameObject.activeInHierarchy) return false;
            if (inputKey == KeyCode.None) return false;
            return Input.GetKeyDown(inputKey);
        }

        /// <summary>
        /// Optional local gate for standalone execution when usePipeline == false.
        /// </summary>
        protected virtual bool IsLocallyGated() => true;

        protected virtual void Update()
        {
            // Keep your existing “verbose enables resolver logs” behavior.
            ActionResolver.DebugLogging = (debugLevel == DebugLevel.Verbose);
        }

        protected virtual void LateUpdate()
        {
            // Standalone executors can run without controller.
            if (usePipeline) return;
            ExecuteFrame(new CombatFrame(Time.time, Time.deltaTime));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Main execution entry
        // ─────────────────────────────────────────────────────────────────────

        public void ExecuteFrame(CombatFrame frame)
        {
            if (!HasValidRig())
            {
                if (debugLevel != DebugLevel.Off)
                    Debug.Log($"[Exec:{PrettyName()}] ExecuteFrame skipped – invalid rig.", this);
                return;
            }

            ActionPhase phaseId = controller != null ? controller.CurrentPhase : ActionPhase.Active;

            // Controller-driven path: only execute during Active
            if (controller != null && phaseId != ActionPhase.Active)
            {
                if (debugLevel == DebugLevel.Verbose)
                    Debug.Log($"[Exec:{PrettyName()}] ExecuteFrame skipped – phase={phaseId} (needs Active).", this);
                return;
            }

            // Standalone local gating (if you keep standalone mode)
            if (controller == null && !usePipeline && !IsLocallyGated())
            {
                if (debugLevel == DebugLevel.Verbose)
                    Debug.Log($"[Exec:{PrettyName()}] ExecuteFrame skipped – standalone gate closed.", this);
                return;
            }

            if (debugLevel != DebugLevel.Off)
            {
                Debug.Log($"[Exec:{PrettyName()}] ExecuteFrame at t={frame.Time:0.000}, phase={phaseId}, controller={(controller ? controller.name : "<none>")}", this);
            }

            _hitThisFrame.Clear();

            var phaseDef = ResolvePhaseDefFor(phaseId);

            // Phase tick hooks
            OnPhaseTickHook(phaseId, phaseDef, frame.DeltaTime);
            PhaseTicked?.Invoke(phaseId, phaseDef, frame.DeltaTime);

            // Step 1: collect hits
            var hitsEnum = CollectHits(frame)
                           ?? Enumerable.Empty<(GameObject target,
                                                IActionTarget targetComponent,
                                                Vector3 point,
                                                Vector3 normal,
                                                string region,
                                                float param)>();

            var hits = hitsEnum as IList<(GameObject target,
                                          IActionTarget targetComponent,
                                          Vector3 point,
                                          Vector3 normal,
                                          string region,
                                          float param)>
                       ?? hitsEnum.ToList();

            if (debugLevel != DebugLevel.Off)
            {
                Debug.Log($"[Exec:{PrettyName()}] CollectHits returned {hits.Count} candidates.", this);
            }

            var activePhase = GetActivePhaseOrNull(); // used for hit policy + payload defaults (existing behavior)
            var phaseKey = CurrentKey();

            if (!_hitsByActionPhase.TryGetValue(phaseKey, out var setForPhase))
                _hitsByActionPhase[phaseKey] = setForPhase = new HashSet<GameObject>();

            int applied = 0;

            foreach (var hit in hits)
            {
                if (hit.target == null)
                {
                    if (debugLevel == DebugLevel.Verbose)
                        Debug.LogWarning($"[Exec:{PrettyName()}] Skipping hit with null target GameObject.", this);
                    continue;
                }

                if (hit.targetComponent == null)
                {
                    if (debugLevel != DebugLevel.Off)
                        Debug.LogWarning($"[Exec:{PrettyName()}] Skipping {hit.target.name} – no IActionTarget found in parent chain.", this);
                    continue;
                }

                // Per-frame target de-dupe
                if (!_hitThisFrame.Add(hit.target))
                {
                    if (debugLevel == DebugLevel.Verbose)
                        Debug.Log($"[Exec:{PrettyName()}] Skipping {hit.target.name} – already hit this frame.", this);
                    continue;
                }

                int nowMs = Mathf.RoundToInt(frame.Time * 1000f);
                if (!PassesDeDupeAndRateLimit(hit.target, nowMs, activePhase, phaseKey, setForPhase))
                {
                    if (debugLevel == DebugLevel.Verbose)
                        Debug.Log($"[Exec:{PrettyName()}] Skipping {hit.target.name} – blocked by phase de-dupe / rate limiting.", this);
                    continue;
                }

                // Build payload
                var payload = BuildPayload(hit.target, hit.point, hit.normal, hit.region, in frame, activePhase);

                if (debugLevel == DebugLevel.Verbose)
                {
                    Debug.Log(
                        $"[Exec:{PrettyName()}] Applying hit to {hit.target.name} at {hit.point} region={hit.region} (damage={payload.intent.baseDamage}).",
                        this);
                }

                // Apply via pipeline
                ApplyHit(hit.targetComponent, in payload);
                applied++;

                OnHitLogged?.Invoke(hit.target, hit.point, phaseKey.actionId, phaseKey.phase);
            }

            if (debugLevel != DebugLevel.Off)
            {
                Debug.Log($"[Exec:{PrettyName()}] Frame complete: candidates={hits.Count}, applied={applied}.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Abstract / virtual surface
        // ─────────────────────────────────────────────────────────────────────

        protected abstract bool HasValidRig();

        protected abstract IEnumerable<(GameObject target,
                                        IActionTarget targetComponent,
                                        Vector3 point,
                                        Vector3 normal,
                                        string region,
                                        float param)> CollectHits(CombatFrame frame);

        /// <summary>Override to customize which MoveDef the executor wants to start (default: moveDef field).</summary>
        public virtual MoveDef GetMoveDefToStart() => moveDef;

        /// <summary>Builds an ActionPayload for a hit; can be overridden per executor.</summary>
        protected virtual ActionPayload BuildPayload(GameObject target,
                                                     Vector3 point,
                                                     Vector3 normal,
                                                     string region,
                                                     in CombatFrame frame,
                                                     MoveDef.Phase activePhase)
        {
            var facts = new ActionPayload.Facts
            {
                instigator = gameObject,
                source = gameObject,
                target = target,
                hitPoint = point,
                hitNormal = normal,
                region = region,
                time = frame.Time,
                actionInstanceId = controller != null ? controller.ActionInstanceId : 0u,
                phaseId = controller != null ? controller.CurrentPhase : ActionPhase.Active
            };

            var intent = new ActionPayload.Intent
            {
                baseDamage = activePhase != null ? activePhase.damage.baseDamage : 10f,
                damageType = activePhase != null ? activePhase.damage.damageType : "Generic",
                stunMs = activePhase != null ? activePhase.damage.stunMs : 0,
                hitstopFrames = activePhase != null ? activePhase.damage.hitstopFrames : 0,
                knockbackPower = activePhase != null ? activePhase.damage.knockbackPower : 0f,
                impulseDirectionMode = activePhase != null ? activePhase.damage.impulseDirectionMode : ActionPayload.Intent.ImpulseDirMode.HitNormal,
                authorDirection = activePhase != null ? activePhase.damage.authorDirection : Vector3.forward,
                tags = activePhase != null ? activePhase.damage.tags : null
            };

            return new ActionPayload { facts = facts, intent = intent };
        }

        protected virtual void ApplyHit(IActionTarget targetComponent, in ActionPayload payload)
        {
            if (targetComponent == null) return;

            // Cache attacker marker
            if (_cachedAttackerTarget == null)
                _cachedAttackerTarget = GetComponentInParent<IActionTarget>();

            var attacker = _cachedAttackerTarget;

            var result = DamageResolver.Resolve(in payload);
            DamagePipeline.Apply(in payload, in result, attacker, targetComponent);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Phase resolution + hit policy
        // ─────────────────────────────────────────────────────────────────────

        protected MoveDef.Phase GetActivePhaseOrNull()
        {
            if (controller == null) return null;
            if (controller.CurrentActionDef == null) return null;
            return controller.CurrentActionDef.phases.FirstOrDefault(p => p != null && p.phaseId == ActionPhase.Active);
        }

        protected (uint actionId, ActionPhase phase) CurrentKey()
        {
            uint id = controller != null ? controller.ActionInstanceId : 0u;
            ActionPhase p = controller != null ? controller.CurrentPhase : ActionPhase.Active;
            return (id, p);
        }

        protected MoveDef.Phase ResolvePhaseDefFor(ActionPhase p)
        {
            if (controller == null) return null;
            return controller.GetCurrentPhaseOrNull();
        }

        protected bool PassesDeDupeAndRateLimit(
            GameObject target,
            int nowMs,
            MoveDef.Phase activePhase,
            (uint actionId, ActionPhase phase) key,
            HashSet<GameObject> setForPhase)
        {
            if (activePhase == null)
                return true;

            // ─────────────────────────────────────────────────────
            // Hit policy
            // ─────────────────────────────────────────────────────

            switch (activePhase.hitPolicy)
            {
                case MoveDef.HitPolicy.OncePerPhase:
                    if (setForPhase.Contains(target))
                        return false;
                    break;

                case MoveDef.HitPolicy.OncePerAction:
                    foreach (var kvp in _hitsByActionPhase)
                    {
                        if (kvp.Key.actionId != key.actionId)
                            continue;

                        if (kvp.Value.Contains(target))
                            return false;
                    }
                    break;

                case MoveDef.HitPolicy.Unlimited:
                    // no de-dupe here
                    break;
            }

            // ─────────────────────────────────────────────────────
            // Rate limiting (optional, stacks with hitPolicy)
            // ─────────────────────────────────────────────────────

            if (activePhase.rateLimitMs > 0)
            {
                var rateKey = (key, target);
                if (_lastHitMs.TryGetValue(rateKey, out var lastMs))
                {
                    if (nowMs - lastMs < activePhase.rateLimitMs)
                        return false;
                }

                _lastHitMs[rateKey] = nowMs;
            }

            // Track hit for policy enforcement
            setForPhase.Add(target);
            return true;
        }


        protected string PrettyName()
        {
            var n = !string.IsNullOrEmpty(logContextName) ? logContextName : name;
            return n;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Controller event handlers
        // ─────────────────────────────────────────────────────────────────────

        protected virtual void HandlePhaseEnter(ActionPhase p)
        {
            var phaseDef = ResolvePhaseDefFor(p);

            OnPhaseEnterHook(p, phaseDef);
            PhaseEntered?.Invoke(p, phaseDef);

            if (debugLevel == DebugLevel.Off || !debugActionEvents) return;
            if (controller != null)
                Debug.Log($"[Exec:{PrettyName()}] → Enter {p} (#{controller.ActionInstanceId})", this);
        }

        protected virtual void HandlePhaseExit(ActionPhase p)
        {
            var phaseDef = ResolvePhaseDefFor(p);

            OnPhaseExitHook(p, phaseDef);
            PhaseExited?.Invoke(p, phaseDef);

            if (debugLevel == DebugLevel.Off || !debugActionEvents) return;
            if (controller != null)
                Debug.Log($"[Exec:{PrettyName()}] ← Exit {p} (#{controller.ActionInstanceId})", this);
        }

        protected virtual void HandleFinished()
        {
            var id = controller != null ? controller.ActionInstanceId : 0u;
            if (id != 0)
            {
                foreach (var k in _hitsByActionPhase.Keys.Where(k => k.actionId == id).ToArray())
                    _hitsByActionPhase.Remove(k);

                foreach (var rk in _lastHitMs.Keys.Where(rk => rk.key.actionId == id).ToArray())
                    _lastHitMs.Remove(rk);
            }

            OnActionFinishedHook(id);
            ActionFinished?.Invoke(id);

            if (debugLevel != DebugLevel.Off && debugActionEvents && controller != null)
                Debug.Log($"[Exec:{PrettyName()}] ✓ Finished (#{controller.ActionInstanceId})", this);
        }
    }
}
