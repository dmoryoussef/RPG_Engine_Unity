using UnityEngine;
using Combat;

namespace Animation
{
    [DisallowMultipleComponent]
    public sealed class CombatActionVisualController : MonoBehaviour
    {
        [SerializeField] private ActionTimelineController actionTimeline;
        [SerializeField] private FacingProvider facing;
        [SerializeField] private SpriteAnimationPlayer player;

        private ActionPhase _currentPhase;

        private void Reset()
        {
            actionTimeline = GetComponent<ActionTimelineController>();
            facing = GetComponent<FacingProvider>();
            player = GetComponentInChildren<SpriteAnimationPlayer>();
        }

        private void Awake()
        {
            if (!actionTimeline) actionTimeline = GetComponent<ActionTimelineController>();
            if (!facing) facing = GetComponent<FacingProvider>();
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
        }

        private void OnEnable()
        {
            if (!actionTimeline) return;
            actionTimeline.OnPhaseEnter += HandlePhaseEnter;
            actionTimeline.OnFinished += HandleFinished;
        }

        private void OnDisable()
        {
            if (!actionTimeline) return;
            actionTimeline.OnPhaseEnter -= HandlePhaseEnter;
            actionTimeline.OnFinished -= HandleFinished;
        }

        private void HandlePhaseEnter(ActionPhase phase)
        {
            _currentPhase = phase;
            TryPlayCurrent();
        }

        private void HandleFinished()
        {
            // MVP: do nothing (locomotion will take over later).
        }

        private void TryPlayCurrent()
        {
            if (!player || !actionTimeline) return;

            MoveDef move = actionTimeline.CurrentActionDef;
            if (!move || move.phases == null) return;

            // Find the phase entry for the current phaseId
            MoveDef.Phase phaseEntry = null;
            for (int i = 0; i < move.phases.Count; i++)
            {
                if (move.phases[i].phaseId == _currentPhase)
                {
                    phaseEntry = move.phases[i];
                    break;
                }
            }
            if (phaseEntry == null) return;

            bool facingRight = facing ? facing.FacingRight : true;

            if (phaseEntry.visuals != null &&
                phaseEntry.visuals.TryResolve(facingRight, out var clip, out var mirror) &&
                clip != null)
            {
                player.SetMirror(mirror);
                player.Play(clip); // assumes your player has Play(AnimationClipDef)
            }
            // else: hold-last frame (no popping)
        }
    }
}
