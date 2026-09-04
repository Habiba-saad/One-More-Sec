// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// Where the match has got to. Every rule about when players can move, when they are
    /// put back on the field and when the results appear is written against one of these
    /// five states rather than against a pile of booleans that could contradict each
    /// other - "starting and ended at the same time" is not a state that can exist here.
    /// </summary>
    public enum MatchState
    {
        // Nobody has started yet: waiting for enough players to be connected.
        Waiting = 0,

        // A round is about to begin. Everyone has just been put back on the field and a
        // short countdown is running, so that nobody is shot before their screen has
        // finished drawing.
        RoundStarting = 1,

        // The round is being played. The only state the game is really live in.
        RoundInProgress = 2,

        // The round is over and the next one has not started. This is the gap the
        // "next round in..." screen fills.
        RoundEnded = 3,

        // Every round that matters has been played. The results board is up and nothing
        // else happens.
        MatchEnded = 4
    }
}
