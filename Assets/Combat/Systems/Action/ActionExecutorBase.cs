// ActionExecutorBase.cs
// Purpose: Shared execution logic for combat action executors (melee, AoE, projectiles).
//
// This version includes detailed debug output so we can see each step:
//   - When ExecuteFrame runs and what phase we're in
//   - How many hits CollectHits returns
//   - Which hits pass de-dupe/rate limiting
//   - When payloads are built and sent to the DamagePipeline
//
// NOTE:
//   - IActionTarget is a marker. Effects are handled by receivers on the target GO.

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

        [Header("Debug")]
        [SerializeField] protected DebugLevel debugLevel = DebugLevel.Minimal;

        [Tooltip("If true, logs phase enter/exit and finish events from the controller.")]
        [SerializeField] protected bool debugActionEvents = true;

        [Tooltip("Optional pretty name for logs; defaults to GameObject name.")]
        [SerializeField] protected string logContextName = null;

        // Per-frame per-target de-dupe
        protected readonly HashSet<GameObject> _hitThisFrame = new();

        // Per-action-phase de-dupe + rate limiting
        protected readonly Dictionary<(uint actionId, ActionPhase phase), HashSet<GameObject>> _hitsByActionPhase
            = new();

        protected readonly Dictionary<((uint actionId, ActionPhase phase) key, GameObject target), int> _lastHitMs
            = new();

        // Cached attacker IActionTarget (found in parents)
        private IActionTarget _cachedAttackerTarget;

        public event System.Action<GameObject, Vector3, uint, ActionPhase> OnHitLogged;
        public event System.Action<ActionPhase, MoveDef.Phase> PhaseEntered;
        public event System.Action<ActionPhase, MoveDef.Phase> PhaseExited;
        public event System.Action<ActionPhase, MoveDef.Phase, float> PhaseTicked;
        public event System.Action<uint> ActionFinished;

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
            if (controller)
            {
                controller.OnFinished += HandleFinished;
                controller.OnPhaseEnter += HandlePhaseEnter;
                controller.OnPhaseExit += HandlePhaseExit;
            }
        }

        protected virtual void OnDisable()
        {
            if (controller)
            {
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

        protected virtual void Update()
        {
            // Standalone executors can sample input here when usePipeline == false.
            if (debugLevel == DebugLevel.Verbose)
            {
                ActionResolver.DebugLogging = true;
            }
            else
                ActionResolver.DebugLogging = false;
        }

        protected virtual void LateUpdate()
        {
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

            if (controller != null && phaseId != ActionPhase.Active)
            {
                if (debugLevel == DebugLevel.Verbose)
                    Debug.Log($"[Exec:{PrettyName()}] ExecuteFrame skipped – phase={phaseId} (needs Active).", this);
                return;
            }

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
                var hitsEnum = CollectHits(frame) ??
                               Enumerable.Empty<(GameObject target,
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

                var activePhase = GetActivePhaseOrNull();
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

        protected virtual bool IsLocallyGated() => true;

        protected virtual MoveDef GetAuthoringMoveDef() => null;

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
                impulseDirectionMode = ActionPayload.Intent.ImpulseDirMode.HitNormal,
                authorDirection = transform.forward,
                tags = activePhase != null ? activePhase.damage.tags : null
            };

            return new ActionPayload { facts = facts, intent = intent };
        }

        /// <summary>
        /// Resolve DamageResult and send it + payload + attacker/target into the pipeline.
        /// </summary>
        protected virtual void ApplyHit(IActionTarget target, in ActionPayload payload)
        {
            if (target == null)
                return;

            var attacker = ResolveAttackerTarget();
            if (attacker == null && debugLevel != DebugLevel.Off)
            {
                Debug.LogWarning($"[Exec:{PrettyName()}] ApplyHit: no IActionTarget found for attacker in parent chain.", this);
            }

            var result = DamageResolver.Resolve(in payload);

            if (debugLevel != DebugLevel.Off)
            {
                var attackerGO = (attacker as MonoBehaviour)?.gameObject;
                var targetGO = (target as MonoBehaviour)?.gameObject;
                Debug.Log(
                    $"[Exec:{PrettyName()}] Dispatching to pipeline: attacker={(attackerGO ? attackerGO.name : "<null>")}, target={(targetGO ? targetGO.name : "<null>")}, damage={result.finalDamage}.",
                    this);
            }

            DamagePipeline.Apply(in payload, in result, attacker, target);
            }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        protected IActionTarget ResolveAttackerTarget()
        {
            if (_cachedAttackerTarget != null)
                return _cachedAttackerTarget;

            _cachedAttackerTarget = GetComponentInParent<IActionTarget>();
            return _cachedAttackerTarget;
        }

        protected MoveDef.Phase GetActivePhaseOrNull()
        {
            if (controller != null)
            {
                return controller.GetCurrentPhaseOrNull();
            }

            var authored = GetAuthoringMoveDef();
            if (authored != null && authored.phases != null)
                return authored.phases.Find(p => p.phaseId == ActionPhase.Active);

            return null;
        }

        protected MoveDef.Phase ResolvePhaseDefFor(ActionPhase phaseId)
        {
            if (controller != null && controller.CurrentActionDef != null &&
                controller.CurrentActionDef.phases != null)
            {
                var list = controller.CurrentActionDef.phases;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].phaseId == phaseId)
                        return list[i];
                }
            }

            var authored = GetAuthoringMoveDef();
            if (authored != null && authored.phases != null)
            {
                var list = authored.phases;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].phaseId == phaseId)
                        return list[i];
                }
            }

            return null;
        }

        protected (uint actionId, ActionPhase phase) CurrentKey()
        {
            uint id = controller != null ? controller.ActionInstanceId : 0u;
            var ph = controller != null ? controller.CurrentPhase : ActionPhase.Active;
            return (id, ph);
        }

        protected bool PassesDeDupeAndRateLimit(GameObject target,
                                                int nowMs,
                                                MoveDef.Phase activePhase,
                                                in (uint actionId, ActionPhase phase) key,
                                                HashSet<GameObject> setForPhase)
        {
            bool oneHitPerPhase = activePhase != null && activePhase.oneHitPerPhase;
            if (oneHitPerPhase && setForPhase.Contains(target))
                return false;

            int rateLimitMs = activePhase != null ? activePhase.rateLimitMs : 0;
            if (rateLimitMs > 0)
            {
                var rateKey = (key, target);
                if (_lastHitMs.TryGetValue(rateKey, out var lastMs) && nowMs - lastMs < rateLimitMs)
                    return false;

                _lastHitMs[rateKey] = nowMs;
            }

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
