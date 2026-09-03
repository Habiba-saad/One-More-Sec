// The slot stores a Pickup, which lives in the pickup system.
using Unity.MP_FPS.Pickups;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Inventory
{
    /// <summary>
    /// One line in the player's inventory: which item, and how many of it.
    ///
    /// It is plain C# and not a MonoBehaviour, because a slot is not a thing in the world
    /// - it is a row in a list that lives inside the player. Only the pickup lying on the
    /// floor needs a GameObject.
    /// </summary>
    public class InventorySlot
    {
        // The item held in this slot, or null once the slot has been emptied.
        private Pickup m_Item;

        // How many of that item are stacked here.
        private int m_Quantity;

        // Read-only access to the item for the inventory UI.
        public Pickup Item => m_Item;

        // Read-only access to the count for the inventory UI.
        public int Quantity => m_Quantity;

        /// <summary>
        /// A slot always starts life holding something - an empty slot would just be a
        /// row of nothing taking up space in the list.
        /// </summary>
        public InventorySlot(Pickup item, int quantity)
        {
            // Remember what is being held.
            m_Item = item;

            // Remember how many.
            m_Quantity = quantity;
        }

        /// <summary>
        /// True when there is nothing left in this slot.
        /// </summary>
        public bool IsEmpty()
        {
            // A slot counts as empty either because the item is gone or because the last
            // one of it was used up. Checking both means a half-cleared slot can never be
            // mistaken for a full one.
            return m_Item == null || m_Quantity <= 0;
        }

        /// <summary>
        /// Stacks more of the same item into this slot.
        /// </summary>
        public void Add(int amount)
        {
            // Grow the stack.
            m_Quantity += amount;
        }

        /// <summary>
        /// Takes some of the item out of this slot.
        /// </summary>
        public void Remove(int amount)
        {
            // Shrink the stack.
            m_Quantity -= amount;

            // Never let the count go below zero, so a double removal cannot leave the slot
            // holding a negative number of med kits.
            if (m_Quantity < 0)
            {
                m_Quantity = 0;
            }
        }
    }
}
