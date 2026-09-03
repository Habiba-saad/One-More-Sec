// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Large Med Kit - the rare version of the med kit, restoring far more health.
    ///
    /// It is a separate class from MedKit rather than a MedKit with a bigger number,
    /// because the two reach the player along different paths: the ordinary med kit is
    /// spawned around the map by PickupManager, while this one only ever comes out of a
    /// spaceship supply drop.
    /// </summary>
    public class LargeMedKit : HighValuePickup
    {
        // How much health this restores. Serialized so it can be retuned on the prefab.
        [Tooltip("Health points restored when collected.")]
        [Min(0f)]
        [SerializeField]
        private float m_HealAmount = 60f;

        /// <summary>
        /// Heals the player who walked into it.
        /// </summary>
        public override void OnCollected(IPickupTarget player)
        {
            // A player wired up without a HealthSystem is a broken prefab. Report it and
            // give up on this one pickup rather than throwing in the middle of a match.
            if (player.Health == null)
            {
                Debug.LogError($"{name}: the player has no HealthSystem, so this med kit did nothing.", this);
                return;
            }

            // Hand the healing over. HealthSystem still caps the player at its own maximum,
            // so a full player collecting this wastes most of it - which is the point of
            // making them decide whether to grab it now or leave it for later.
            player.Health.Heal(m_HealAmount);
        }
    }
}
