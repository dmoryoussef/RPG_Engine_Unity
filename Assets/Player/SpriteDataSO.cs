using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Character/Sprite Appearance Data (Minimal)")]
public class SpriteAppearanceData : ScriptableObject
{
    [Serializable]
    public class TierEntry
    {
        [Tooltip("Unique id; e.g. t0/prototype/final.")]
        public string key;

        [Tooltip("Higher = more preferred when no key is forced.")]
        public int priority = 0;

        [Header("Pick ONE of these (animator takes precedence)")]
        public RuntimeAnimatorController animatorController;
        public Sprite defaultSprite; // used if no animatorController
    }

    [Tooltip("Add as many tiers as you like. Order doesn't matter.")]
    public List<TierEntry> tiers = new List<TierEntry>();

    public TierEntry GetHighestPriorityTier()
    {
        TierEntry best = null;
        foreach (var t in tiers)
        {
            if (t == null) continue;
            if (best == null || t.priority > best.priority)
                best = t;
        }
        return best;
    }

    public TierEntry FindByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var t in tiers)
        {
            if (t == null) continue;
            if (string.Equals(t.key, key, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }
}

