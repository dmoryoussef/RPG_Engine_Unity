// ActionTargetRoot.cs
// Purpose: Simple MonoBehaviour that marks a GameObject as a combat target node.

using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Attach this to any GameObject that should be a combat target node.
    ///
    /// HurtBoxes will typically find this via GetComponentInParent<IActionTarget>(),
    /// and the pipeline will treat this object as the anchor for receivers/reactors.
    /// </summary>
    public sealed class ActionTargetRoot : MonoBehaviour, IActionTarget
    {
        [Tooltip("Optional debug label for logs/inspectors.")]
        public string debugName;
    }
}
