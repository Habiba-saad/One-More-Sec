using Gameplay.Leaderboard;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// The referee. It owns the round clock, decides when a round is over and who won it,
    /// counts the wins, and says when the match itself has been decided.
    ///
    /// One per match, spawned as a manager ghost exactly like LeaderboardManager - which
    /// is also why MatchStateData is declared inside this class: GhostGameObjectBaker adds
    /// the types nested in a GhostMonoBehaviour to its ghost entity, and that is how the
    /// state reaches the clients without a hand-written baker.
    ///
    /// It decides, it does not spawn. Putting players back on the field stays in
    /// ServerGameSystem, which already owns the prefabs and the spawn points; this class
    /// only opens the window during which that is allowed. Keeping the decision and the
    /// spawning apart is what stops a respawn rule from having to be written twice.
    ///
    /// Server only. Every field below is written on the server and read everywhere.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchManager : GhostMonoBehaviour, IGhostManager, IUpdateServer
    {
        /// <summary>
        /// The match as every client sees it: which state, which round, how long is left,
        /// and who won the last round.
        ///
        /// Replicated because every client draws it - the round timer, the countdown, the
        /// "next round in..." screen and the results board all read these fields.
        /// </summary>
        public struct MatchStateData : IComponentData
        {
            // Which state, kept as a byte rather than as the enum itself. An integer is
            // the one shape every version of Netcode serialises the same way, so the enum
            // is cast at the two edges instead of being trusted to survive the wire.
            [GhostField] public byte StateValue;

            // The chosen format, same reasoning. Sent because the results screen wants to
            // say "best of 3" and the client has no other way to know.
            [GhostField] public byte FormatValue;

            // Which round is being played, counting from 1. Zero before the first starts.
            [GhostField] public int CurrentRound;

            // Seconds left in whatever state we are in: the countdown before a round, the
            // round itself, or the gap before the next one. One field for all three,
            // because the state already says what is being counted and two separate
            // timers could disagree about which one is running.
            [GhostField] public float StateSeconds;

            // Who won the round that just finished, or -1 when nobody did - which is what
            // happens when the clock runs out with more than one player still standing.
            [GhostField] public int LastRoundWinnerId;

            /// <summary>The state as something to switch on rather than a number.</summary>
            public MatchState State
            {
                get => (MatchState)StateValue;
                set => StateValue = (byte)value;
            }

            /// <summary>The format as something to switch on rather than a number.</summary>
            public MatchFormat Format
            {
                get => (MatchFormat)FormatValue;
                set => FormatValue = (byte)value;
            }

            /// <summary>
            /// True while players should be walking around and shooting.
            /// </summary>
            public bool IsRoundLive => State == MatchState.RoundInProgress;

            /// <summary>
            /// True only during the moment players are put back on the field.
            /// ServerGameSystem respawns nobody outside this window, and that single fact
            /// is what keeps a killed player out of the game until the next round instead
            /// of five seconds later.
            /// </summary>
            public bool AllowsRespawn => State == MatchState.RoundStarting;
        }

        public static MatchManager Instance { get; private set; }

        [Header("Format")]
        [Tooltip("How many rounds the match runs for. The lobby will set this later.")]
        [SerializeField]
        private MatchFormat m_Format = MatchFormat.OneRound;

        [Header("Timing")]
        [Tooltip("How long one round lasts, in seconds. 150 is two and a half minutes.")]
        [Min(10f)]
        [SerializeField]
        private float m_RoundSeconds = 150f;

        [Tooltip("Countdown after everyone is respawned and before the round goes live.")]
        [Min(0f)]
        [SerializeField]
        private float m_RoundStartSeconds = 5f;

        [Tooltip("How long the result of a round stays up before the next one begins.")]
        [Min(1f)]
        [SerializeField]
        private float m_RoundEndSeconds = 6f;

        [Header("Players")]
        [Tooltip("How many players have to be connected before the first round starts.")]
        [Min(1)]
        [SerializeField]
        private int m_MinPlayersToStart = 2;

        // Every player character currently in the world. Built once: creating a query is
        // the expensive part, running it is not.
        private EntityQuery m_PlayerQuery;
        private bool m_QueryBuilt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Sets the format the match will be played in. Only meaningful before the first
        /// round starts - changing it half way through would move the finish line while
        /// people are running at it, so it is refused once the match is under way.
        /// </summary>
        public void SetFormat(MatchFormat format)
        {
            if (!TryReadState(out var data) || data.State != MatchState.Waiting)
            {
                Debug.LogWarning($"{name}: the format cannot be changed once the match has started.", this);
                return;
            }

            m_Format = format;
            data.Format = format;
            WriteGhostComponentData(data);
        }

        /// <summary>
        /// Runs the clock and moves the match from one state to the next.
        /// </summary>
        public void UpdateServer(float deltaTime)
        {
            if (!TryReadState(out var data))
            {
                return;
            }

            // The format is carried on the state so the clients can read it, and this is
            // the one place it is copied across.
            data.Format = m_Format;

            switch (data.State)
            {
                case MatchState.Waiting:
                    UpdateWaiting(ref data);
                    break;

                case MatchState.RoundStarting:
                    UpdateRoundStarting(ref data, deltaTime);
                    break;

                case MatchState.RoundInProgress:
                    UpdateRoundInProgress(ref data, deltaTime);
                    break;

                case MatchState.RoundEnded:
                    UpdateRoundEnded(ref data, deltaTime);
                    break;

                case MatchState.MatchEnded:
                    // Nothing left to run. The results board is up and the only way out is
                    // back to the menu.
                    break;
            }

            WriteGhostComponentData(data);
        }

        /// <summary>
        /// Waits for enough people to be connected, then starts the first round.
        /// </summary>
        private void UpdateWaiting(ref MatchStateData data)
        {
            if (CountPlayers(out _) < m_MinPlayersToStart)
            {
                return;
            }

            BeginRound(ref data, 1);
        }

        /// <summary>
        /// Counts down the few seconds between everyone being respawned and the round
        /// going live.
        /// </summary>
        private void UpdateRoundStarting(ref MatchStateData data, float deltaTime)
        {
            data.StateSeconds -= deltaTime;

            if (data.StateSeconds > 0f)
            {
                return;
            }

            data.State = MatchState.RoundInProgress;
            data.StateSeconds = m_RoundSeconds;

            Debug.Log($"[Server] Round {data.CurrentRound.ToString()} is live.");
        }

        /// <summary>
        /// Runs the round clock and watches for it being over.
        /// </summary>
        private void UpdateRoundInProgress(ref MatchStateData data, float deltaTime)
        {
            data.StateSeconds -= deltaTime;

            int alive = CountPlayers(out int lastAliveNetworkId);

            // One player left standing: they won it. Checked before the clock, so that the
            // last kill of a round still counts even if it lands on the final tick.
            if (alive <= 1)
            {
                EndRound(ref data, alive == 1 ? lastAliveNetworkId : -1);
                return;
            }

            // Time up with more than one player alive. The round is void: nobody takes a
            // win from it, and the score stays where it was. Surviving a clock is not the
            // same as beating anybody.
            if (data.StateSeconds <= 0f)
            {
                EndRound(ref data, -1);
            }
        }

        /// <summary>
        /// Holds the result on screen, then either starts the next round or ends the
        /// match.
        /// </summary>
        private void UpdateRoundEnded(ref MatchStateData data, float deltaTime)
        {
            data.StateSeconds -= deltaTime;

            if (data.StateSeconds > 0f)
            {
                return;
            }

            // Somebody has taken enough rounds, so there is nothing left worth playing -
            // a best of five stops at three wins rather than playing two dead rubbers.
            if (HasMatchWinner())
            {
                data.State = MatchState.MatchEnded;
                data.StateSeconds = 0f;
                Debug.Log("[Server] Match over.");
                return;
            }

            BeginRound(ref data, data.CurrentRound + 1);
        }

        /// <summary>
        /// Opens the respawn window and starts the countdown for a round.
        ///
        /// Nobody is spawned here. Moving to RoundStarting is what tells ServerGameSystem
        /// that respawning is allowed, and it puts everyone back on the field on its own
        /// next frame.
        /// </summary>
        private void BeginRound(ref MatchStateData data, int roundNumber)
        {
            data.State = MatchState.RoundStarting;
            data.CurrentRound = roundNumber;
            data.StateSeconds = m_RoundStartSeconds;
            data.LastRoundWinnerId = -1;

            Debug.Log($"[Server] Round {roundNumber.ToString()} starting.");
        }

        /// <summary>
        /// Closes a round, awarding it to <paramref name="winnerNetworkId"/> or to nobody
        /// when that is -1.
        /// </summary>
        private void EndRound(ref MatchStateData data, int winnerNetworkId)
        {
            data.State = MatchState.RoundEnded;
            data.StateSeconds = m_RoundEndSeconds;
            data.LastRoundWinnerId = winnerNetworkId;

            if (winnerNetworkId >= 0)
            {
                // The leaderboard keeps every number a player has earned, so the round win
                // is recorded there next to their kills rather than in a second tally here
                // that the results board would then have to join up.
                if (LeaderboardManager.Instance != null)
                {
                    LeaderboardManager.Instance.AddRoundWin(winnerNetworkId);
                }

                Debug.Log($"[Server] Round {data.CurrentRound.ToString()} won by player {winnerNetworkId.ToString()}.");
            }
            else
            {
                Debug.Log($"[Server] Round {data.CurrentRound.ToString()} ended with nobody winning it.");
            }

            ClearBattlefield();
        }

        /// <summary>
        /// Takes whoever is still standing off the field, so that the next round starts
        /// with everybody spawned fresh rather than with the winner keeping the health,
        /// ammo and position they finished the last one with.
        ///
        /// Done by emptying their health rather than by destroying the character here,
        /// because ServerGameSystem already turns an empty health bar into a destroyed
        /// character and a connection waiting to respawn. Writing that a second time would
        /// mean two places to fix the day it changes.
        /// </summary>
        private void ClearBattlefield()
        {
            EnsureQuery();

            var entities = m_PlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                var entityManager = World.EntityManager;

                foreach (var entity in entities)
                {
                    var ghost = entityManager.GetComponentData<PredictedPlayerGhost>(entity);

                    // Already dead: leave them alone, they are on their way out anyway.
                    if (ghost.CurrentHealth <= 0f)
                    {
                        continue;
                    }

                    ghost.CurrentHealth = 0f;
                    entityManager.SetComponentData(entity, ghost);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }

        /// <summary>
        /// True once somebody has won enough rounds to take the match.
        /// </summary>
        private bool HasMatchWinner()
        {
            if (LeaderboardManager.Instance == null)
            {
                return false;
            }

            int roundsToWin = MatchFormatRules.RoundsToWin(m_Format);

            foreach (var score in LeaderboardManager.Instance.GetScores())
            {
                if (score.RoundWins >= roundsToWin)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How many players are alive right now, and who the last one is when only one is
        /// left.
        /// </summary>
        private int CountPlayers(out int lastAliveNetworkId)
        {
            lastAliveNetworkId = -1;

            EnsureQuery();

            var entities = m_PlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                var entityManager = World.EntityManager;
                int alive = 0;

                foreach (var entity in entities)
                {
                    // A character whose health has reached zero is still in the world for
                    // the frame or two before it is destroyed, and counting it would keep
                    // a round running after its last kill.
                    if (entityManager.GetComponentData<PredictedPlayerGhost>(entity).CurrentHealth <= 0f)
                    {
                        continue;
                    }

                    alive++;
                    lastAliveNetworkId = entityManager.GetComponentData<GhostOwner>(entity).NetworkId;
                }

                // Only meaningful when exactly one is left; with more than one it is
                // whichever happened to come last out of the query.
                if (alive != 1)
                {
                    lastAliveNetworkId = -1;
                }

                return alive;
            }
            finally
            {
                entities.Dispose();
            }
        }

        /// <summary>
        /// Builds the query over every player character, once.
        /// </summary>
        private void EnsureQuery()
        {
            if (m_QueryBuilt)
            {
                return;
            }

            m_PlayerQuery = World.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PredictedPlayerGhost>(),
                ComponentType.ReadOnly<GhostOwner>());

            m_QueryBuilt = true;
        }

        /// <summary>
        /// The current state, or false while the ghost has not been linked yet - which is
        /// true for the first frames of a session.
        /// </summary>
        private bool TryReadState(out MatchStateData data)
        {
            if (GhostGameObject == null || !GhostGameObject.IsGhostLinked() || !GhostHasComponent<MatchStateData>())
            {
                data = default;
                return false;
            }

            ReadGhostComponentData(out data);
            return true;
        }
    }
}
