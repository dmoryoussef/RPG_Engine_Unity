// DamageResolver.cs
// Purpose: Pure transform from ActionPayload (intent) to DamageResult (resolved).

using UnityEngine;
using System.Collections.Generic;

namespace Combat
{
    public static class DamageResolver
    {
        public static DamageResult Resolve(in ActionPayload packet)
        {
            var result = new DamageResult
            {
                finalDamage = packet.intent.baseDamage,
                effects = new List<IResolvedEffect>(),
                tags = packet.intent.tags
            };

            // Stun
            if (packet.intent.stunMs > 0)
            {
                result.effects.Add(new ResolvedStun
                {
                    durationMs = packet.intent.stunMs
                });
            }

            // Hitstop
            if (packet.intent.hitstopFrames > 0)
            {
                result.effects.Add(new ResolvedHitstop
                {
                    frames = packet.intent.hitstopFrames
                });
            }

            // Knockback
            if (packet.intent.knockbackPower > 0f)
            {
                Vector3 dir = ResolveKnockbackDirection(in packet);
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    result.effects.Add(new ResolvedKnockback
                    {
                        dir = dir,
                        mag = packet.intent.knockbackPower
                    });
                }
            }

            return result;
        }

        private static Vector3 ResolveKnockbackDirection(in ActionPayload packet)
        {
            var facts = packet.facts;
            var intent = packet.intent;

            switch (intent.impulseDirectionMode)
            {
                case ActionPayload.Intent.ImpulseDirMode.HitNormal:
                    return facts.hitNormal;

                case ActionPayload.Intent.ImpulseDirMode.AuthorDirection:
                    return intent.authorDirection;

                case ActionPayload.Intent.ImpulseDirMode.Tangent:
                    {
                        Vector3 n = facts.hitNormal.sqrMagnitude > 1e-6f
                            ? facts.hitNormal.normalized
                            : Vector3.up;

                        Vector3 t = Vector3.Cross(n, Vector3.up);
                        if (t.sqrMagnitude < 1e-4f)
                            t = Vector3.Cross(n, Vector3.right);

                        return t;
                    }

                case ActionPayload.Intent.ImpulseDirMode.Custom:
                    return intent.authorDirection;

                default:
                    return intent.authorDirection;
            }
        }
    }
}
