// Needed for ArgumentNullException.
using System;

// Needed for List and IReadOnlyList.
using System.Collections.Generic;

// The inventory stores Pickup objects, which live in the pickup system.
using Unity.MP_FPS.Pickups;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Inventory
{
    /// <summary>
    /// What one player is carrying: a list of slots, each holding a stack of one item.
    ///
    /// It is plain C# and not a MonoBehaviour, for the same reason as UpgradeController:
    /// the class diagram gives PlayerController a filled diamond to this class, which
    /// means PlayerController creates and owns it with new. It is not a separate component
    /// dragged onto the prefab.
    ///
    /// Items are grouped by their pickup id, so two med kits share one slot with a count
    /// of two instead of taking up two rows.
    /// </summary>
    public class InventoryComponent
    {
        // Every stack this player is carrying, one entry per kind of item.
        private readonly List<InventorySlot> m_Slots = new List<InventorySlot>();

        // Read-only view for the inventory UI to draw from, so nothing outside this class
        // can add or drop items behind its back.
        public IReadOnlyList<InventorySlot> Slots => m_Slots;

        /// <summary>
        /// Puts an item into the inventory, stacking it if the player already carries one.
        /// </summary>
        public void AddItem(Pickup item)
        {
            // Adding nothing is a programming mistake, not an empty inventory.
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // Look for a stack of the same kind of item.
            InventorySlot slot = FindSlot(item.PickupId);

            // Already carrying one, so just make the stack bigger.
            if (slot != null)
            {
                slot.Add(1);
                return;
            }

            // First one of its kind, so it needs a new row in the list.
            m_Slots.Add(new InventorySlot(item, 1));
        }

        /// <summary>
        /// Takes one of this item back out of the inventory.
        /// </summary>
        public void RemoveItem(Pickup item)
        {
            // Removing nothing is a programming mistake.
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // Find the stack this item belongs to.
            InventorySlot slot = FindSlot(item.PickupId);

            // The player is not carrying it, so there is nothing to take away.
            if (slot == null)
            {
                return;
            }

            // Take one off the stack.
            slot.Remove(1);

            // The last one has been used, so drop the whole row rather than leaving an
            // empty line the UI would have to know to skip.
            if (slot.IsEmpty())
            {
                m_Slots.Remove(slot);
            }
        }

        /// <summary>
        /// True when the player is carrying at least one of this kind of item.
        /// </summary>
        public bool HasItem(int itemId)
        {
            // Carrying it means there is a non-empty stack of it.
            return FindSlot(itemId) != null;
        }

        /// <summary>
        /// The item of this kind that the player is carrying, or null when they have none.
        /// </summary>
        public Pickup GetItem(int itemId)
        {
            // Find the stack.
            InventorySlot slot = FindSlot(itemId);

            // Hand back the item it holds, or null when there is no such stack. Written
            // this way rather than slot.Item so that a missing stack does not crash.
            return slot == null ? null : slot.Item;
        }

        /// <summary>
        /// The non-empty stack holding this kind of item, or null when there is none.
        /// </summary>
        private InventorySlot FindSlot(int itemId)
        {
            // A plain loop rather than List.Find, because this runs on every pickup and a
            // loop allocates nothing.
            for (int i = 0; i < m_Slots.Count; i++)
            {
                // Skip rows that have been used up but not cleared yet.
                if (m_Slots[i].IsEmpty())
                {
                    continue;
                }

                // Match by pickup id, which is what makes two med kits stack together.
                if (m_Slots[i].Item.PickupId == itemId)
                {
                    return m_Slots[i];
                }
            }

            // The player is not carrying anything of this kind.
            return null;
        }
    }
}
