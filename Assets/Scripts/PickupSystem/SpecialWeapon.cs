// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Special Weapon - a rare drop that swaps the player's gun for a stronger one.
    ///
    /// It replaces the weapon rather than adding to it: the player carries one gun in this
    /// game, so picking this up is a trade and not a collection. That makes it the only
    /// pickup a player can regret taking.
    /// </summary>
    public class SpecialWeapon : HighValuePickup
    {
        // Which weapon this hands over, named by CombatSystem's own weapon id. Storing an
        // id rather than a Weapon object keeps the whole pickup folder free of any
        // dependency on the combat classes, which belong to another team member.
        [Tooltip("Id of the weapon handed to the player, as known by CombatSystem.")]
        [SerializeField]
        private int m_WeaponId = 0;

        /// <summary>
        /// Gives the weapon to the player who walked into it, replacing their current one.
        /// </summary>
        public override void OnCollected(IPickupTarget player)
        {
            // A player wired up without a CombatSystem is a broken prefab. Report it and
            // give up on this one pickup rather than throwing in the middle of a match.
            if (player.Weapons == null)
            {
                Debug.LogError($"{name}: the player has no CombatSystem, so this weapon did nothing.", this);
                return;
            }

            // Hand the weapon over. Whether the old gun is dropped, destroyed or kept in
            // reserve is CombatSystem's decision - the pickup only says which weapon.
            player.Weapons.EquipWeapon(m_WeaponId);
        }
    }
}
