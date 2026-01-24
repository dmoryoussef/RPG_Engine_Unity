using System.Collections.Generic;
using UnityEngine;
using Grass;
using WorldGrid.Unity.Rendering;

[DisallowMultipleComponent]
public sealed class GrassInfluenceSystem : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private ChunkGrassRenderer grassRenderer;

    [Header("Influencers")]
    [SerializeField] private Transform player;
    [SerializeField] private float playerRadius = 1.0f;
    [SerializeField] private float playerStrength = 0.8f;

    [Tooltip("Optional: monsters/NPCs/etc.")]
    [SerializeField] private List<Transform> extraEntities = new();
    [SerializeField] private float entityRadius = 0.8f;
    [SerializeField] private float entityStrength = 0.6f;

    [Header("Behavior")]
    [SerializeField] private int maxInfluencers = 16;
    [SerializeField] private bool velocityBoost = true;
    [SerializeField] private float velocityToStrength = 0.15f;
    [SerializeField] private float minMultiplier = 0.5f;
    [SerializeField] private float maxMultiplier = 2.0f;

    private readonly List<GrassInfluencer> _scratch = new(32);

    private Vector3 _lastPlayerPos;
    private readonly Dictionary<Transform, Vector3> _lastPos = new();

    private void Awake()
    {
        if (grassRenderer == null)
            grassRenderer = GetComponent<ChunkGrassRenderer>();
    }

    private void LateUpdate()
    {
        if (grassRenderer == null) return;

        _scratch.Clear();

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Player first (usually highest priority)
        if (player != null)
        {
            float s = playerStrength * SpeedMultiplier(player, dt, isPlayer: true);
            _scratch.Add(new GrassInfluencer
            {
                position = player.position,
                radius = playerRadius,
                strength = s
            });
        }

        // Extras
        for (int i = 0; i < extraEntities.Count && _scratch.Count < maxInfluencers; i++)
        {
            var t = extraEntities[i];
            if (t == null) continue;

            float s = entityStrength * SpeedMultiplier(t, dt, isPlayer: false);
            _scratch.Add(new GrassInfluencer
            {
                position = t.position,
                radius = entityRadius,
                strength = s
            });
        }

        grassRenderer.SetInfluencers(_scratch);
    }

    private float SpeedMultiplier(Transform t, float dt, bool isPlayer)
    {
        if (!velocityBoost) return 1f;

        Vector3 last;
        if (isPlayer)
        {
            last = _lastPlayerPos;
            _lastPlayerPos = t.position;
        }
        else
        {
            if (!_lastPos.TryGetValue(t, out last))
                last = t.position;
            _lastPos[t] = t.position;
        }

        float speed = (t.position - last).magnitude / dt;
        float mult = 0.5f + speed * velocityToStrength;
        return Mathf.Clamp(mult, minMultiplier, maxMultiplier);
    }
}
