// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// How long a match runs for, chosen in the lobby before anybody spawns.
    ///
    /// The names say how many rounds are played at most; how many wins it takes to end the
    /// match is worked out from that by RoundsToWin below rather than written twice.
    /// </summary>
    public enum MatchFormat
    {
        // A single round decides everything. The quick game, and the one to test with.
        OneRound = 0,

        // First to three rounds - so two wins take it.
        FT3 = 1,

        // First to five - so three wins take it.
        FT5 = 2
    }

    /// <summary>
    /// The rules that follow from a format, kept next to the enum instead of inside
    /// MatchManager so that the lobby can show "first to 2" on the button without asking
    /// the match about it.
    /// </summary>
    public static class MatchFormatRules
    {
        /// <summary>
        /// How many round wins end the match.
        ///
        /// A best-of-N is won by taking more than half of N, which is why FT3 needs two
        /// and FT5 needs three - the last round is never played when it cannot change the
        /// result.
        /// </summary>
        public static int RoundsToWin(MatchFormat format)
        {
            switch (format)
            {
                case MatchFormat.FT3:
                    return 2;

                case MatchFormat.FT5:
                    return 3;

                default:
                    // OneRound, and anything a future format forgets to handle: one win
                    // ends it. Erring towards a short match rather than an endless one.
                    return 1;
            }
        }

        /// <summary>
        /// The most rounds this format can run to. Only used for display - the match ends
        /// on wins, not on a round count.
        /// </summary>
        public static int MaxRounds(MatchFormat format)
        {
            switch (format)
            {
                case MatchFormat.FT3:
                    return 3;

                case MatchFormat.FT5:
                    return 5;

                default:
                    return 1;
            }
        }
    }
}
