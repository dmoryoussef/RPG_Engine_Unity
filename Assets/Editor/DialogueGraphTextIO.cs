using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Dialogue;

/// <summary>
/// Dialogue graph import/export helpers for DialogueGraphAsset.
/// Round-trips a simple text format:
///
/// NODE [start]
///  Speaker: Sheriff
///  Text: Hello there.
/// 
///  Choices:
///    1. Who are you?  →  sheriff_intro
///    2. Leave me alone.  →  rude_exit
///
/// -----------------------------------------------
///
/// NOTE: This version encodes newlines in Text and Choice text as "\n".
/// On import, those "\n" sequences are decoded back to real newlines.
/// </summary>
public static class DialogueGraphTextIO
{
    // =====================================================================
    //  EXPORT (DialogueGraphAsset → TXT)
    // =====================================================================

    [MenuItem("Assets/Dialogue/Export Dialogue Graph to TXT", true)]
    private static bool ValidateExport()
    {
        return Selection.activeObject is DialogueGraphAsset;
    }

    [MenuItem("Assets/Dialogue/Export Dialogue Graph to TXT")]
    private static void ExportSelectedDialogue()
    {
        var graph = Selection.activeObject as DialogueGraphAsset;
        if (!graph)
        {
            Debug.LogError("No DialogueGraphAsset selected.");
            return;
        }

        ExportGraph(graph);
    }

    public static void ExportGraph(DialogueGraphAsset graph)
    {
        if (!graph)
        {
            Debug.LogError("ExportGraph: graph is null.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("===============================================");
        sb.AppendLine($" Dialogue Export: {graph.GraphId}");
        sb.AppendLine("===============================================\n");

        // Use authoring data: NodeData / ChoiceData, via the Nodes property we added.
        foreach (var node in graph.Nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
                continue;

            // Encode newlines as literal "\n" so we can safely single-line them.
            string textEncoded = (node.Text ?? string.Empty).Replace("\n", "\\n");

            sb.AppendLine($"NODE [{node.Id}]");
            sb.AppendLine($" Speaker: {node.SpeakerId}");
            sb.AppendLine($" Text: {textEncoded}");
            sb.AppendLine();

            if (node.Choices != null && node.Choices.Count > 0)
            {
                sb.AppendLine(" Choices:");
                for (int i = 0; i < node.Choices.Count; i++)
                {
                    var choice = node.Choices[i];
                    if (choice == null) continue;

                    string choiceText = (choice.Text ?? string.Empty).Replace("\n", "\\n");
                    sb.AppendLine($"   {i + 1}. {choiceText}  →  {choice.NextNodeId}");
                }
                sb.AppendLine();
            }
            else if (!string.IsNullOrEmpty(node.NextNodeId))
            {
                sb.AppendLine($" Next → {node.NextNodeId}\n");
            }
            else
            {
                sb.AppendLine(" [End Node]\n");
            }

            sb.AppendLine("-----------------------------------------------\n");
        }

        string path = EditorUtility.SaveFilePanel(
            "Export Dialogue Graph",
            Application.dataPath,
            graph.name + "_DialogueExport.txt",
            "txt"
        );

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"Dialogue exported to: {path}");
        }
    }

    // =====================================================================
    //  IMPORT (TXT → DialogueGraphAsset)
    // =====================================================================

    [MenuItem("Assets/Dialogue/Import Dialogue TXT as Graph")]
    private static void ImportDialogueTxtAsGraph()
    {
        string path = EditorUtility.OpenFilePanel(
            "Import Dialogue TXT",
            Application.dataPath,
            "txt"
        );

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            var graph = ImportGraphFromTxt(path);
            if (graph == null)
            {
                EditorUtility.DisplayDialog("Import Failed",
                    "Could not parse dialogue TXT. Check Console for details.", "OK");
                return;
            }

            // Save as asset
            string assetName = Path.GetFileNameWithoutExtension(path);
            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Save DialogueGraphAsset",
                assetName,
                "asset",
                "Choose a location for the new DialogueGraphAsset");

            if (string.IsNullOrEmpty(assetPath))
                return;

