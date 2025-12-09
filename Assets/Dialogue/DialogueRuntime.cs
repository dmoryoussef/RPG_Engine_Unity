// DialogueRuntime.cs
using System;
using System.Collections.Generic;

namespace Dialogue
{
    /// <summary>
    /// A single selectable choice on a dialogue node.
    /// </summary>
    public sealed class DialogueChoice
    {
        public string Id { get; }
        public string Text { get; }
        public string NextNodeId { get; }

        public DialogueChoice(string id, string text, string nextNodeId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            NextNodeId = nextNodeId ?? string.Empty;
        }
    }

    /// <summary>
    /// One line of dialogue, optionally with choices and/or a next node.
    /// </summary>
    public sealed class DialogueNode
    {
        public string Id { get; }
        public string SpeakerId { get; }
        public string Text { get; }
        public string NextNodeId { get; }
        public IReadOnlyList<DialogueChoice> Choices { get; }

        public bool HasChoices => Choices.Count > 0;
        public bool IsTerminal => string.IsNullOrEmpty(NextNodeId) && Choices.Count == 0;

        public DialogueNode(
            string id,
            string speakerId,
            string text,
            string nextNodeId,
            IList<DialogueChoice> choices = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SpeakerId = speakerId ?? string.Empty;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            NextNodeId = nextNodeId ?? string.Empty;

            if (choices == null || choices.Count == 0)
            {
                Choices = Array.Empty<DialogueChoice>();
            }
            else
            {
                Choices = new List<DialogueChoice>(choices).AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Immutable graph of nodes, looked up by Id.
    /// </summary>
    public sealed class DialogueGraph
    {
        public string Id { get; }
        public string StartNodeId { get; }

        readonly Dictionary<string, DialogueNode> _nodes;

        public DialogueGraph(string id, string startNodeId, IEnumerable<DialogueNode> nodes)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Graph id must be non-empty.", nameof(id));

            if (string.IsNullOrEmpty(startNodeId))
                throw new ArgumentException("Start node id must be non-empty.", nameof(startNodeId));

            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            Id = id;
            StartNodeId = startNodeId;

            _nodes = new Dictionary<string, DialogueNode>();
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (string.IsNullOrEmpty(node.Id))
                    throw new InvalidOperationException("DialogueNode id cannot be null or empty.");

                _nodes[node.Id] = node;
            }

            if (!_nodes.ContainsKey(StartNodeId))
            {
                throw new InvalidOperationException(
                    $"DialogueGraph '{Id}' does not contain start node '{StartNodeId}'.");
            }
        }

        public DialogueNode GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return null;

            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }
    }

    /// <summary>
    /// Runtime session walking a DialogueGraph.
    /// </summary>
    public sealed class DialogueSession
    {
        public DialogueGraph Graph { get; }
        public DialogueNode CurrentNode { get; private set; }
        public bool IsActive { get; private set; }

        public event Action<DialogueNode> NodeEntered;
        public event Action SessionEnded;

        public DialogueSession(DialogueGraph graph)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public void Start(string startNodeId = null)
        {
            if (IsActive)
                throw new InvalidOperationException("DialogueSession is already active.");

            var nodeId = string.IsNullOrEmpty(startNodeId)
                ? Graph.StartNodeId
                : startNodeId;

            var node = Graph.GetNode(nodeId);
            if (node == null)
            {
                throw new InvalidOperationException(
                    $"DialogueSession could not find start node '{nodeId}' in graph '{Graph.Id}'.");
            }

            IsActive = true;
            SetCurrentNode(node);
        }

        public void Advance()
        {
            if (!IsActive || CurrentNode == null)
                return;

            if (CurrentNode.HasChoices)
            {
                // UI must call ChooseChoice instead.
                return;
            }

            if (string.IsNullOrEmpty(CurrentNode.NextNodeId))
            {
                End();
                return;
            }

            var next = Graph.GetNode(CurrentNode.NextNodeId);
            if (next == null)
            {
                End();
                return;
            }

            SetCurrentNode(next);
        }

        public void ChooseChoice(int index)
        {
            if (!IsActive || CurrentNode == null)
                return;

            if (!CurrentNode.HasChoices)
                throw new InvalidOperationException(
                    "ChooseChoice called but current node has no choices.");

            if (index < 0 || index >= CurrentNode.Choices.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var choice = CurrentNode.Choices[index];

            if (string.IsNullOrEmpty(choice.NextNodeId))
            {
                End();
                return;
            }

            var next = Graph.GetNode(choice.NextNodeId);
            if (next == null)
            {
                End();
                return;
            }

            SetCurrentNode(next);
        }

        public void Cancel()
        {
            if (!IsActive)
                return;

            End();
        }

        void SetCurrentNode(DialogueNode node)
        {
            CurrentNode = node;
            NodeEntered?.Invoke(node);
        }

        void End()
        {
            IsActive = false;
            CurrentNode = null;
            SessionEnded?.Invoke();
        }
    }
}
