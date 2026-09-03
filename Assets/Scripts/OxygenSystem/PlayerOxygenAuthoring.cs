// Authoring lives on the player prefab, so this file needs the Unity namespace.
using Unity.Entities;
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Oxygen
{
    /// <summary>
    /// The oxygen numbers, exposed on the player prefab so they can be retuned from the
    /// Inspector without a code change - the same approach
    /// PredictedPlayerControllerConstsAuthoring takes for the movement speeds.
    ///
    /// Add this to ArmaturePlayer_Rifle and ArmaturePlayer_Shotgun, next to
    /// PredictedPlayerControllerConstsAuthoring.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerOxygenAuthoring : MonoBehaviour
    {
        [field: Header("Player Oxygen")]
        [field: SerializeField, Tooltip("Seconds of air the player spawns with, and the full value of the HUD bar")]
        public float StartingSeconds { get; private set; } = 60f;

        [field: SerializeField, Tooltip("Seconds of air used per real second. 1 means the tank empties in real time")]
        public float DrainPerSecond { get; private set; } = 1f;

        [field: SerializeField, Tooltip("Seconds of air won back per real second while standing still and recharging. At 2, one second of standing still buys two seconds of air")]
        public float RechargePerSecond { get; private set; } = 2f;

        [field: SerializeField, Tooltip("Health lost per second once the air has run out. At 10, a player on full health has 10 seconds to find a tank before dying")]
        public float SuffocationDamagePerSecond { get; private set; } = 10f;
    }

    public class PlayerOxygenBaker : Baker<PlayerOxygenAuthoring>
    {
        public override void Bake(PlayerOxygenAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Seconds starts at the authored maximum, so a freshly spawned player has a full
            // tank. Respawning re-instantiates the prefab, which means this also resets the
            // air on respawn without anything having to remember to do it.
            AddComponent(entity, new PlayerOxygen
            {
                Seconds = authoring.StartingSeconds,
                MaxSeconds = authoring.StartingSeconds,
                DrainPerSecond = authoring.DrainPerSecond,
                RechargePerSecond = authoring.RechargePerSecond,
                SuffocationDamagePerSecond = authoring.SuffocationDamagePerSecond,
            });
        }
    }
}
