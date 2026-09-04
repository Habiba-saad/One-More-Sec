using Unity.Entities;
using Unity.NetCode;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// One upgrade that is running on a player right now: which one, and how long it has
    /// left. There is one of these per running upgrade, so an empty buffer means nothing
    /// is switched on.
    ///
    /// It exists because the two halves of the shop live in different worlds.
    /// UpgradeController runs on the server - it is the only thing that knows what was
    /// bought and watches the clock - while the panel the player looks at runs on their
    /// own client. Without this the panel would have no way to know that a boost is
    /// running, and its countdown bar would have nothing to draw.
    ///
    /// A buffer rather than a few fields on PredictedPlayerGhost, because the number of
    /// running upgrades is not fixed: a fourth upgrade, or two of them at once, needs no
    /// change here at all - which is the same reason UpgradeController keeps a list and
    /// never asks what kind of upgrade it is holding.
    ///
    /// Only the duration that is left is sent. The price, the name and the icon are all in
    /// the SuitUpgradeData asset, which the client already has in its own build, and
    /// sending them over the network every tick would be paying for something nobody can
    /// change mid-match.
    /// </summary>
    [InternalBufferCapacity(4)]
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct ActiveUpgradeStatus : IBufferElementData
    {
        // Which upgrade this is, matching SuitUpgradeData.UpgradeId. The id is sent rather
        // than an index into the catalogue, because an index would silently point at the
        // wrong upgrade the day two players are allowed to carry different ones.
        [GhostField] public int UpgradeId;

        // Seconds before it wears off. The panel turns this into a bar height by dividing
        // it by the duration in the asset, so the fraction never has to travel.
        [GhostField] public float RemainingSeconds;
    }
}
