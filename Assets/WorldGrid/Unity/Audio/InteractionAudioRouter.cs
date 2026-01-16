using Unity.Burst.CompilerServices;
using UnityEngine;
using WorldGrid.Unity.Input;

[DisallowMultipleComponent]
public sealed class InteractionAudioRouter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldPointer2D pointer;

    [Header("Clips")]
    [SerializeField] private AudioClip hoverEnter;
    [SerializeField] private AudioClip hoverExit;
    [SerializeField] private AudioClip click;

    [Header("Volume")]
    [Range(0f, 1f)][SerializeField] private float hoverEnterVolume = 0.25f;
    [Range(0f, 1f)][SerializeField] private float hoverExitVolume = 0.20f;
    [Range(0f, 1f)][SerializeField] private float clickVolume = 0.35f;

    [Header("Policy")]
    [SerializeField] private bool playHoverOnlyWhenValid = true;
    [SerializeField] private bool playClickOnlyWhenValid = false;

    [Header("Anti-Spam")]
    [SerializeField] private float minHoverInterval = 0.05f;

    private AudioSource _audio;
    private float _lastHoverTime;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
        }
    }

    private void OnEnable()
    {
        if (pointer == null) return;

        pointer.TileHoverChanged += OnTileHoverChanged;
        pointer.TileHoverEntered += OnHoverEntered;
        pointer.TileHoverExited += OnHoverExited;
        pointer.Clicked += OnClicked;
    }

    private void OnDisable()
    {
        if (pointer == null) return;

        pointer.TileHoverChanged -= OnTileHoverChanged;
        pointer.TileHoverEntered -= OnHoverEntered;
        pointer.TileHoverExited -= OnHoverExited;
        pointer.Clicked -= OnClicked;
    }

    private void OnTileHoverChanged(WorldPointerHit prev, WorldPointerHit cur)
    {
        if (playHoverOnlyWhenValid && !cur.Valid)
            return;

        if (!CanPlayHover())
            return;

        Play(hoverEnter, hoverEnterVolume);
    }

    private void OnHoverEntered(WorldPointerHit hit)
    {
        if (playHoverOnlyWhenValid && !hit.Valid)
            return;

        if (!CanPlayHover())
            return;

        Play(hoverEnter, hoverEnterVolume);
    }

    private void OnHoverExited(WorldPointerHit hit)
    {
        if (!CanPlayHover())
            return;

        Play(hoverExit, hoverExitVolume);
    }

    private void OnClicked(WorldPointerHit hit, int button)
    {
        if (playClickOnlyWhenValid && !hit.Valid)
            return;

        Play(click, clickVolume);
    }

    private bool CanPlayHover()
    {
        if (minHoverInterval <= 0f)
            return true;

        if (Time.unscaledTime - _lastHoverTime < minHoverInterval)
            return false;

        _lastHoverTime = Time.unscaledTime;
        return true;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
            return;

        _audio.PlayOneShot(clip, volume);
    }
}
