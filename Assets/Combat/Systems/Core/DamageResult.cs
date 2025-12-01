// DamageResult.cs
// Purpose: Final resolved damage numbers + effect payloads.

using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Marker for any resolved effect that the pipeline can route to receivers.
    /// </summary>
    public interface IResolvedEffect { }

    /// <summary>Resolved stun effect.</summary>
    public struct ResolvedStun : IResolvedEffect
    {
        public int durationMs;
    }

    /// <summary>Resolved hitstop effect.</summary>
    public struct ResolvedHitstop : IResolvedEffect
    {
        public int frames;
    }

    /// <summary>Resolved knockback effect.</summary>
    public struct ResolvedKnockback : IResolvedEffect
    {
        public Vector3 dir;
        public float mag;
    }

    /// <summary>
    /// Final outcome of resolving an ActionPayload.
    /// </summary>
    public struct DamageResult
    {
        public float finalDamage;
        public List<IResolvedEffect> effects;
        public string[] tags;
    }
}
