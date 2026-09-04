using Unity.Entities;
using Unity.NetCode;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Minimap
{
    /// <summary>
    /// One opponent who is currently visible on this player's minimap.
    ///
    /// The buffer sits on the player who is <em>looking</em>, not on the player being
    /// looked at, and it is sent to that player alone. Being revealed is not a property of
    /// a person - the same opponent can be a blip on one minimap and invisible on every
    /// other one at the same moment - so it belongs to the viewer.
    ///
    /// That also keeps the secret where it should be. If it were stored on the player
    /// being revealed and replicated to everybody, every client in the match would receive
    /// the fact that somebody had been found, and a modified client could draw the blip
    /// whether it had paid for a scan or not.
    ///
    /// Only the id travels. The position does not: every player is already a replicated
    /// ghost, so the client works out where the blip goes from the ghost it is holding.
    /// </summary>
    [InternalBufferCapacity(4)]
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct RevealedPlayer : IBufferElementData
    {
        // The network id of the opponent to draw. The same id the leaderboard and the kill
        // feed use, so a blip can be tied back to a name later without a second lookup.
        [GhostField] public int NetworkId;
    }
}
