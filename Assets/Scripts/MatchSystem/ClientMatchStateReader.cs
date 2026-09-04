using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Match
{
    /// <summary>
    /// Everything a screen needs to know about the match, read from the client's own copy
    /// of the world: what the match is doing, whether this player is still standing, and
    /// which player they are.
    ///
    /// It exists because three different screens need the same three answers - the round
    /// timer, the screen a dead player looks at, and the results board - and each one
    /// finding the client world and building its own queries would be the same twenty
    /// lines written three times, with three places to fix when a query changes.
    ///
    /// It only reads. Nothing here can change the match, which is the server's business.
    /// </summary>
    public sealed class ClientMatchStateReader
    {
        private World m_ClientWorld;
        private EntityManager m_EntityManager;

        private EntityQuery m_MatchQuery;
        private EntityQuery m_LocalPlayerQuery;
        private EntityQuery m_NetworkIdQuery;

        /// <summary>
        /// True while this client has a character of its own on the field. False covers
        /// being eliminated, the gap between rounds, and the seconds before the first
        /// spawn - all of which look the same from a screen's point of view.
        /// </summary>
        public bool IsLocalPlayerAlive { get; private set; }

        /// <summary>
        /// Which player this client is, or -1 before the connection has one.
        ///
        /// Read from the connection rather than from the character, because a dead player
        /// has no character and the results board still has to know which row is theirs.
        /// </summary>
        public int LocalNetworkId { get; private set; } = -1;

        /// <summary>
        /// Refreshes everything and hands back the match state. Returns false when there
        /// is no match to read - no client world yet, or no MatchManager ghost - and every
        /// screen treats that as "show nothing" rather than as an error, so that a scene
        /// without a match still runs.
        /// </summary>
        public bool TryRead(out MatchManager.MatchStateData data)
        {
            data = default;

            // The world is looked up lazily and again when it goes away, because it is
            // created when the match starts and destroyed on the way back to the menu.
            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                Initialize();
            }

            if (m_ClientWorld == null)
            {
                IsLocalPlayerAlive = false;
                LocalNetworkId = -1;
                return false;
            }

            ReadLocalPlayer();

            var states = m_MatchQuery.ToComponentDataArray<MatchManager.MatchStateData>(Allocator.Temp);

            try
            {
                // No manager ghost yet. It arrives a moment after the connection does, so
                // this is normal for the first frames rather than a fault.
                if (states.Length == 0)
                {
                    return false;
                }

                data = states[0];
                return true;
            }
            finally
            {
                states.Dispose();
            }
        }

        /// <summary>
        /// Works out whether this client still has a character, and which player it is.
        /// </summary>
        private void ReadLocalPlayer()
        {
            // Asked for as a list rather than as a singleton: GhostOwnerIsLocal is an
            // enableable component, and Entities refuses the singleton calls on a query
            // holding one.
            var localPlayers = m_LocalPlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                IsLocalPlayerAlive = localPlayers.Length > 0;
            }
            finally
            {
                localPlayers.Dispose();
            }

            var networkIds = m_NetworkIdQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);

            try
            {
                // The connection outlives the character, which is the whole reason the id
                // is taken from here.
                LocalNetworkId = networkIds.Length > 0 ? networkIds[0].Value : -1;
            }
            finally
            {
                networkIds.Dispose();
            }
        }

        /// <summary>
        /// Finds the client world and builds the three queries.
        /// </summary>
        private void Initialize()
        {
            m_ClientWorld = null;
            m_EntityManager = default;

            foreach (var world in World.All)
            {
                if (world.IsClient())
                {
                    m_ClientWorld = world;
                    m_EntityManager = world.EntityManager;
                    break;
                }
            }

            if (m_ClientWorld == null)
            {
                return;
            }

            m_MatchQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MatchManager.MatchStateData>());

            m_LocalPlayerQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PredictedPlayerGhost>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());

            m_NetworkIdQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>());
        }
    }
}
