// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Med Kit - the ordinary pickup, restoring a small amount of health.
    ///
    /// This is the only pickup PickupManager spawns on its own, every 30 to 45 seconds
    /// around the map. Everything under HighValuePickup reaches players a different way,
    /// through the spaceship supply drop.
    /// </summary>
    public class MedKit : Pickup
    {
        // How much health this restores. Serialized rather than fixed in code so the
        // amount can be retuned on the prefab, and so a weaker or stronger variant can be
        // made without writing a new class.
        [Tooltip("Health points restored when collected.")]
        [Min(0f)]
        [SerializeField]
        private float m_HealAmount = 25f;

        /// <summary>
        /// Heals the player who walked into it.
        /// </summary>
        public override void OnCollected(IPickupTarget player)
        {
            // A player wired up without a HealthSystem is a broken prefab. Say so in the
            // console instead of throwing, because a pickup failing must not be allowed to
            // interrupt a running match.
            if (player.Health == null)
            {
                Debug.LogError($"{name}: the player has no HealthSystem, so this med kit did nothing.", this);
                return;
            }

            // Hand the healing over. How much health the player is allowed to end up with
            // is HealthSystem's decision, not the med kit's.
            player.Health.Heal(m_HealAmount);
        }
    }
}
