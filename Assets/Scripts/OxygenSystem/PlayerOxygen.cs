// The oxygen value has to reach every client, so this file needs the netcode namespace.
using Unity.Entities;
using Unity.NetCode;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Oxygen
{
    /// <summary>
    /// How much air a player has left, and the rules for running out.
    ///
    /// This is a component of its own rather than three more fields on PredictedPlayerGhost.
    /// Oxygen is a system in its own right - it is the currency the suit upgrades are bought
    /// with and the clock the whole match runs on - so it has its own reason to change and
    /// belongs in its own component. It also gives IOxygenBank and IOxygenRefill one clear
    /// place to read from and write to.
    ///
    /// The server owns this value. It is not predicted on the client: unlike movement there
    /// is no per-tick input driving it and nothing to roll back, so the client simply
    /// receives it and displays it. That is also why it has to be server owned at all -
    /// oxygen is spent to buy upgrades, so a client that owned the number could buy anything
    /// it liked for free.
    /// </summary>
    public struct PlayerOxygen : IComponentData
    {
        // Seconds of air left. Replicated, because other players and the HUD both need it.
        [GhostField]
        public float Seconds;

        // What the player spawns with, kept so the HUD can draw a bar rather than a number.
        [GhostField]
        public float MaxSeconds;

        // Not replicated: these are baked onto the prefab, so the server and every client
        // already hold identical copies and sending them every snapshot would be waste.

        // How many seconds of air are used per real second.
        public float DrainPerSecond;

        // How many seconds of air are won back per real second while recharging. Not
        // replicated for the same reason as DrainPerSecond: it is baked onto the prefab,
        // so the server and every client already hold the same copy.
        public float RechargePerSecond;

        // Health lost per second once the air has run out.
        public float SuffocationDamagePerSecond;

        /// <summary>
        /// True once the air has run out and the player has started taking damage.
        /// </summary>
        public bool IsSuffocating => Seconds <= 0f;
    }
}
