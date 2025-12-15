using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/Action Sprite Animation Def", fileName = "NewActionSpriteAnimationDef")]
    public sealed class ActionSpriteAnimationDef : ScriptableObject
    {
        [Serializable]
        public sealed class PhaseClip
        {
            public ActionPhase phaseId;

            [Tooltip("Frames in order (eg 4 sprites).")]
            public Sprite[] frames;

            [Tooltip("Frames per second.")]
            public float fps = 12f;

            [Tooltip("Loop while phase is active.")]
            public bool loop = false;

            [Tooltip("Restart from frame 0 each time the phase is entered.")]
            public bool restartOnEnter = true;
        }

        public List<PhaseClip> clips = new();

        public bool TryGet(ActionPhase phaseId, out PhaseClip clip)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                if (c != null && c.phaseId == phaseId)
                {
                    clip = c;
                    return true;
                }
            }
            clip = null;
            return false;
        }
    }
}
