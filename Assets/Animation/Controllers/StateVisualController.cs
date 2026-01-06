using UnityEngine;
using State;

namespace Animation
{
    /// <summary>
    /// StateVisualController
    ///
    /// Observes ONE BaseState and plays a transition animation whenever the state changes.
    /// Toggle mode: alternates between Transition A and Transition B on each change.
    ///
    /// Transition visuals are chosen via DirectionalVisualSet, so props can vary animations by facing
    /// (supports One/Two/Four/Eight via the visual set implementation).
    ///
    /// 2D Isometric Support (XY):
    /// - Facing is resolved on the XY plane (Transform.up is a good default for iso "forward").
    ///
    /// Editor Preview:
    /// - Updates when inspector fields change (OnValidate).
    /// - Updates when you rotate/move the object (or facingTransform) while selected (OnDrawGizmosSelected).
    /// - All editor preview logic is consolidated into a single editor-only block.
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
            [Tooltip("Directional visuals for this transition (One/Two/Four/Eight).")]
            public DirectionalVisualSet visuals;

            [Tooltip("If true, plays the resolved clip in reverse (last frame to first).")]
            public bool playInReverse;

            [Tooltip("If true, loops the clip. If false, plays once and stops on the terminal frame.")]
            public bool loop;
        }

        public enum NextTransition
        {
            A = 0,
            B = 1
        }

        private enum FacingSource
        {
            [Tooltip("Use a constant facing vector in XY.")]
            FixedVectorXY = 0,

            [Tooltip("Use this Transform's right vector in XY.")]
            TransformRightXY = 1,

            [Tooltip("Use this Transform's up vector in XY (recommended for 2D isometric facing).")]
            TransformUpXY = 2
        }

        // -------------------------
        // Inspector
        // -------------------------
        [Header("Mode")]
        [SerializeField] private Mode mode = Mode.Toggle;

        [Header("Wiring")]
        [SerializeField] private BaseState observedState;
        [SerializeField] private SpriteAnimationPlayer player;

        [Header("Facing (2D Isometric / XY)")]
        [SerializeField] private FacingSource facingSource = FacingSource.TransformUpXY;

        [Tooltip("Used when Facing Source = FixedVectorXY.")]
        [SerializeField] private Vector2 fixedFacing = Vector2.right;

        [Tooltip("Optional. If unset, uses this GameObject's transform.")]
        [SerializeField] private Transform facingTransform;

        [Header("Toggle Transitions")]
        [SerializeField] private Transition transitionA = new Transition();
        [SerializeField] private Transition transitionB = new Transition();

        [Header("Toggle Sync")]
        [SerializeField] private NextTransition next = NextTransition.A;

        [Header("Debug (Read Only)")]
        [SerializeField] private string debugLastPlayed;
        [SerializeField] private bool debugLastReverse;
        [SerializeField] private bool debugLastLoop;
        [SerializeField] private bool debugLastMirrorX;
        [SerializeField] private bool debugLastMirrorY;

        // -------------------------
        // Unity lifecycle
        // -------------------------
        private void Reset()
        {
            observedState = GetComponent<BaseState>();
            player = GetComponentInChildren<SpriteAnimationPlayer>();
            facingTransform = transform;
        }

        private void Awake()
        {
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
            if (!facingTransform) facingTransform = transform;

            debugLastPlayed = "";
            debugLastReverse = false;
            debugLastLoop = false;
            debugLastMirrorX = false;
            debugLastMirrorY = false;
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

        // -------------------------
        // Runtime: state change -> play
        // -------------------------
        private void HandleStateChanged(BaseState _)
        {
            if (mode != Mode.Toggle)
                return;

            if (!player)
                return;

            Transition t = (next == NextTransition.A) ? transitionA : transitionB;
            if (t == null || t.visuals == null)
                return;

            PlayTransition(t);

            // Flip for next change.
            next = (next == NextTransition.A) ? NextTransition.B : NextTransition.A;
        }

        // -------------------------
        // Facing + Resolve
        // -------------------------
        private Vector2 ResolveFacingXY()
        {
            Vector2 f;

            switch (facingSource)
            {
                case FacingSource.TransformRightXY:
                    f = facingTransform ? (Vector2)facingTransform.right : Vector2.right;
                    break;

                case FacingSource.TransformUpXY:
                    f = facingTransform ? (Vector2)facingTransform.up : Vector2.up;
                    break;

                default:
                    f = fixedFacing;
                    break;
            }

            if (f.sqrMagnitude < 0.0001f)
                f = Vector2.right;

            return f.normalized;
        }

        private bool TryResolveVisual(Transition t, out DirectionalVisualSet.ResolvedVisual visual)
        {
            visual = default;

            if (t == null || t.visuals == null)
                return false;

            Vector2 facing = ResolveFacingXY();
            return t.visuals.TryGetVisualForDirection(facing, out visual) && visual.clip != null;
        }

        // -------------------------
        // Playback
        // -------------------------
        private void PlayTransition(Transition t)
        {
            if (!player)
                return;

            if (!TryResolveVisual(t, out var vis))
                return;

            player.SetMirror(vis.mirrorX, vis.mirrorY);

            var frames = vis.clip.GetResolvedFrames();
            float fps = vis.clip.GetResolvedFps();
            if (frames == null || frames.Length == 0 || fps <= 0f)
                return;

            var clipData = new SpriteAnimationPlayer.ClipData(
                id: vis.clip.GetStableId(),
                frames: frames,
                fps: fps,
                loop: t.loop,
                restartOnEnter: true, // MVP rule: always restart on state change
                playInReverse: t.playInReverse
            );

            player.Play(clipData);

            debugLastPlayed = vis.clip.name;
            debugLastReverse = t.playInReverse;
            debugLastLoop = t.loop;
            debugLastMirrorX = vis.mirrorX;
            debugLastMirrorY = vis.mirrorY;
        }

        // -------------------------
        // Editor Preview (consolidated)
        // -------------------------
#if UNITY_EDITOR
        [Header("Editor Preview")]
        [Tooltip("If true, updates the SpriteRenderer pose in edit mode when values change / when selected.")]
        [SerializeField] private bool previewInEditor = true;

        private void OnValidate()
        {
            EditorPreview_TryApply();
        }

        //private void OnDrawGizmosSelected()
        //{
        //    EditorPreview_TryApply();
        //}

        private void EditorPreview_TryApply()
        {
            if (!previewInEditor)
                return;

            if (Application.isPlaying)
                return;

            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
            if (!observedState) observedState = GetComponent<BaseState>();
            if (!facingTransform) facingTransform = transform;

            if (!player)
                return;

            Transition t = (next == NextTransition.A) ? transitionA : transitionB;
            if (!TryResolveVisual(t, out var vis))
                return;

            player.SetMirror(vis.mirrorX, vis.mirrorY);

            // Pose to the start of the transition (forward => first frame, reverse => last frame).
            player.TrySetPose(vis.clip, t.playInReverse);
        }
#endif
    }
}
