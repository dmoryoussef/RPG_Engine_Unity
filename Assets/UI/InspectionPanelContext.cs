using System;
using System.Collections.Generic;
using UnityEngine;
using Inspection;

namespace UI
{
    /// <summary>
    /// Runtime view-model built from IInspectionPanelContributor components.
    ///
    /// Contributors ONLY interact with this type via:
    /// - AddState   (summary/status lines)
    /// - AddAction  (things the player can do from this inspection)
    /// - AddSection (larger blocks of descriptive or structured text)
    ///
    /// UI subpanels are free to interpret and render these however they like.
    /// Contributors DO NOT need to know about concrete UI panels.
    /// </summary>
    public sealed class InspectionPanelContext
    {
        // ----------------- Nested entry types -----------------

        /// <summary>
        /// A short "status" or summary line, e.g. "Locked", "Open", "Wounded".
        /// Multiple states can be added; UI decides how many / which to show.
        /// </summary>
        public readonly struct StateEntry
        {
            public readonly string Label;
            public readonly string Category;
            public readonly Sprite Icon;
            public readonly int Priority;

            public StateEntry(string label, string category, Sprite icon, int priority)
            {
                Label = label;
                Category = category;
                Icon = icon;
                Priority = priority;
            }
        }

        /// <summary>
        /// The specific InspectionPanelRoot building and using this context.
        /// Contributors can optionally cache this if they need to trigger
        /// refreshes later (e.g. in response to state change events).
        /// </summary>
        public InspectionPanelRoot PanelRoot { get; }

        public InspectionPanelContext(InspectionData data, InspectionPanelRoot panelRoot)
        {
            Data = data;
            PanelRoot = panelRoot;
        }

        /// <summary>
        /// An action the player can perform from the inspection view,
        /// e.g. "Open", "Close", "Unlock", "Read".
        /// </summary>
        public readonly struct ActionEntry
        {
            public readonly string Label;
            public readonly Action Callback;
            public readonly bool IsEnabled;
            public readonly string DisabledReason;
            public readonly string Category;
            public readonly Sprite Icon;
            public readonly int Priority;

            public ActionEntry(
                string label,
                Action callback,
                bool isEnabled,
                string disabledReason,
                string category,
                Sprite icon,
                int priority)
            {
                Label = label;
                Callback = callback;
                IsEnabled = isEnabled;
                DisabledReason = disabledReason;
                Category = category;
                Icon = icon;
                Priority = priority;
            }
        }

        /// <summary>
        /// A larger block of information, e.g. a description paragraph,
        /// a list of facts, or a mini "card" for an NPC or item.
        /// </summary>
        public readonly struct SectionEntry
        {
            public readonly string Title;
            public readonly string Body;
            public readonly string Category;
            public readonly Sprite Icon;
            public readonly int Priority;

            public SectionEntry(
                string title,
                string body,
                string category,
                Sprite icon,
                int priority)
            {
                Title = title;
                Body = body;
                Category = category;
                Icon = icon;
                Priority = priority;
            }
        }

        // ----------------- Public collections -----------------

        /// <summary>High-level status lines.</summary>
        public readonly List<StateEntry> States = new List<StateEntry>();

        /// <summary>Actions the player can perform from the inspection panel.</summary>
        public readonly List<ActionEntry> Actions = new List<ActionEntry>();

        /// <summary>Larger descriptive/structured sections.</summary>
        public readonly List<SectionEntry> Sections = new List<SectionEntry>();

        /// <summary>
        /// The underlying inspection data packet that contributors are
        /// annotating. This comes from the core InspectionData flow.
        /// </summary>
        public InspectionData Data { get; }

        public InspectionPanelContext(InspectionData data)
        {
            Data = data;
        }

        // ----------------- Generic contributor APIs -----------------
        // These are the ONLY methods normal contributors should use.
        // They don't need to know anything about layout or UI.

        /// <summary>
        /// Add a simple status / summary line.
        ///
        /// Examples:
        /// - "Locked"
        /// - "Open"
        /// - "Wounded"
        ///
        /// category: an optional grouping key (e.g. "Door", "Health").
        /// icon: optional sprite the UI may show next to the text.
        /// priority: lower numbers appear earlier.
        /// </summary>
        public void AddState(
            string label,
            int priority = 0,
            string category = null,
            Sprite icon = null)
        {
            if (string.IsNullOrEmpty(label))
                return;

            States.Add(new StateEntry(label, category, icon, priority));
        }

        /// <summary>
        /// Add an action the player can perform from the inspection panel.
        ///
        /// label: text on the button or action row.
        /// callback: code to run when the action is triggered.
        /// isEnabled: if false, the UI should treat this as disabled.
        /// disabledReason: optional explanation for disabled actions.
        /// category: optional grouping key (e.g. "Door", "Interaction").
        /// icon: optional sprite the UI may show next to the label.
        /// priority: lower numbers appear earlier.
        /// </summary>
        public void AddAction(
            string label,
            Action callback,
            bool isEnabled = true,
            string disabledReason = null,
            int priority = 0,
            string category = null,
            Sprite icon = null)
        {
            if (string.IsNullOrEmpty(label))
                return;

            Actions.Add(new ActionEntry(
                label: label,
                callback: callback,
                isEnabled: isEnabled,
                disabledReason: disabledReason,
                category: category,
                icon: icon,
                priority: priority));
        }

        /// <summary>
        /// Add a larger block of descriptive or structured information.
        ///
        /// Examples:
        /// - A "Book" section with title + page text.
        /// - A "Status" section with multiple lines of info.
        /// - An "NPC" section with name, role, mood, etc.
        ///
        /// category: optional grouping key ("Book", "NPC", "Facts").
        /// icon: optional sprite (e.g., a book icon, NPC portrait).
        /// priority: lower numbers appear earlier or in more prominent slots.
        /// </summary>
        public void AddSection(
            string title,
            string body,
            int priority = 0,
            string category = null,
            Sprite icon = null)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
                return;

            Sections.Add(new SectionEntry(
                title: title ?? string.Empty,
                body: body ?? string.Empty,
                category: category,
                icon: icon,
                priority: priority));
        }
    }
}
