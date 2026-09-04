using Unity.Entities;
using Unity.MP_FPS.Minimap;
using Unity.MP_FPS.Upgrades;
using UnityEngine;

[DisallowMultipleComponent]
public class PredictedPlayerGhostAuthoring : MonoBehaviour
{
    public float DisabledPredictionLerpFactor = 10f;
}

public class PredictedPlayerGhostBaker : Baker<PredictedPlayerGhostAuthoring>
{
    public override void Bake(PredictedPlayerGhostAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        // Both multipliers are set here rather than left at the struct default, because
        // that default is 0: a player baked with it would be unable to move at all, and
        // every shot they fired would land for nothing.
        AddComponent(entity, new PredictedPlayerGhost
        {
            DisabledPredictionLerpFactor = authoring.DisabledPredictionLerpFactor,
            SpeedMultiplier = 1f,
            DamageMultiplier = 1f,
        });
        AddBuffer<PredictedPlayerGhostState>(entity);

        // Baked empty, which is the truth for a player who has just spawned: nothing is
        // running. It has to be baked rather than added when the first upgrade is bought,
        // because a component that is not on the prefab is not part of the ghost and would
        // never reach the client at all.
        AddBuffer<ActiveUpgradeStatus>(entity);

        // Baked for the same reason: who this player can see on their map is replicated to
        // them alone, and a buffer that is not on the prefab is not part of the ghost.
        AddBuffer<RevealedPlayer>(entity);

        AddComponent<PlayerInputComponent>(entity);
    }
}
