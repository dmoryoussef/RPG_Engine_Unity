using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WorldGrid.Runtime.Rendering;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Central binder: resolves the world once, and resolves tile library views from a TileLibraryProvider.
    /// Supports multiple tile renderers by resolving a per-channel TileLibraryKey when present.
    /// </summary>
    public sealed class ChunkRenderOrchestrator : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private WorldHost _worldHost;
        [SerializeField] private ChunkViewWindow viewWindow;

        [Header("Tile Libraries")]
        [SerializeField] private TileLibraryProvider tileLibraryProvider;

        [Tooltip("Fallback key if a channel does not expose a TileLibraryKey.")]
        [SerializeField] private TileLibraryKey defaultTileLibraryKey;

        [Header("Channels (MonoBehaviours implementing IRenderChunkChannel)")]
        [SerializeField] private List<MonoBehaviour> channels = new List<MonoBehaviour>();

        [Header("Debug")]
        [SerializeField] private bool logBinding = true;

        private readonly List<IRenderChunkChannel> _bound = new List<IRenderChunkChannel>();
        private SparseChunkWorld _world;

        private void Start()
        {
            if (!ResolveInputs())
            {
                enabled = false;
                return;
            }     
            BindAll();
        }

        private void OnEnable()
        {

        }

        private void Update()
        {
            for (int i = 0; i < _bound.Count; i++)
                _bound[i].Tick();
        }

        private void OnDisable()
        {
            UnbindAll();
        }

        private bool ResolveInputs()
        {
            if (_worldHost == null)
            {
                UnityEngine.Debug.LogError("[Orchestrator] WorldHost missing.", this);
                return false;
            }

            _world = _worldHost.World;
            if (_world == null)
            {
                UnityEngine.Debug.LogError("[Orchestrator] WorldHost.World is null (world not created yet).", _worldHost);
                return false;
            }

            if (viewWindow == null)
            {
                UnityEngine.Debug.LogError("[Orchestrator] ChunkViewWindow missing.", this);
                return false;
            }

            if (tileLibraryProvider == null)
            {
                UnityEngine.Debug.LogError("[Orchestrator] TileLibraryProvider missing.", this);
                return false;
            }

            return true;
        }

        private void BindAll()
        {
            _bound.Clear();

            if (logBinding)
                    UnityEngine.Debug.Log($"[Orchestrator] Binding {channels.Count} channels.", this);

            for (int i = 0; i < channels.Count; i++)
            {
                MonoBehaviour mb = channels[i];
                if (mb == null)
                    continue;

                if (mb is not IRenderChunkChannel ch)
                {
                    UnityEngine.Debug.LogError($"[Orchestrator] '{mb.name}' does not implement IRenderChunkChannel.", mb);
                    continue;
                }

                // Resolve tile library view for this channel.
                TileLibraryKey key = ResolveTileLibraryKey(mb);
                if (!tileLibraryProvider.TryGet(key, out ITileLibraryView tiles, out string error))
                {
                    UnityEngine.Debug.LogError($"[Orchestrator] TileLibraryProvider.TryGet failed for key '{key}': {error}", mb);
                    continue;
                }

                if (tiles == null)
                {
                    UnityEngine.Debug.LogError($"[Orchestrator] TileLibraryProvider returned null tiles view for key '{key}'.", mb);
                    continue;
                }

                if (logBinding)
                    UnityEngine.Debug.Log($"[Orchestrator] Bind -> {mb.name} ({mb.GetType().Name}) key='{key}'", mb);

                ch.Bind(_worldHost, _world, tiles, viewWindow);
                _bound.Add(ch);
            }
        }

        private void UnbindAll()
        {
            for (int i = 0; i < _bound.Count; i++)
                _bound[i].Unbind();

            _bound.Clear();
        }

        private TileLibraryKey ResolveTileLibraryKey(MonoBehaviour channel)
        {
            // Convention-based: if a channel exposes a public property named "TileLibraryKey" of type TileLibraryKey,
            // we use it. Otherwise we fall back to the orchestrator's defaultTileLibraryKey.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var t = channel.GetType();

            var prop = t.GetProperty("TileLibraryKey", flags);
            if (prop != null && prop.PropertyType == typeof(TileLibraryKey) && prop.CanRead)
            {
                object v = prop.GetValue(channel);
                if (v is TileLibraryKey k)
                    return k;
            }

            var field = t.GetField("tileLibraryKey", flags);
            if (field != null && field.FieldType == typeof(TileLibraryKey))
            {
                object v = field.GetValue(channel);
                if (v is TileLibraryKey k)
                    return k;
            }

            return defaultTileLibraryKey;
        }
    }
}