            AssetDatabase.CreateAsset(graph, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;

            Debug.Log($"DialogueGraphAsset created from TXT: {assetPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Import failed: {ex}");
            EditorUtility.DisplayDialog("Import Failed",
                "Exception occurred while importing TXT. Check Console for details.", "OK");
        }
    }

    /// <summary>
    /// Parse a TXT file with the ExportGraph format and build a DialogueGraphAsset.
    /// </summary>
    private static DialogueGraphAsset ImportGraphFromTxt(string path)
    {
        var lines = File.ReadAllLines(path);
        var nodes = new List<DialogueGraphAsset.NodeData>();

        string currentId = null;
        string currentSpeaker = null;
        string currentText = null;
        string currentNextNodeId = null;
        var currentChoices = new List<DialogueGraphAsset.ChoiceData>();

        string DecodeText(string encoded)
        {
            return (encoded ?? string.Empty).Replace("\\n", "\n");
        }

        void FlushCurrentNode()
        {
            if (string.IsNullOrEmpty(currentId))
                return;

            var node = new DialogueGraphAsset.NodeData
            {
                Id = currentId,
                SpeakerId = currentSpeaker ?? string.Empty,
                Text = DecodeText(currentText),
                NextNodeId = currentNextNodeId ?? string.Empty,
                Choices = new List<DialogueGraphAsset.ChoiceData>(currentChoices)
            };

            nodes.Add(node);

            currentId = null;
            currentSpeaker = null;
            currentText = null;
            currentNextNodeId = null;
            currentChoices.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Start of node
            if (line.StartsWith("NODE ["))
            {
                // If we were already building a node, flush it first
                FlushCurrentNode();

                int startBracket = line.IndexOf('[');
                int endBracket = line.IndexOf(']');
                if (startBracket >= 0 && endBracket > startBracket)
                {
                    currentId = line.Substring(startBracket + 1, endBracket - startBracket - 1).Trim();
                }
                else
                {
                    Debug.LogWarning($"Could not parse node id from line: {raw}");
                }

                continue;
            }

            if (line.StartsWith("Speaker:"))
            {
                currentSpeaker = line.Substring("Speaker:".Length).Trim();
                continue;
            }

            if (line.StartsWith("Text:"))
            {
                // Text was encoded on a single line with \n sequences for newlines
                currentText = line.Substring("Text:".Length).Trim();
                continue;
            }

            if (line.StartsWith("Next →") || line.StartsWith("Next ->"))
            {
                int arrow = line.IndexOf('→');
                if (arrow < 0)
                    arrow = line.IndexOf("->", System.StringComparison.Ordinal);

                if (arrow >= 0)
                {
                    string after = line.Substring(arrow + 1);
                    currentNextNodeId = after.Replace(">", "").Trim();
                }
                continue;
            }

            if (line.StartsWith("Choices:", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Choice line: "1. Some text  →  nextNodeId"
            if (char.IsDigit(line[0]) && line.Contains(".") && line.Contains("→"))
            {
                int dotIndex = line.IndexOf('.');
                if (dotIndex > 0)
                {
                    string afterNumber = line.Substring(dotIndex + 1).Trim();

                    int arrowIndex = afterNumber.IndexOf('→');
                    if (arrowIndex > 0)
                    {
                        string choiceTextEncoded = afterNumber.Substring(0, arrowIndex).Trim();
                        string nextId = afterNumber.Substring(arrowIndex + 1).Trim();

                        var choice = new DialogueGraphAsset.ChoiceData
                        {
                            Id = $"{currentId}_choice_{currentChoices.Count + 1}",
                            Text = DecodeText(choiceTextEncoded),
                            NextNodeId = nextId
                        };
                        currentChoices.Add(choice);
                    }
                }

                continue;
            }

            // End-node marker
            if (line.StartsWith("[End Node]"))
            {
                continue;
            }

            // Separator between nodes
            if (line.StartsWith("--------------------------------"))
            {
                FlushCurrentNode();
                continue;
            }

            // Other lines (header, etc.) are ignored by this parser.
        }

        // Flush last node if any
        FlushCurrentNode();

        if (nodes.Count == 0)
        {
            Debug.LogError("ImportGraphFromTxt: No nodes parsed from file.");
            return null;
        }

        // Create graph asset and populate NodeData list
        var graph = ScriptableObject.CreateInstance<DialogueGraphAsset>();

        // Use our helper methods we added to DialogueGraphAsset
        graph.SetNodes(nodes);
        graph.SetStartNodeId(nodes[0].Id);

        return graph;
    }
}
