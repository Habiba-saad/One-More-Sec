// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Oxygen Tank - a rare drop that adds seconds straight to the player's tank.
    ///
    /// In this game oxygen is the survival timer and the currency at the same time, so
    /// this is the most flexible item in the drop: the player can spend it on staying
    /// alive, or on suit upgrades, and that choice is theirs.
    /// </summary>
    public class OxygenTank : HighValuePickup
    {
        // How many seconds this adds to the tank. Serialized so the drop can be retuned on
        // the prefab without a code change.
        [Tooltip("Seconds of oxygen added when collected.")]
        [Min(0f)]
        [SerializeField]
        private float m_OxygenAmount = 60f;

        /// <summary>
        /// Tops up the oxygen of the player who walked into it.
        /// </summary>
        public override void OnCollected(IPickupTarget player)
        {
            // A player wired up without an OxygenSystem is a broken prefab. Report it and
            // give up on this one pickup rather than throwing in the middle of a match.
            if (player.OxygenRefill == null)
            {
                Debug.LogError($"{name}: the player has no OxygenSystem, so this tank did nothing.", this);
                return;
            }

            // Add the seconds. There is no cap on the tank, so this can genuinely push the
            // player past the 120 seconds they started the round with.
            player.OxygenRefill.AddSeconds(m_OxygenAmount);
        }
    }
}
