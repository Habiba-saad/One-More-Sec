// Pickups sit on GameObjects in the world, so this file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Abstract middle of the PICKUP HIERARCHY: the rare items that only arrive through
    /// the spaceship supply drop, every 60 to 90 seconds, one item per drop event.
    ///
    /// The class adds no behaviour of its own - it exists so that the rest of the game can
    /// say "this is a rare item" without listing OxygenTank, SpecialWeapon and LargeMedKit
    /// by name. SupplyDrop holds exactly one of these, and a fourth rare item later would
    /// slot in without SupplyDrop changing at all.
    /// </summary>
    public abstract class HighValuePickup : Pickup
    {
        // How rare this item is, for the drop table to weight its choice by. A higher tier
        // is meant to be a better item, so the drop can be tuned to hand out the strong
        // ones less often.
        [Tooltip("How rare this item is. Higher means better and rarer.")]
        [Min(1)]
        [SerializeField]
        private int m_ValueTier = 1;

        // Read-only access to the tier for whoever builds the supply drop table.
        public int ValueTier => m_ValueTier;
    }
}
