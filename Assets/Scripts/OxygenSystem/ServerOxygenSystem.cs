using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Oxygen
{
    /// <summary>
    /// Runs the air down, and hurts whoever runs out.
    ///
    /// Server only. The client never writes this value, it only receives it - see the note
    /// on PlayerOxygen for why the server has to be the one holding it.
    ///
    /// This sits in the ordinary simulation group rather than the predicted one, because
    /// oxygen is not predicted: there is no per-tick input behind it and no rollback to
    /// survive, so a plain once-per-frame update on the server is enough.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerOxygenSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Nothing to do until a client is actually playing - this matches the guard
            // ServerPlayerMovementSystem uses, and stops the air draining in the lobby.
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (oxygen, playerGhost) in
                     SystemAPI.Query<RefRW<PlayerOxygen>, RefRW<PredictedPlayerGhost>>())
            {
                // A dead player is not breathing, and must not keep taking suffocation damage
                // while waiting to respawn. ServerGameSystem.HandlePlayerDeathAndRespawn
                // already watches for CurrentHealth reaching zero, whatever caused it.
                if (playerGhost.ValueRO.CurrentHealth <= 0f)
                {
                    continue;
                }

                // Standing still to recharge, so the air goes up instead of down. Left
                // deliberately uncapped - MaxSeconds is only the full value of the HUD bar,
                // and banking more air than the suit started with is the point of stopping.
                if (playerGhost.ValueRO.ControllerState.IsRecharging)
                {
                    oxygen.ValueRW.Seconds += oxygen.ValueRO.RechargePerSecond * deltaTime;
                    continue;
                }

                if (oxygen.ValueRO.Seconds > 0f)
                {
                    // Clamped at zero rather than allowed to go negative, so the HUD bar and
                    // the "is the player suffocating" test both stay honest.
                    oxygen.ValueRW.Seconds = math.max(0f,
                        oxygen.ValueRO.Seconds - oxygen.ValueRO.DrainPerSecond * deltaTime);
                }
                else
                {
                    // Out of air: health starts draining instead. Clamped at zero for the same
                    // reason, and because the death check reads it as "less than or equal".
                    playerGhost.ValueRW.CurrentHealth = math.max(0f,
                        playerGhost.ValueRO.CurrentHealth -
                        oxygen.ValueRO.SuffocationDamagePerSecond * deltaTime);
                }
            }
        }
    }
}
