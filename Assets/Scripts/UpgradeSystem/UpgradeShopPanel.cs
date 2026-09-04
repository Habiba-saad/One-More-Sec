using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.MP_FPS.Oxygen;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The upgrade shop, bottom left of the screen: one rectangle split into a cell per
    /// upgrade, each cell showing its icon and its price in oxygen seconds. Buying one
    /// lights its cell up and fills it with a bar that empties downwards as the effect
    /// runs out, and the cell goes quiet again when it does.
    ///
    /// This is the client half of the shop, and it only ever reads. Every decision -
    /// whether the player can afford an upgrade, whether it is already running, when it
    /// expires - is made by UpgradeController on the server, and arrives here as the
    /// ActiveUpgradeStatus buffer. A panel that decided any of that for itself would be a
    /// second copy of the rules that could disagree with the first.
    ///
    /// It builds its own elements in code rather than from a UXML file so that it can be
    /// dropped onto the existing HUD without editing the HUD's own layout, which belongs
    /// to the rest of the team.
    ///
    /// Put it on the same GameObject as InGameHUD - the one carrying the UIDocument - and
    /// fill in one slot per upgrade below.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class UpgradeShopPanel : MonoBehaviour
    {
        /// <summary>
        /// One cell of the panel, as it is filled in from the Inspector.
        /// </summary>
        [Serializable]
        private class UpgradeSlot
        {
            [Tooltip("The upgrade this cell buys: SpeedBoostData, DamageBoostData or PlayerScanData.")]
            public SuitUpgradeData Data;

            [Tooltip("Optional picture for the cell. Left empty, the cell is a plain block of Tint.")]
            public Texture2D Icon;

            [Tooltip("The colour this cell lights up in while its upgrade is running.")]
            public Color Tint = new Color(0.29f, 0.83f, 0.43f, 1f);

            [Tooltip("The key that buys it, drawn in the corner of the cell. Purely a label.")]
            public string HotKeyLabel = "1";
        }

        /// <summary>
        /// The elements of one cell, kept together so the update loop can reach them
        /// without searching the tree by name every frame.
        /// </summary>
        private class SlotView
        {
            public UpgradeSlot Slot;
            public VisualElement Cell;
            public VisualElement Icon;
            public VisualElement Fill;
            public Label Price;
        }

        [Header("Cells, left to right")]
        [SerializeField]
        private UpgradeSlot[] m_Slots = new UpgradeSlot[0];

        [Header("Layout")]
        [Tooltip("Size of one square cell, in pixels.")]
        [SerializeField]
        private float m_CellSize = 82f;

        [Tooltip("Distance from the left edge of the screen, in pixels.")]
        [SerializeField]
        private float m_ScreenMargin = 20f;

        [Tooltip("Gap left between the panel and the health bar it sits above, in pixels.")]
        [SerializeField]
        private float m_GapAboveHealthBar = 26f;

        [Tooltip("Used only if the health bar cannot be found in the HUD.")]
        [SerializeField]
        private float m_FallbackBottomMargin = 92f;

        // The cells, in the order they were filled in.
        private readonly List<SlotView> m_Views = new List<SlotView>();

        // What is running right now, as upgrade id to seconds left. Rebuilt from the ghost
        // buffer every frame, and kept as a field so that reading it allocates nothing.
        private readonly Dictionary<int, float> m_Running = new Dictionary<int, float>();

        // The air this player has left, which is also their wallet. Starts at infinity so
        // that a player whose oxygen cannot be read is never told they are too poor to buy
        // something - the server would refuse the purchase anyway if they really were.
        private float m_OxygenSeconds = float.PositiveInfinity;

        // The panel itself, so it can be hidden while there is no player to show it for.
        private VisualElement m_Panel;

        // The health bar the panel lines itself up with. Held rather than searched for
        // every frame, and allowed to be null: a HUD without one is not an error, the
        // panel just falls back to the fixed margins above.
        private VisualElement m_HealthBar;

        // Where the panel was last put, so the layout is only dirtied when it really moves
        // rather than on every single frame.
        private float m_AppliedLeft = float.NaN;
        private float m_AppliedBottom = float.NaN;

        // The client's ECS world, and the query that finds this player's own ghost in it.
        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;

        // Faces of a cell that is doing nothing. Named rather than repeated inline, so the
        // "off" look is defined in one place.
        private static readonly Color k_IdleBackground = new Color(0.06f, 0.07f, 0.09f, 0.72f);
        private static readonly Color k_IdleBorder = new Color(1f, 1f, 1f, 0.18f);

        // And of a cell the player cannot pay for yet.
        private static readonly Color k_UnaffordableBorder = new Color(0.85f, 0.25f, 0.25f, 0.45f);
        private static readonly Color k_UnaffordablePrice = new Color(1f, 0.36f, 0.36f, 0.9f);

        private void OnEnable()
        {
            BuildPanel();
        }

        private void OnDisable()
        {
            // Taken out of the tree rather than left hidden, so that a domain reload or a
            // scene change cannot end up with two panels stacked on top of each other.
            m_Panel?.RemoveFromHierarchy();
            m_Panel = null;
            m_Views.Clear();
        }

        /// <summary>
        /// Creates the rectangle and its cells once, and hangs them off the HUD's root.
        /// </summary>
        private void BuildPanel()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                return;
            }

            m_Panel = new VisualElement { name = "upgrade-shop-panel" };

            // Absolute, so it sits in the corner no matter how the rest of the HUD is laid
            // out - this panel is a guest in someone else's document and should not push
            // anything else around.
            m_Panel.style.position = Position.Absolute;
            m_Panel.style.left = m_ScreenMargin;
            m_Panel.style.bottom = m_FallbackBottomMargin;
            m_Panel.style.flexDirection = FlexDirection.Row;

            // The health bar shares this corner. Found by the name it carries in
            // PlayerHUD.uxml, and only read from - the panel moves itself out of the way
            // rather than moving somebody else's element.
            //
            // The bar itself and not the block around it: that block carries padding, so
            // lining up with it would leave the panel sitting a centimetre to the left of
            // the bar the player actually sees.
            m_HealthBar = root.Q<VisualElement>("player-health-bar");

            // The player shoots through this corner of the screen. Nothing here is
            // clickable, so the panel must never swallow a click meant for the game.
            m_Panel.pickingMode = PickingMode.Ignore;

            foreach (var slot in m_Slots)
            {
                // A slot with no asset is an unfinished row in the Inspector, not a cell.
                if (slot == null || slot.Data == null)
                {
                    continue;
                }

                m_Panel.Add(BuildCell(slot));
            }

            root.Add(m_Panel);
        }

        /// <summary>
        /// Builds one cell: the block itself, the icon, the price, the hot key and the bar
        /// that will empty down it.
        /// </summary>
        private VisualElement BuildCell(UpgradeSlot slot)
        {
            var cell = new VisualElement { name = $"upgrade-cell-{slot.Data.UpgradeId.ToString()}" };
            cell.pickingMode = PickingMode.Ignore;
            cell.style.width = m_CellSize;
            cell.style.height = m_CellSize;
            cell.style.marginRight = 8f;
            cell.style.backgroundColor = k_IdleBackground;
            cell.style.overflow = Overflow.Hidden;
            SetBorder(cell, k_IdleBorder, 1f);
            SetRadius(cell, 6f);

            // The countdown, sitting behind everything else and anchored to the bottom of
            // the cell. Its height is the only thing that changes while an upgrade runs,
            // so the top edge slides downwards as the seconds go - which is what makes the
            // bar read as draining rather than merely shrinking.
            var fill = new VisualElement { name = "upgrade-cell-fill" };
            fill.pickingMode = PickingMode.Ignore;
            fill.style.position = Position.Absolute;
            fill.style.left = 0f;
            fill.style.right = 0f;
            fill.style.bottom = 0f;
            fill.style.height = Length.Percent(0f);
            cell.Add(fill);

            // The picture. Without one the block of colour is the icon, which is enough to
            // tell three cells apart until the real art arrives.
            var icon = new VisualElement { name = "upgrade-cell-icon" };
            icon.pickingMode = PickingMode.Ignore;
            icon.style.position = Position.Absolute;
            icon.style.left = 10f;
            icon.style.right = 10f;
            icon.style.top = 10f;
            icon.style.height = m_CellSize * 0.45f;
            SetRadius(icon, 4f);
            if (slot.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(slot.Icon);
            }
            cell.Add(icon);

            // The price, in the oxygen seconds it costs. Read from the asset, so retuning
            // the balance in the Inspector retunes the label with it.
            var price = new Label($"{Mathf.RoundToInt(slot.Data.CostOxygen).ToString()}s")
            {
                name = "upgrade-cell-price"
            };
            price.pickingMode = PickingMode.Ignore;
            price.style.position = Position.Absolute;
            price.style.left = 0f;
            price.style.right = 0f;
            price.style.bottom = 6f;
            price.style.unityTextAlign = TextAnchor.MiddleCenter;
            price.style.fontSize = 14f;
            price.style.color = Color.white;
            cell.Add(price);

            // Which key buys it. A stand-in for a real shop, and the label goes when the
            // panel becomes clickable.
            var key = new Label(slot.HotKeyLabel) { name = "upgrade-cell-key" };
            key.pickingMode = PickingMode.Ignore;
            key.style.position = Position.Absolute;
            key.style.top = 4f;
            key.style.left = 6f;
            key.style.fontSize = 11f;
            key.style.color = new Color(1f, 1f, 1f, 0.55f);
            cell.Add(key);

            m_Views.Add(new SlotView
            {
                Slot = slot,
                Cell = cell,
                Icon = icon,
                Fill = fill,
                Price = price
            });

            return cell;
        }

        private void LateUpdate()
        {
            if (m_Panel == null)
            {
                return;
            }

            // Nothing to draw until this client has a player of its own on the field. That
            // covers the menu, the loading screen and the seconds after being killed.
            if (!TryReadRunningUpgrades())
            {
                m_Panel.style.display = DisplayStyle.None;
                return;
            }

            m_Panel.style.display = DisplayStyle.Flex;
            KeepAboveHealthBar();

            foreach (var view in m_Views)
            {
                // Not in the dictionary means not running, which is the resting state and
                // by far the common one.
                bool isRunning = m_Running.TryGetValue(view.Slot.Data.UpgradeId, out float remaining);

                // Only a hint for the player, never a rule: the server re-checks the price
                // at the moment of purchase, because the tank keeps draining while they
                // are deciding. See IOxygenBank.
                bool canAfford = m_OxygenSeconds >= view.Slot.Data.CostOxygen;

                DrawCell(view, isRunning, canAfford, remaining);
            }
        }

        /// <summary>
        /// Slides the panel up so that it sits on top of the health bar rather than over
        /// it.
        ///
        /// Measured every frame instead of written down as a number, because the health
        /// bar belongs to the rest of the team: if its height or its distance from the
        /// floor changes, the panel follows it without this file being touched.
        /// </summary>
        private void KeepAboveHealthBar()
        {
            float left = m_ScreenMargin;
            float bottom = m_FallbackBottomMargin;

            var parent = m_Panel.parent;
            if (m_HealthBar != null && parent != null)
            {
                var bar = m_HealthBar.worldBound;
                var area = parent.worldBound;

                // The first frames run before UI Toolkit has laid anything out, and the
                // empty rectangle it reports then would throw the panel into the corner.
                // The fixed margins cover those frames.
                if (bar.height > 0f && area.height > 0f)
                {
                    // Both rectangles are in screen space, so they are brought back into
                    // the coordinates the panel is positioned in before being used.
                    left = bar.xMin - area.xMin;

                    // Measured from the bar's top edge up, which is what leaves a real gap
                    // rather than letting the two touch.
                    bottom = area.yMax - bar.yMin + m_GapAboveHealthBar;
                }
            }

            // Writing a style is what marks the element for a fresh layout pass, so it is
            // only worth doing when a value actually changed.
            if (!Mathf.Approximately(left, m_AppliedLeft))
            {
                m_Panel.style.left = left;
                m_AppliedLeft = left;
            }

            if (!Mathf.Approximately(bottom, m_AppliedBottom))
            {
                m_Panel.style.bottom = bottom;
                m_AppliedBottom = bottom;
            }
        }

        /// <summary>
        /// Reads the ghost buffer into <see cref="m_Running"/>. Returns false when this
        /// client has no player entity to read from, which is also when the panel should
        /// not be on screen at all.
        /// </summary>
        private bool TryReadRunningUpgrades()
        {
            // The world is looked up lazily and re-looked-up when it goes away, because it
            // is created when the match starts and destroyed on the way back to the menu.
            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                InitializeEcs();
            }

            if (m_ClientWorld == null)
            {
                return false;
            }

            // Asked for as a list rather than as a singleton, even though there is only
            // ever one of these. GhostOwnerIsLocal is an enableable component, and Entities
            // refuses GetSingletonEntity on any query holding one, because a singleton
            // cannot say whether it means the entities that have the component or the ones
            // that have it switched on. A Temp array lives until the end of the frame and
            // is the cheap way to ask the question properly.
            var localPlayers = m_LocalPlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                // No player of our own on the field: the menu, the loading screen, or the
                // moment after being killed.
                if (localPlayers.Length == 0)
                {
                    return false;
                }

                m_Running.Clear();

                var playerEntity = localPlayers[0];

                // The wallet, read from the same entity. A player prefab without a tank
                // keeps the infinity it started with rather than reporting no air, because
                // "cannot be read" and "empty" are not the same thing.
                if (m_EntityManager.HasComponent<PlayerOxygen>(playerEntity))
                {
                    m_OxygenSeconds = m_EntityManager.GetComponentData<PlayerOxygen>(playerEntity).Seconds;
                }

                // A player ghost baked before this buffer existed simply has nothing
                // running, which is the honest thing to draw rather than an error.
                if (!m_EntityManager.HasBuffer<ActiveUpgradeStatus>(playerEntity))
                {
                    return true;
                }

                var statuses = m_EntityManager.GetBuffer<ActiveUpgradeStatus>(playerEntity, true);
                for (int i = 0; i < statuses.Length; i++)
                {
                    m_Running[statuses[i].UpgradeId] = statuses[i].RemainingSeconds;
                }

                return true;
            }
            finally
            {
                localPlayers.Dispose();
            }
        }

        /// <summary>
        /// Finds the client world and the query that returns this client's own player.
        /// </summary>
        private void InitializeEcs()
        {
            // Cleared through the world rather than through the EntityManager, because an
            // EntityManager is a struct and has no empty value to fall back to. The world
            // is the thing that is really there or not, so it is the thing that is asked.
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

            if (m_ClientWorld != null)
            {
                // GhostOwnerIsLocal is what narrows this from every player in the match to
                // the one this client is playing.
                m_LocalPlayerQuery = m_EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PredictedPlayerGhost>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>());
            }
        }

        /// <summary>
        /// Puts one cell into its running or its resting look.
        /// </summary>
        private void DrawCell(SlotView view, bool isRunning, bool canAfford, float remainingSeconds)
        {
            if (!isRunning)
            {
                // Out of reach: the price goes red and the whole cell fades further back,
                // so the player can tell at a glance that pressing the key is pointless
                // rather than pressing it and wondering why nothing happened.
                float iconAlpha = canAfford ? 0.35f : 0.15f;

                view.Cell.style.backgroundColor = k_IdleBackground;
                SetBorder(view.Cell, canAfford ? k_IdleBorder : k_UnaffordableBorder, 1f);
                view.Fill.style.height = Length.Percent(0f);
                view.Icon.style.unityBackgroundImageTintColor = new StyleColor(Fade(view.Slot.Tint, iconAlpha));
                view.Icon.style.backgroundColor =
                    view.Slot.Icon != null ? Color.clear : Fade(view.Slot.Tint, iconAlpha);
                view.Price.style.color = canAfford ? new Color(1f, 1f, 1f, 0.7f) : k_UnaffordablePrice;
                return;
            }

            // Lit: the cell takes the upgrade's own colour, so which one is running can be
            // read from the corner of the eye without looking away from the fight.
            view.Cell.style.backgroundColor = new Color(view.Slot.Tint.r, view.Slot.Tint.g, view.Slot.Tint.b, 0.22f);
            SetBorder(view.Cell, view.Slot.Tint, 2f);
            view.Icon.style.unityBackgroundImageTintColor = new StyleColor(view.Slot.Tint);
            view.Icon.style.backgroundColor = view.Slot.Icon != null ? Color.clear : view.Slot.Tint;
            view.Price.style.color = Color.white;

            // Guarded against a zero duration, which the asset forbids but a broken one
            // could still carry - and dividing by it would put the bar at infinity.
            float duration = view.Slot.Data.Duration;
            float fraction = duration > 0f ? Mathf.Clamp01(remainingSeconds / duration) : 0f;

            view.Fill.style.height = Length.Percent(fraction * 100f);
            view.Fill.style.backgroundColor = new Color(view.Slot.Tint.r, view.Slot.Tint.g, view.Slot.Tint.b, 0.45f);
        }

        /// <summary>
        /// The same colour at a different strength. A cell that is off is faded, and one
        /// that cannot be paid for is fainter still.
        /// </summary>
        private static Color Fade(Color colour, float alpha)
        {
            return new Color(colour.r, colour.g, colour.b, alpha);
        }

        /// <summary>
        /// UI Toolkit has a separate property per edge, so setting a border is four lines
        /// twice over. Kept here rather than repeated at every call site.
        /// </summary>
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

        /// <summary>
        /// The same again for the four corner radii.
        /// </summary>
        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
