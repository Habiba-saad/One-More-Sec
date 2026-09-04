using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

using Unity.MP_FPS;

using static FirstPersonController;

public struct PredictedPlayerGhostState : IPredictedState
{
    public uint Tick { get; set; }

    public ControllerState PredictedControllerState;
    public float3 PredictedAccumulatedMovement;
}

public struct PredictedPlayerControllerConsts : IComponentData
{
    public ControllerConsts ControllerConsts;
}

public struct PredictedClientInput : IComponentData
{
    [GhostField]
    public bool SeenNewSnapshot;

    [GhostField]
    public uint LastProcessedServerTick;

    public int BeginInputIndex;
    public int InputCount;
}

public struct PredictedPlayerGhost : IComponentData
{
    public float2 LocalLookYawPitchDegrees;
    public float3 AccumulatedMovement;
    public float3 AppliedError;
    public float ErrorTimeout;
    public float RotationError;
    public float RotationErrorTimeout;
    public bool RequestApplyMovement;

    public float DisabledPredictionLerpFactor;

    public uint ClientPredictionEnabledTick;

    [GhostField] public bool ServerDisabledPrediction;

    [GhostField] public int InputIndex;

    // Everything currently multiplying this player's move speed, already combined into
    // one number by PlayerUpgradeBridge. Replicated because movement is predicted: the
    // client works out its own position from this too, so a value only the server knew
    // would have the two disagreeing every tick a boost was running.
    [GhostField] public float SpeedMultiplier;

    // The same idea for the damage this player deals: everything DamageBoost has
    // registered, already combined by PlayerUpgradeBridge. Only the server reads it when
    // a shot lands, but it is replicated anyway so the owning client's HUD can show that
    // a boost is running - and it costs almost nothing, because a value that does not
    // change between two snapshots is not resent.
    [GhostField] public float DamageMultiplier;

    [GhostField] public ControllerState ControllerState;
    [GhostField] public float CurrentHealth;
    [GhostField] public float MaxHealth;
    
    [GhostField] public uint EquippedWeaponID;
    [GhostField] public float WeaponCooldown;   // Timer to control rate of fire
    
    [GhostField] public int CurrentAmmo;
    [GhostField] public float LastDamageAmount;
    [GhostField] public uint LastHitTick;
    [GhostField] public uint LastShotTick;
    [GhostField] public uint LastJumpTick;
    [GhostField] public uint LastLandTick;
    [GhostField] public uint LastReloadTick;
    [GhostField] public uint LastGrenadeShotTick;
    [GhostField] public float ReloadTimer;
}
