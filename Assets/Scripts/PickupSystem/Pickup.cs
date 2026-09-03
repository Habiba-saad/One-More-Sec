// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Abstract root of the PICKUP HIERARCHY: anything lying on the map that a player can
    /// walk into and collect.
    ///
    /// Unlike the suit upgrades, a pickup is a MonoBehaviour. An upgrade is pure logic
    /// living inside the player, but a pickup is a real object in the arena - it has a
    /// mesh, a position and a trigger collider - so it belongs on a GameObject.
    ///
    /// The class holds what every pickup has - an id and a description - and leaves what
    /// collecting it actually does to the subclass.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Pickup : MonoBehaviour
    {
        // Identifies what kind of pickup this is. The inventory groups items by this id,
        // so two med kits stack instead of taking two slots.
        [Tooltip("Identifies the kind of pickup. Items with the same id stack in the inventory.")]
        [SerializeField]
        private int m_PickupId = 0;

        // Short line of text for the HUD, e.g. "Med Kit +25 HP". Kept as data on the
        // prefab so it can be reworded, or translated, without touching code.
        [Tooltip("Short line shown to the player when this is collected.")]
        [SerializeField]
        private string m_Description = string.Empty;

        // Read-only access to the id for the inventory and the HUD.
        public int PickupId => m_PickupId;

        // Read-only access to the description for the HUD.
        public string Description => m_Description;

        /// <summary>
        /// What this pickup does to the player who collected it.
        /// Every subclass answers this differently, and that difference is the entire
        /// point of the hierarchy: nothing else in the game has to know which kind of
        /// pickup it just handed to a player.
        /// </summary>
        public abstract void OnCollected(IPickupTarget player);

        /// <summary>
        /// Unity calls this when something enters the pickup's trigger collider.
        ///
        /// Not drawn in the class diagram, which shows only OnCollected. It has to exist
        /// somewhere though, because otherwise nothing in the game would ever call
        /// OnCollected - and putting it here means it is written once for the whole
        /// hierarchy instead of five times.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // Look for the player on the object that touched us. GetComponentInParent is
            // used rather than GetComponent because the collider is usually on a child of
            // the player prefab, not on the root where PlayerController sits.
            IPickupTarget player = other.GetComponentInParent<IPickupTarget>();

            // Something that is not a player walked into it - a projectile, a bit of
            // scenery - so there is nobody to give the item to.
            if (player == null)
            {
                return;
            }

            // Hand the item over. Which effect that is, is the subclass's business.
            OnCollected(player);

            // Take it out of the world so it cannot be collected a second time.
            Despawn();
        }

        /// <summary>
        /// Removes the pickup from the world after it has been collected.
        ///
        /// Marked virtual because this is the one place the netcode will need to change:
        /// in a multiplayer match the server has to be the one that despawns the object,
        /// otherwise two clients can both collect the same med kit. Whoever wires the
        /// netcode can override this without touching any of the five pickup classes.
        /// </summary>
        protected virtual void Despawn()
        {
            // Simple local removal, correct for single player and for testing in a scene.
            Destroy(gameObject);
        }
    }
}
