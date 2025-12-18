using UnityEngine;

namespace Animation
{
    public sealed class FacingProvider : MonoBehaviour
    {
        // MVP: something else (mover later) can set this.
        [SerializeField] private bool facingRight = true;
        public bool FacingRight => facingRight;

        public void SetFacingRight(bool value) => facingRight = value;
    }
}
