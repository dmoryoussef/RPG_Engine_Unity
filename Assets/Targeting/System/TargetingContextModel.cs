using System;

namespace Targeting
{
    /// <summary>
    /// Pure data + events for targeting state.
    /// 
    /// Single-writer model: TargetingComponent mutates this; all other systems read
    /// from it and subscribe to its events.
    /// 
    /// Channels:
    /// - Hover: what the aim/mouse is currently over.
    /// - Locked: explicit lock-on target.
    /// - Focus: reserved for special modes (inspection/combat soft lock) in future.
    /// 
    /// CurrentTarget = Focus ?? Locked ?? Hover
    /// </summary>
    public sealed class TargetingContextModel
    {
        public FocusTarget Hover => _hover;
        public FocusTarget Locked => _locked;
        public FocusTarget Focus => _focus;

        private FocusTarget _hover;
        private FocusTarget _locked;
        private FocusTarget _focus;

        public event Action<FocusChange> HoverChanged;
        public event Action<FocusChange> LockedChanged;
        public event Action<FocusChange> FocusChanged;


        public void SetHover(FocusTarget target)
        {
            if (ReferenceEquals(_hover, target))
                return;

            var change = new FocusChange(_hover, target);
            _hover = target;
            HoverChanged?.Invoke(change);
        }

        public void ClearHover()
        {
            if (_hover == null)
                return;

            var change = new FocusChange(_hover, null);
            _hover = null;
            HoverChanged?.Invoke(change);
        }

        public void SetLocked(FocusTarget target)
        {
            if (ReferenceEquals(_locked, target))
                return;

            var change = new FocusChange(_locked, target);
            _locked = target;
            LockedChanged?.Invoke(change);
        }

        public void ClearLocked()
        {
            if (_locked == null)
                return;

            var change = new FocusChange(_locked, null);
            _locked = null;
            LockedChanged?.Invoke(change);
        }

        public void SetFocus(FocusTarget target)
        {
            if (ReferenceEquals(_focus, target))
                return;

            var change = new FocusChange(_focus, target);
            _focus = target;
            FocusChanged?.Invoke(change);
        }

        public void ClearFocus()
        {
            if (_focus == null)
                return;

            var change = new FocusChange(_focus, null);
            _focus = null;
            FocusChanged?.Invoke(change);
        }
    }
}
