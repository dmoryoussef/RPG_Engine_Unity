using UnityEngine;

[DisallowMultipleComponent]
public class SpriteAppearance : MonoBehaviour
{
    [Header("Data")]
    public SpriteAppearanceData appearanceData;

    [Header("Selection")]
    [Tooltip("Optional: force a specific tier by key; leave empty to use the highest priority.")]
    public string preferredTierKey;

    [Header("Status (read-only)")]
    [SerializeField] private string _activeTierKey;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        if (appearanceData == null) return;

        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Apply();
    }
#endif

    public void Apply()
    {
        if (appearanceData == null)
        {
            Debug.LogWarning($"{name}: No appearanceData assigned.");
            return;
        }

        var tier = !string.IsNullOrEmpty(preferredTierKey)
            ? appearanceData.FindByKey(preferredTierKey)
            : null;

        if (tier == null)
            tier = appearanceData.GetHighestPriorityTier();

        if (tier == null)
        {
            Debug.LogWarning($"{name}: No valid tier found in appearanceData.");
            return;
        }

        _activeTierKey = tier.key;

        if (tier.animatorController != null)
        {
            if (_animator == null) _animator = gameObject.AddComponent<Animator>();
            _animator.runtimeAnimatorController = tier.animatorController;
        }
        else if (tier.defaultSprite != null)
        {
            if (_spriteRenderer == null) _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = tier.defaultSprite;
        }
        else
        {
            Debug.LogWarning($"{name}: Active tier '{tier.key}' has neither AnimatorController nor default Sprite.");
        }
    }

    [ContextMenu("Apply Now")]
    void ContextApply() => Apply();

    [ContextMenu("Clear Preferred Tier")]
    void ClearPreferredTier()
    {
        preferredTierKey = string.Empty;
        Apply();
    }
}
