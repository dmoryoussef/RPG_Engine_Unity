using UnityEngine;
using State;

namespace Animation
{
    /// <summary>
    /// StateVisualController (MVP)
    ///
    /// Observes ONE BaseState and plays a transition animation whenever the state changes.
    /// Toggle mode: alternates between Transition A and Transition B on each change.
    ///
    /// The visualizer does NOT interpret state meaning (no "open/closed" logic).
    /// The author wires clips, reverse flags, and looping flags in the inspector.
    ///
    /// Editor Preview:
    /// - When not playing, OnValidate will pose the SpriteRenderer to the "Next" transition's start frame
    ///   (first frame if forward, last frame if reverse) so you see results immediately in Scene View.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateVisualController : MonoBehaviour
    {
        public enum Mode
        {
            Toggle = 0
        }

        [System.Serializable]
        public sealed class Transition
        {
            [Tooltip("Clip to play for this transition.")]
            public AnimationClipDef clip;

            [Tooltip("If true, plays the clip in reverse (last frame to first).")]
            public bool playInReverse;

            [Tooltip("If true, loops the clip. If false, plays once and should stop on the terminal frame.")]
            public bool loop;
        }

        public enum NextTransition
        {
            A = 0,
            B = 1
        }

        // -------------------------
        // Inspector
        // -------------------------
        [Header("Mode")]
        [SerializeField] private Mode mode = Mode.Toggle;

        [Header("Wiring")]
        [SerializeField] private BaseState observedState;
        [SerializeField] private SpriteAnimationPlayer player;

        [Header("Toggle Transitions")]
        [SerializeField] private Transition transitionA = new Transition();
        [SerializeField] private Transition transitionB = new Transition();

        [Header("Toggle Sync")]
        [SerializeField] private NextTransition next = NextTransition.A;

#if UNITY_EDITOR
        [Header("Editor Preview")]
        [Tooltip("If true, updates the SpriteRenderer pose in edit mode when values change.")]
        [SerializeField] private bool previewInEditor = true;
#endif

        [Header("Debug (Read Only)")]
        [SerializeField] private string debugLastPlayed;
        [SerializeField] private bool debugLastReverse;
        [SerializeField] private bool debugLastLoop;

        // -------------------------
        // Unity lifecycle
        // -------------------------
        private void Reset()
        {
            // Best-effort auto-wiring for author convenience.
            observedState = GetComponent<BaseState>();
            player = GetComponentInChildren<SpriteAnimationPlayer>();
        }

        private void Awake()
        {
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();

            debugLastPlayed = "";
            debugLastReverse = false;
            debugLastLoop = false;
        }

        private void OnEnable()
        {
            if (observedState != null)
                observedState.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (observedState != null)
                observedState.OnStateChanged -= HandleStateChanged;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!previewInEditor)
                return;

            // Runtime should drive visuals.
            if (Application.isPlaying)
                return;

            // Best-effort editor wiring.
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
            if (!observedState) observedState = GetComponent<BaseState>();

            ApplyPreviewPose();
        }
#endif

        // -------------------------
        // Event handling
        // -------------------------
        private void HandleStateChanged(BaseState _)
        {
            if (mode != Mode.Toggle)
                return;

            if (!player)
                return;

            Transition t = (next == NextTransition.A) ? transitionA : transitionB;
            if (t == null || t.clip == null)
                return;

            PlayTransition(t);

            debugLastPlayed = t.clip.name;
            debugLastReverse = t.playInReverse;
            debugLastLoop = t.loop;

            // Flip for next change.
            next = (next == NextTransition.A) ? NextTransition.B : NextTransition.A;
        }

        // -------------------------
        // Playback / Preview
        // -------------------------
        private void PlayTransition(Transition t)
        {
            if (t == null || t.clip == null || !player)
                return;

            var frames = t.clip.GetResolvedFrames();
            float fps = t.clip.GetResolvedFps();
            if (frames == null || frames.Length == 0 || fps <= 0f)
                return;

            int id = t.clip.GetStableId();

            // MVP rule:
            // - Always restart on state change
            // - Loop is controlled per transition
            var clipData = new SpriteAnimationPlayer.ClipData(
                id: id,
                frames: frames,
                fps: fps,
                loop: t.loop,
                restartOnEnter: true,
                playInReverse: t.playInReverse
            );

            player.Play(in clipData);
        }

#if UNITY_EDITOR
        private void ApplyPreviewPose()
        {
            if (!player)
                return;

            Transition t = (next == NextTransition.A) ? transitionA : transitionB;
            if (t == null || t.clip == null)
                return;

            // Pose to the start of the transition (forward => first frame, reverse => last frame).
            player.TrySetPose(t.clip, t.playInReverse);
        }
#endif
    }
}
