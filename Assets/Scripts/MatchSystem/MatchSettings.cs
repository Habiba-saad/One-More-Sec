// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// What the host picked in the lobby, carried from the menu into the match.
    ///
    /// It is static because the two ends of it live in different scenes: the choice is
    /// made in the main menu, and the MatchManager that needs it is not created until the
    /// game scene has loaded and the server is running. Nothing survives that crossing
    /// except statics and disk, and a single enum is not worth a file.
    ///
    /// This only works because the host is also the server, which is how the game is
    /// played today. A dedicated server never runs the menu, so nothing sets this and
    /// HasSelection stays false - which is exactly why that flag exists rather than the
    /// format alone. See the note in MatchManager.
    /// </summary>
    public static class MatchSettings
    {
        /// <summary>
        /// The format the host chose.
        /// </summary>
        public static MatchFormat SelectedFormat { get; private set; } = MatchFormat.OneRound;

        /// <summary>
        /// False until somebody actually chose. Without this a match started outside the
        /// menu would be silently forced to OneRound, overriding whatever the prefab was
        /// set to and making the Inspector field a lie.
        /// </summary>
        public static bool HasSelection { get; private set; }

        /// <summary>
        /// Records the host's choice. Called by the lobby, and by nothing else.
        /// </summary>
        public static void Select(MatchFormat format)
        {
            SelectedFormat = format;
            HasSelection = true;
        }
    }
}
