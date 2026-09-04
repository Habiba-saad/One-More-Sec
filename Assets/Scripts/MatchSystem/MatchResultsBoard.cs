using System.Collections.Generic;
using Gameplay.Leaderboard;
using UnityEngine;
using UnityEngine.UIElements;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// The table that goes up when the match is over: everybody in the match, best first,
    /// with the rounds they won and the kills they took.
    ///
    /// Ordered by rounds first and kills second, because the match is won by rounds - a
    /// player with fifteen kills and no round wins finished behind one who quietly took
    /// two rounds, and the table has to say so or it is describing a different game.
    ///
    /// It reads and never writes. Every number here is the server's, arriving through the
    /// leaderboard ghost.
    ///
    /// Put it on the same GameObject as RespawnScreen: that GameObject has a UIDocument of
    /// its own which stays visible once a player is off the field, and by the time the
    /// match ends every player is.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class MatchResultsBoard : MonoBehaviour
    {
        /// <summary>
        /// One row, in the order it will be drawn.
        /// </summary>
        private struct Row
        {
            public int NetworkId;
            public string Name;
            public int RoundWins;
            public int Kills;
            public int Deaths;
        }

        [Tooltip("How often the table is rebuilt while it is up, in seconds.")]
        [Min(0.1f)]
        [SerializeField]
        private float m_RefreshSeconds = 0.5f;

        private readonly ClientMatchStateReader m_Match = new ClientMatchStateReader();

        // Reused between refreshes so that sorting the table allocates nothing new.
        private readonly List<Row> m_Rows = new List<Row>();

        private VisualElement m_Board;
        private VisualElement m_RowsContainer;
        private Label m_TitleLabel;
        private Label m_SubtitleLabel;

        private float m_TimeUntilRefresh;

        private static readonly Color k_YouColor = new Color(0.45f, 0.95f, 1f);
        private static readonly Color k_TextColor = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color k_FaintColor = new Color(1f, 1f, 1f, 0.45f);

        private void OnEnable()
        {
            BuildBoard();
        }

        private void OnDisable()
        {
            m_Board?.RemoveFromHierarchy();
            m_Board = null;
            m_RowsContainer = null;
        }

        /// <summary>
        /// Creates the panel once. It stays hidden until the match ends.
        /// </summary>
        private void BuildBoard()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                return;
            }

            m_Board = new VisualElement { name = "match-results-board" };
            m_Board.pickingMode = PickingMode.Ignore;
            m_Board.style.position = Position.Absolute;
            m_Board.style.left = 0f;
            m_Board.style.right = 0f;
            m_Board.style.top = 0f;
            m_Board.style.bottom = 0f;
            m_Board.style.alignItems = Align.Center;
            m_Board.style.justifyContent = Justify.Center;

            // Dark enough to read a table against whatever the camera is looking at, and
            // not so dark that the arena disappears completely behind it.
            m_Board.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
            m_Board.style.display = DisplayStyle.None;

            var panel = new VisualElement { name = "match-results-panel" };
            panel.pickingMode = PickingMode.Ignore;
            panel.style.minWidth = 460f;
            panel.style.paddingLeft = 28f;
            panel.style.paddingRight = 28f;
            panel.style.paddingTop = 22f;
            panel.style.paddingBottom = 22f;
            panel.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.94f);
            SetRadius(panel, 8f);
            SetBorder(panel, new Color(1f, 1f, 1f, 0.18f), 1f);

            m_TitleLabel = new Label("MATCH OVER") { name = "match-results-title" };
            m_TitleLabel.pickingMode = PickingMode.Ignore;
            m_TitleLabel.style.fontSize = 26f;
            m_TitleLabel.style.color = k_TextColor;
            m_TitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(m_TitleLabel);

            m_SubtitleLabel = new Label(string.Empty) { name = "match-results-subtitle" };
            m_SubtitleLabel.pickingMode = PickingMode.Ignore;
            m_SubtitleLabel.style.fontSize = 13f;
            m_SubtitleLabel.style.color = k_FaintColor;
            m_SubtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_SubtitleLabel.style.marginBottom = 16f;
            panel.Add(m_SubtitleLabel);

            panel.Add(BuildHeaderRow());

            m_RowsContainer = new VisualElement { name = "match-results-rows" };
            m_RowsContainer.pickingMode = PickingMode.Ignore;
            panel.Add(m_RowsContainer);

            m_Board.Add(panel);
            root.Add(m_Board);
        }

        /// <summary>
        /// The line of column titles above the players.
        /// </summary>
        private VisualElement BuildHeaderRow()
        {
            var header = MakeRowContainer();
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
            header.style.paddingBottom = 6f;
            header.style.marginBottom = 6f;

            header.Add(MakeCell("#", 34f, k_FaintColor, TextAnchor.MiddleLeft));
            header.Add(MakeCell("PLAYER", 0f, k_FaintColor, TextAnchor.MiddleLeft, grow: true));
            header.Add(MakeCell("ROUNDS", 78f, k_FaintColor, TextAnchor.MiddleRight));
            header.Add(MakeCell("KILLS", 62f, k_FaintColor, TextAnchor.MiddleRight));
            header.Add(MakeCell("DEATHS", 70f, k_FaintColor, TextAnchor.MiddleRight));

            return header;
        }

        private void LateUpdate()
        {
            if (m_Board == null)
            {
                return;
            }

            if (!m_Match.TryRead(out var match) || match.State != MatchState.MatchEnded)
            {
                m_Board.style.display = DisplayStyle.None;

                // Reset so that the table is rebuilt the instant the next match ends
                // rather than up to half a second later.
                m_TimeUntilRefresh = 0f;
                return;
            }

            m_Board.style.display = DisplayStyle.Flex;

            m_TimeUntilRefresh -= Time.deltaTime;
            if (m_TimeUntilRefresh > 0f)
            {
                return;
            }

            m_TimeUntilRefresh = m_RefreshSeconds;
            Refresh(match);
        }

        /// <summary>
        /// Rebuilds the table from the leaderboard.
        ///
        /// Rebuilt on a timer rather than every frame because GetScores hands back a fresh
        /// list each time it is called, and a screen that nobody is going to act on does
        /// not need sixty of those a second.
        /// </summary>
        private void Refresh(MatchManager.MatchStateData match)
        {
            m_Rows.Clear();

            if (LeaderboardManager.Instance != null)
            {
                foreach (var score in LeaderboardManager.Instance.GetScores())
                {
                    m_Rows.Add(new Row
                    {
                        NetworkId = score.NetworkId,
                        Name = score.PlayerName.ToString(),
                        RoundWins = score.RoundWins,
                        Kills = score.Kills,
                        Deaths = score.Deaths
                    });
                }
            }

            // Rounds first, kills as the tie-break, deaths as the last one: of two players
            // level on both, the one who died less did more with the same result.
            m_Rows.Sort((a, b) =>
            {
                if (a.RoundWins != b.RoundWins)
                {
                    return b.RoundWins.CompareTo(a.RoundWins);
                }

                if (a.Kills != b.Kills)
                {
                    return b.Kills.CompareTo(a.Kills);
                }

                return a.Deaths.CompareTo(b.Deaths);
            });

            m_TitleLabel.text = m_Rows.Count > 0 ? $"{m_Rows[0].Name} WINS" : "MATCH OVER";
            m_SubtitleLabel.text = BuildSubtitle(match);

            DrawRows();
        }

        /// <summary>
        /// The line under the title, saying what was played.
        /// </summary>
        private string BuildSubtitle(MatchManager.MatchStateData match)
        {
            int maxRounds = MatchFormatRules.MaxRounds(match.Format);

            return maxRounds == 1
                ? "SINGLE ROUND"
                : $"BEST OF {maxRounds.ToString()} - FIRST TO {MatchFormatRules.RoundsToWin(match.Format).ToString()}";
        }

        /// <summary>
        /// Puts the sorted rows on screen, reusing the elements already there.
        /// </summary>
        private void DrawRows()
        {
            // Grown to fit, never shrunk: the spare rows are simply hidden, so a player
            // leaving does not cost a rebuild of every element below them.
            while (m_RowsContainer.childCount < m_Rows.Count)
            {
                m_RowsContainer.Add(MakePlayerRow());
            }

            for (int i = 0; i < m_RowsContainer.childCount; i++)
            {
                var element = m_RowsContainer[i];

                if (i >= m_Rows.Count)
                {
                    element.style.display = DisplayStyle.None;
                    continue;
                }

                element.style.display = DisplayStyle.Flex;

                var row = m_Rows[i];

                // This client's own line is picked out, because a table of five names is
                // useless if you have to remember which one you are.
                bool isYou = row.NetworkId == m_Match.LocalNetworkId;
                Color colour = isYou ? k_YouColor : k_TextColor;

                SetCell(element, 0, $"{(i + 1).ToString()}", colour);
                SetCell(element, 1, row.Name, colour);
                SetCell(element, 2, row.RoundWins.ToString(), colour);
                SetCell(element, 3, row.Kills.ToString(), colour);
                SetCell(element, 4, row.Deaths.ToString(), colour);
            }
        }

        private VisualElement MakePlayerRow()
        {
            var row = MakeRowContainer();
            row.style.paddingTop = 3f;
            row.style.paddingBottom = 3f;

            row.Add(MakeCell(string.Empty, 34f, k_TextColor, TextAnchor.MiddleLeft));
            row.Add(MakeCell(string.Empty, 0f, k_TextColor, TextAnchor.MiddleLeft, grow: true));
            row.Add(MakeCell(string.Empty, 78f, k_TextColor, TextAnchor.MiddleRight));
            row.Add(MakeCell(string.Empty, 62f, k_TextColor, TextAnchor.MiddleRight));
            row.Add(MakeCell(string.Empty, 70f, k_TextColor, TextAnchor.MiddleRight));

            return row;
        }

        private static void SetCell(VisualElement row, int index, string text, Color colour)
        {
            var label = (Label)row[index];
            label.text = text;
            label.style.color = colour;
        }

        private static VisualElement MakeRowContainer()
        {
            var row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            return row;
        }

        private static Label MakeCell(string text, float width, Color colour, TextAnchor align, bool grow = false)
        {
            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = 15f;
            label.style.color = colour;
            label.style.unityTextAlign = align;

            if (grow)
            {
                label.style.flexGrow = 1f;
            }
            else
            {
                label.style.width = width;
                label.style.flexShrink = 0f;
            }

            return label;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetBorder(VisualElement element, Color colour, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;

            element.style.borderTopColor = colour;
            element.style.borderRightColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
        }
    }
}
