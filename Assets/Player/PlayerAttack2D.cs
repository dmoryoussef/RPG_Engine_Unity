using UnityEngine;
using Player;

public class PlayerAttack2D : MonoBehaviour
{
    [SerializeField] private PlayerMover2D mover;  // drag the same object that has PlayerMover2D
    [SerializeField] private Transform attackRoot; // this is AttackRoot

    public void Update()
    {
        if (mover == null || attackRoot == null)
            return;

        Vector2 facing = mover.Facing;
        if (facing.sqrMagnitude == 0f)
            facing = Vector2.down; // same default as mover

        // Convert Facing to an angle (assuming +X is "right" and we rotate around Z in 2D)
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

        // Rotate only the attack root; sprite child stays unrotated.
        attackRoot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

    }
}
