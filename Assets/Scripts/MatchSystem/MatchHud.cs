using UnityEngine;
using UnityEngine.UIElements;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// The round strip across the top of the screen: which round is being played and how
    /// long is left of it.
    ///
    /// Deliberately only those two things. The match has plenty more to say - who won the
    /// last round, when the next one starts, the final table - but all of that belongs on
    /// a screen a dead player can also see, and this strip is part of the HUD, which
    /// disappears with the rest of it the moment its player does.
    ///
    /// Built in code rather than from a UXML file so it can be dropped onto the existing
    /// HUD without editing a layout the rest of the team owns.
    ///
    /// Put it on the same GameObject as InGameHUD - the one carrying the UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class MatchHud : MonoBehaviour
    {
        [Tooltip("Distance from the top of the screen, in pixels.")]
        [SerializeField]
        private float m_TopMargin = 74f;

        [Tooltip("Seconds left at which the clock turns red.")]
        [Min(0f)]
        [SerializeField]
        private float m_WarningSeconds = 20f;

        private readonly ClientMatchStateReader m_Match = new ClientMatchStateReader();

        private VisualElement m_Strip;
        private Label m_RoundLabel;
        private Label m_TimerLabel;

        private static readonly Color k_NormalColor = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color k_WarningColor = new Color(1f, 0.34f, 0.34f, 1f);

        private void OnEnable()
        {
            BuildStrip();
        }

        private void OnDisable()
        {
            // Taken out of the tree rather than hidden, so a domain reload cannot leave
            // two strips stacked on top of each other.
            m_Strip?.RemoveFromHierarchy();
            m_Strip = null;
        }

        /// <summary>
        /// Creates the strip once and hangs it off the HUD's root.
        /// </summary>
        private void BuildStrip()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                return;
            }

            m_Strip = new VisualElement { name = "match-strip" };
            m_Strip.pickingMode = PickingMode.Ignore;
            m_Strip.style.position = Position.Absolute;
            m_Strip.style.top = m_TopMargin;
            m_Strip.style.left = 0f;
            m_Strip.style.right = 0f;
            m_Strip.style.alignItems = Align.Center;

            m_RoundLabel = new Label("ROUND 1") { name = "match-round-label" };
            m_RoundLabel.pickingMode = PickingMode.Ignore;
            m_RoundLabel.style.fontSize = 13f;
            m_RoundLabel.style.letterSpacing = 2f;
            m_RoundLabel.style.color = new Color(1f, 1f, 1f, 0.55f);
            m_Strip.Add(m_RoundLabel);

            m_TimerLabel = new Label("0:00") { name = "match-timer-label" };
            m_TimerLabel.pickingMode = PickingMode.Ignore;
            m_TimerLabel.style.fontSize = 30f;
            m_TimerLabel.style.color = k_NormalColor;
            m_Strip.Add(m_TimerLabel);

            root.Add(m_Strip);
        }

        private void LateUpdate()
        {
            if (m_Strip == null)
            {
                return;
            }

            if (!m_Match.TryRead(out var match))
            {
                m_Strip.style.display = DisplayStyle.None;
                return;
            }

            switch (match.State)
            {
                case MatchState.RoundStarting:
                    // The countdown before the round goes live. Shown as a plain number,
                    // because seconds and no minutes is what a three second wait is.
                    ShowStrip(match.CurrentRound, Mathf.CeilToInt(match.StateSeconds).ToString(), false);
                    break;

                case MatchState.RoundInProgress:
                    ShowStrip(match.CurrentRound, FormatClock(match.StateSeconds),
                        match.StateSeconds <= m_WarningSeconds);
                    break;

                default:
                    // Waiting, between rounds, or the match is over. All three have their
                    // own screen to say so, and a clock counting something else down in
                    // the corner of it would only be confusing.
                    m_Strip.style.display = DisplayStyle.None;
                    break;
            }
        }

        /// <summary>
        /// Puts the strip on screen with this round number and clock.
        /// </summary>
        private void ShowStrip(int round, string clock, bool warning)
        {
            m_Strip.style.display = DisplayStyle.Flex;
            m_RoundLabel.text = $"ROUND {round.ToString()}";
            m_TimerLabel.text = clock;
            m_TimerLabel.style.color = warning ? k_WarningColor : k_NormalColor;
        }

        /// <summary>
        /// Seconds as m:ss.
        /// </summary>
        private static string FormatClock(float seconds)
        {
            // Rounded up, so the clock reads 0:01 through the last second and only shows
            // 0:00 when the time really is gone - a clock that sits on zero for a whole
            // second looks broken.
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = total / 60;
            int remainder = total % 60;

            return $"{minutes.ToString()}:{remainder.ToString("00")}";
        }
    }
}
