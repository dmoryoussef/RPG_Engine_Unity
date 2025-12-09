// DialogueGraphAsset.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// Unity-authorable data for a dialogue graph. One asset per conversation.
    /// </summary>
    [CreateAssetMenu(menuName = "Dialogue/Dialogue Graph", fileName = "NewDialogueGraph")]
    public sealed class DialogueGraphAsset : ScriptableObject
    {
        [Serializable]
        public sealed class ChoiceData
        {
            public string Id;
            [TextArea] public string Text;
            public string NextNodeId;
        }

        [Serializable]
        public sealed class NodeData
        {
            public string Id;
            public string SpeakerId;
            [TextArea] public string Text;
            public string NextNodeId;
            public List<ChoiceData> Choices = new List<ChoiceData>();
        }

        [SerializeField] private string _graphId;
        [SerializeField] private string _startNodeId = "start";
        [SerializeField] private List<NodeData> _nodes = new List<NodeData>();

        public string GraphId => string.IsNullOrEmpty(_graphId) ? name : _graphId;
        public string StartNodeId => _startNodeId;

        /// <summary>
        /// Build the runtime DialogueGraph from this asset.
        /// </summary>
        public DialogueGraph BuildGraph()
        {
            var runtimeNodes = new List<DialogueNode>();

            foreach (var nodeData in _nodes)
            {
                if (nodeData == null || string.IsNullOrEmpty(nodeData.Id))
                    continue;

                var choices = new List<DialogueChoice>();
                if (nodeData.Choices != null)
                {
                    foreach (var choiceData in nodeData.Choices)
                    {
                        if (choiceData == null || string.IsNullOrEmpty(choiceData.Id))
                            continue;

                        choices.Add(new DialogueChoice(
                            choiceData.Id,
                            choiceData.Text,
                            choiceData.NextNodeId));
                    }
                }

                var node = new DialogueNode(
                    nodeData.Id,
                    nodeData.SpeakerId,
                    nodeData.Text,
                    nodeData.NextNodeId,
                    choices);

                runtimeNodes.Add(node);
            }

            return new DialogueGraph(GraphId, _startNodeId, runtimeNodes);
        }
    }
}
