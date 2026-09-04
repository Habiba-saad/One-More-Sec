using NUnit.Framework.Constraints;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct PlayerInput
{
    public enum InputFlag
    {
        Jump = 1 << 0,
        Shoot = 1 << 1,
        Sprint = 1 << 2,
        Reload = 1 << 3,
        Crouch = 1 << 4,
        RechargeToggle = 1 << 5,
        BuySpeedBoost = 1 << 6,
        BuyDamageBoost = 1 << 7
    }

    public float2 MoveInput;
    public float2 LookYawPitchDegrees;

    public uint InputFlags; // 4 (16)

    public bool Jump => (InputFlags & (uint)InputFlag.Jump) != 0;
    public bool Shoot => (InputFlags & (uint)InputFlag.Shoot) != 0;
    public bool Reload => (InputFlags & (uint)InputFlag.Reload) != 0;

    // Sprint and Crouch describe a continuous state rather than a one-off action,
    // so they stay set for as long as the player holds the key.
    public bool Sprint => (InputFlags & (uint)InputFlag.Sprint) != 0;
    public bool Crouch => (InputFlags & (uint)InputFlag.Crouch) != 0;

    // A single press like Jump rather than a held key: it flips recharging on and off.
    // The latched state itself lives on ControllerState, not here.
    public bool RechargeToggle => (InputFlags & (uint)InputFlag.RechargeToggle) != 0;

    // Temporary stand-in for the upgrade shop, so a purchase can be tested before there
    // is a panel to click. Travels with the rest of the input because the purchase has to
    // be made by the server - it spends oxygen, which only the server owns.
    public bool BuySpeedBoost => (InputFlags & (uint)InputFlag.BuySpeedBoost) != 0;

    // The same stand-in for the damage boost. Two flags rather than one "buy" flag plus
    // an id, because the shop panel that replaces both of them will send a purchase
    // request of its own and neither flag will survive it.
    public bool BuyDamageBoost => (InputFlags & (uint)InputFlag.BuyDamageBoost) != 0;

    public void SetFlag(InputFlag flag, bool set)
    {
        if (set)
        {
            InputFlags |= (uint)flag;
        }
        else
        {
            InputFlags &= ~(uint)flag;
        }
    }

    // Flags are OR-ed together so that a press happening between two ticks is never
    // dropped. For the held flags (Sprint, Crouch) this means a release can linger for
    // a single tick, which is harmless: the server and the predicting client both read
    // the exact same ClientCommandInput, so they stay in agreement either way.
    public void UpdateFrom(in PlayerInput input)
    {
        InputFlags |= input.InputFlags;
    }
}

public struct PlayerInputComponent : IComponentData
{
    public PlayerInput Input;
}

public struct PlayerClientCommandInputLookup : IComponentData
{
    public Entity ClientCommandInputEntity;
}

public struct ClientInput : IComponentData
{
    public PlayerInput PlayerInput;

    public void SetInput(int playerIndex, in PlayerInput playerInput)
    {
        PlayerInput = playerInput;
    }
}

public struct ClientMovementInput : IComponentData
{
    public PlayerInput PlayerInput;

    public void SetInput(int playerIndex, in PlayerInput playerInput)
    {
        PlayerInput = playerInput;
    }

    public void UpdateFrom(in ClientMovementInput clientInput)
    {
        PlayerInput.UpdateFrom(clientInput.PlayerInput);
    }
}

public struct ClientCommandInput : ICommandData
{
    public NetworkTick Tick { get; set; }

    public NetworkTick ClientInterpolationTick;

    public PlayerInput PlayerInput;

    public void SetPlayerMovementInput(int playerIndex, in PlayerInput playerInput)
    {
        PlayerInput = playerInput;
    }

    public void UpdatePlayerInput(int playerIndex, in PlayerInput playerInput)
    {
        PlayerInput.UpdateFrom(playerInput);
    }

    public bool TryGetPlayerMovementInput(int playerIndex, out PlayerInput playerInput)
    {
        playerInput = PlayerInput;
        return true;
    }

    public void UpdateFrom(in ClientMovementInput clientInput)
    {
        PlayerInput.UpdateFrom(clientInput.PlayerInput);
    }

    public void SetFrom(in ClientMovementInput clientInput)
    {
        PlayerInput = clientInput.PlayerInput;
    }
}