using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// TUTORIAL:
    /// This is the data package that the UI (or debug window) will display.
    ///
    /// It is filled by the IInspectable.BuildInspectionData method
    /// and then sent to:
    /// - An in-game UI panel (later)
    /// - A debug editor window (now)
    ///
    /// It is intentionally simple, but designed to grow:
    /// - DisplayName, Short/LongDescription, Icon for basic info
    /// - Actions list for available interactions
    /// </summary>
    [Serializable]
    public class InspectionData
    {
        /// <summary>
        /// The root GameObject being inspected. Useful for selection or debugging.
        /// </summary>
        public GameObject TargetRoot;

        /// <summary> Name shown in the inspection UI. </summary>
        public string DisplayName;

        /// <summary> Short, one-line description or subtitle. </summary>
        public string ShortDescription;

        /// <summary> Longer, multi-line description or lore text. </summary>
        public string LongDescription;

        /// <summary>
        /// High-level state of the object from an interaction perspective.
        /// e.g. "Locked", "Open", "Disabled", "Broken".
        /// </summary>
        public string ObjectStateLabel;

        /// <summary>
        /// How the interaction system would describe the primary interaction.
        /// e.g. "Inspect", "Talk", "Open", "Use".
        /// </summary>
        public string InteractionLabel;


        /// <summary> Optional icon to display with the inspection. </summary>
        public Sprite Icon;

        /// <summary>
        /// Optional list of actions the player can take on this inspected object.
        /// These are built from the Interaction system (IInteractable) later.
        /// For now, you can ignore or leave this empty.
        /// </summary>
        public readonly List<InspectionActionView> Actions = new List<InspectionActionView>();

        /// <summary>
        /// Reset all data fields so this instance can be reused.
        /// </summary>
        public void Clear()
        {
            TargetRoot = null;
            DisplayName = string.Empty;
            ShortDescription = string.Empty;
            LongDescription = string.Empty;
            Icon = null;
            Actions.Clear();
        }
    }

    /// <summary>
    /// TUTORIAL:
    /// A small view-model describing a single action that can appear alongside
    /// inspection data. Example actions:
    /// - "Open"
    /// - "Talk"
    /// - "Pick up"
    ///
    /// For now, you can ignore this in the UI and focus on just the text/icon.
    /// </summary>
    [Serializable]
    public class InspectionActionView
    {
        /// <summary> Internal ID for this action (e.g. "primary", "examine", etc.). </summary>
        public string Id;

        /// <summary> Label used in the UI, e.g. "Open", "Talk", "Pick up". </summary>
        public string Label;

        /// <summary> Whether the action is currently allowed. </summary>
        public bool IsEnabled;

        /// <summary> If disabled, why? (shown as tooltip or message). </summary>
        public string DisabledReason;

        /// <summary>
        /// Back-reference to the underlying interactable object.
        /// This comes from your Interaction system.
        ///
        /// NOTE: This couples Inspection to Interaction intentionally,
        ///       since we want inspection to surface available interactions.
        /// </summary>
       // public Interaction.IInteractable Interactable;
    }
}
