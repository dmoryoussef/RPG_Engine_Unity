// DamagePipeline.cs
// Purpose: Core combat action pipeline.
//  - Takes payload + result + attacker/target IActionTarget
//  - Builds ActionContext
//  - Routes effects to receivers on the target
//  - Invokes reactors on target and attacker
//
// This version logs each major step so we can see if the pipeline is being hit,
// whether receivers are found, and what effects are applied.

using UnityEngine;

namespace Combat
{
    public static class DamagePipeline
    {
        public static bool DebugLogging = true;

        public static void Apply(in ActionPayload payload,
                                 in DamageResult result,
                                 IActionTarget attacker,
                                 IActionTarget target)
        {
            var attackerGO = (attacker as MonoBehaviour)?.gameObject;
            var targetGO = (target as MonoBehaviour)?.gameObject;

            var ctx = new ActionContext(attackerGO, targetGO, in payload, in result);

            if (DebugLogging)
            {
                Debug.Log(
                    $"[DamagePipeline] Apply called. attacker={(attackerGO ? attackerGO.name : "<null>")}, target={(targetGO ? targetGO.name : "<null>")}, damage={result.finalDamage}",
                    targetGO ? targetGO : attackerGO);
            }

            if (targetGO == null)
                {
                    if (DebugLogging)
                        Debug.LogWarning("[DamagePipeline] No target GameObject – aborting.", attackerGO);
                    return;
                }

                // ─────────────────────────────────────────────────────────────
                // 1) Core sinks: HP + resolved effects
                // ─────────────────────────────────────────────────────────────

                // Health
                var healthReceivers = targetGO.GetComponents<IHealthReceiver>();
                if (DebugLogging)
                {
                    Debug.Log($"[DamagePipeline] Found {healthReceivers.Length} IHealthReceiver on {targetGO.name}.", targetGO);
                }

                if (healthReceivers.Length > 0)
                {
                    float amount = -result.finalDamage; // negative = damage
                    foreach (var hr in healthReceivers)
                        hr.ApplyHealthChange(amount, ctx);
                }

                // Effects
                if (result.effects != null)
                {
                    foreach (var e in result.effects)
                    {
                        switch (e)
                        {
                            case ResolvedStun stun:
                                {
                                    var stunReceivers = targetGO.GetComponents<IStunReceiver>();
                                    if (DebugLogging)
                                        Debug.Log($"[DamagePipeline] ResolvedStun {stun.durationMs}ms, receivers={stunReceivers.Length} on {targetGO.name}.", targetGO);
                                    foreach (var sr in stunReceivers)
                                        sr.ApplyStun(stun.durationMs, ctx);
                                    break;
                                }

                            case ResolvedHitstop hs:
                                {
                                    var hsReceivers = targetGO.GetComponents<IHitstopReceiver>();
                                    if (DebugLogging)
                                        Debug.Log($"[DamagePipeline] ResolvedHitstop {hs.frames}f, receivers={hsReceivers.Length} on {targetGO.name}.", targetGO);
                                    foreach (var r in hsReceivers)
                                        r.ApplyHitstop(hs.frames, ctx);
                                    break;
                                }

                            case ResolvedKnockback kb:
                                {
                                    var kbReceivers = targetGO.GetComponents<IKnockbackReceiver>();
                                    if (DebugLogging)
                                        Debug.Log($"[DamagePipeline] ResolvedKnockback mag={kb.mag}, receivers={kbReceivers.Length} on {targetGO.name}.", targetGO);
                                    foreach (var r in kbReceivers)
                                        r.ApplyKnockback(kb.dir, kb.mag, ctx);
                                    break;
                                }
                        }
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // 2) Target-side reactors
                // ─────────────────────────────────────────────────────────────

                var targetReactors = targetGO.GetComponents<IOnActionTakenReactor>();
                if (DebugLogging)
                {
                    Debug.Log($"[DamagePipeline] Found {targetReactors.Length} IOnActionTakenReactor on {targetGO.name}.", targetGO);
                }

                foreach (var r in targetReactors)
                    r.OnActionTaken(ctx);

                // ─────────────────────────────────────────────────────────────
                // 3) Attacker-side reactors
                // ─────────────────────────────────────────────────────────────

                if (attackerGO != null)
                {
                    var attackerReactors = attackerGO.GetComponents<IOnActionDealtReactor>();
                    if (DebugLogging)
                    {
                        Debug.Log($"[DamagePipeline] Found {attackerReactors.Length} IOnActionDealtReactor on {attackerGO.name}.", attackerGO);
                    }

                    foreach (var r in attackerReactors)
                        r.OnActionDealt(ctx);
                }
            }
        }
    }
