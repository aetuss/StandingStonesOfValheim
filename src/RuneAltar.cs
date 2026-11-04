using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace StandingStonesOfValheim
{
    // The rune altar: a cheap stone piece (Stone 20, Wood 10) that can be built once you have touched any
    // standing stone. Each stone you have found can be bound to it once, with an offering from that stone's
    // land; then you can choose any bound stone at the altar instead of travelling to it.
    static class RuneAltar
    {
        internal const string PrefabName = "SSoV_RuneAltar";
        internal const string Token = "$ssov_altar";

        static readonly string[] Bases = { "Vegvisir_Eikthyr", "Vegvisir_GDKing", "RuneStone_Meadows" };
        const float Height = 1.7f;

        // The altar is a small copy of the stone pillars' own vegvisir, its runes glowing pale blue: the
        // game's prefab cloned whole (what renders as a stone renders as the altar too), its own vegvisir
        // script taken off, made a building piece.
        internal static void Register()
        {
            GameObject go = null;
            string from = "";
            foreach (string name in Bases)
                if (PrefabManager.Instance.GetPrefab(name) != null) { go = PrefabManager.Instance.CreateClonedPrefab(PrefabName, name); from = name; break; }
            if (go == null)
            {
                Plugin.Log.LogWarning("No vegvisir or runestone to make the rune altar from; the altar is left out.");
                return;
            }
            for (int pass = 0; pass < 4; pass++)
            {
                bool removed = false;
                foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(script is ZNetView)) { Object.DestroyImmediate(script); removed = true; }
                if (!removed) break;
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            ZNetView view = go.GetComponent<ZNetView>();
            if (view == null) view = go.AddComponent<ZNetView>();
            view.m_persistent = true;

            Bounds b = Bounds(go, go.transform);
            if (b.size.y > 0.01f) go.transform.localScale = Vector3.one * (Height / b.size.y);
            // The model's bottom middle on the piece's own point (the vegvisir's model may sit off its origin).
            foreach (Transform child in go.transform)
                child.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = Bounds(go, go.transform);
            // The game's own layers stay as they are; a box on the piece layer lets the hammer and the
            // building system find it.
            GameObject box = new GameObject("SSoV_Hitbox");
            box.layer = LayerMask.NameToLayer("piece");
            box.transform.SetParent(go.transform, false);
            box.transform.localPosition = b.center;
            box.AddComponent<BoxCollider>().size = b.size;
            StoneBuilder.Tint(go, new Color(0.65f, 0.8f, 1f));
            Plugin.Debug("Rune altar from " + from + ": size " + (b.size * (Height / Mathf.Max(b.size.y, 0.01f))).ToString("F2") +
                ", renderers " + go.GetComponentsInChildren<Renderer>(true).Length);

            Piece piece = go.AddComponent<Piece>();
            piece.m_name = Token;
            piece.m_description = Token + "_desc";
            piece.m_icon = Runes.Icon(Rune.Ansuz, new Color(0.75f, 0.85f, 1f), "Altar");
            WearNTear wear = go.AddComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Stone;
            wear.m_health = 1500f;
            wear.m_noRoofWear = true;
            wear.m_noSupportWear = true;
            CopyEffects(piece, wear);
            go.AddComponent<RuneAltarPiece>();

            PieceConfig config = new PieceConfig();
            config.Name = Token;
            config.Description = Token + "_desc";
            config.PieceTable = "Hammer";
            config.Category = "Misc";
            config.Icon = piece.m_icon;
            config.Requirements = new[] { new RequirementConfig("Stone", 20, 0, true), new RequirementConfig("Wood", 10, 0, true) };
            PieceManager.Instance.AddPiece(new CustomPiece(go, false, config));
            Plugin.Debug("Registered the rune altar");
        }

        // Bounds of the visible meshes in space's coordinates (switched-off parts left out).
        static Bounds Bounds(GameObject go, Transform space)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
        // The sounds and dust of a vanilla stone piece when it is built, hit and broken.
        static void CopyEffects(Piece piece, WearNTear wear)
        {
            GameObject wall = PrefabManager.Instance.GetPrefab("stone_wall_1x1");
            if (wall == null) return;
            Piece wallPiece = wall.GetComponent<Piece>();
            WearNTear wallWear = wall.GetComponent<WearNTear>();
            if (wallPiece != null) piece.m_placeEffect = wallPiece.m_placeEffect;
            if (wallWear != null)
            {
                wear.m_destroyedEffect = wallWear.m_destroyedEffect;
                wear.m_hitEffect = wallWear.m_hitEffect;
            }
        }

        // The altar shows in the hammer once the character has touched a stone. (Once it's there, the game
        // remembers it; it can then always be built.)
        [HarmonyPatch(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        static class UnlockPatch
        {
            static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
            {
                if (__result && mode == Player.RequirementMode.IsKnown && piece != null && piece.m_name == Token && StoneData.Found(__instance).Count == 0) __result = false;
            }
        }
    }

    // Using the altar opens the list of stones you have found.
    class RuneAltarPiece : MonoBehaviour, Hoverable, Interactable
    {
        public string GetHoverName()
        {
            return Localization.instance.Localize(RuneAltar.Token);
        }

        public string GetHoverText()
        {
            return Localization.instance.Localize(RuneAltar.Token + "\n[<color=yellow><b>$KEY_Use</b></color>] $ssov_altar_open");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true, false)) return true;
            AltarPanel.Open(player, transform.position);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public float GetHoverOffset()
        {
            return 0f;
        }
    }

    // The altar's window: one row per stone the character has found, with its offering and a button to
    // bind it, or to choose it once it is bound. Stones not found yet never show.
    static class AltarPanel
    {
        static GameObject panel;
        static Vector3 altarAt;
        static float openedAt;
        static string status = ""; // what the last button did (or why it couldn't), shown above Close
        static Color statusColor = Color.white;
        const float RowHeight = 42f;
        const float Width = 900f;

        internal static bool IsOpen { get { return panel != null; } }

        internal static void Open(Player player, Vector3 at)
        {
            altarAt = at;
            openedAt = Time.time;
            status = "";
            Build(player);
        }

        internal static void Close()
        {
            if (panel == null) return;
            Object.Destroy(panel);
            panel = null;
            GUIManager.BlockInput(false);
        }

        internal static void Tick()
        {
            if (panel == null) return;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, altarAt) > 6f || Input.GetKeyDown(KeyCode.Escape) ||
                (Time.time - openedAt > 0.3f && ZInput.GetButtonDown("Use")))
                Close();
        }

        static void Build(Player player)
        {
            bool wasOpen = panel != null;
            if (wasOpen) Object.Destroy(panel);
            GUIManager gui = GUIManager.Instance;
            if (gui == null || GUIManager.CustomGUIFront == null) return;

            List<Stone> found = new List<Stone>();
            foreach (Stone stone in Stone.All)
                if (StoneData.HasFound(player, stone.Id)) found.Add(stone);
            float height = 215f + Mathf.Max(1, found.Count) * RowHeight;
            Vector2 middle = new Vector2(0.5f, 0.5f);
            panel = gui.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, Vector2.zero, Width, height, false);

            Text(Localization.instance.Localize(RuneAltar.Token), 0f, -38f, Width - 60f, 30, gui.AveriaSerifBold, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.45f));
            Text(Texts.Get("ssov_altar_help"), 0f, -82f, Width - 80f, 16, gui.AveriaSerif, TextAnchor.MiddleCenter, Color.white);

            if (found.Count == 0) Text(Texts.Get("ssov_altar_empty"), 0f, -130f, Width - 80f, 18, gui.AveriaSerif, TextAnchor.MiddleCenter, Color.white);
            Stone active = StoneData.Active(player);
            for (int i = 0; i < found.Count; i++)
            {
                Stone stone = found[i];
                float y = -130f - i * RowHeight;
                bool bound = StoneData.HasBound(player, stone.Id);
                Icon(stone, -Width / 2f + 45f, y);
                Text(Localization.instance.Localize(stone.NameToken), -Width / 2f + 75f + 130f, y, 260f, 18, gui.AveriaSerifBold, TextAnchor.MiddleLeft,
                    Color.Lerp(stone.Glow, Color.white, 0.55f));
                string info = !bound ? Texts.Get("ssov_altar_offering", OfferingText(stone)) : stone == active ? Texts.Get("ssov_altar_chosen") : "";
                Text(info, 120f, y, 400f, 15, gui.AveriaSerif, TextAnchor.MiddleLeft, Color.white);
                if (stone == active && bound) continue;
                Stone target = stone;
                Button(Texts.Get(bound ? "ssov_altar_choose" : "ssov_altar_bind"), Width / 2f - 90f, y, () =>
                {
                    Player p = Player.m_localPlayer;
                    if (p == null) return;
                    if (StoneData.HasBound(p, target.Id))
                    {
                        StandingStone.Choose(p, target);
                        Tell(Texts.Get("ssov_chosen", target.NameToken), Color.Lerp(target.Glow, Color.white, 0.55f));
                    }
                    else Bind(p, target);
                    if (panel != null) Build(p);
                });
            }
            // The game's own messages would show behind this window, so they show here instead.
            if (status.Length > 0) Text(status, 0f, -height + 88f, Width - 80f, 17, gui.AveriaSerifBold, TextAnchor.MiddleCenter, statusColor);
            Button(Texts.Get("ssov_altar_close"), 0f, -height + 40f, Close);
            if (!wasOpen) GUIManager.BlockInput(true);
        }

        static void Text(string text, float x, float y, float width, int size, Font font, TextAnchor anchor, Color color)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            GameObject go = GUIManager.Instance.CreateText(text, panel.transform, top, top, new Vector2(x, y), font, size, color, true, Color.black, width, RowHeight, false);
            Text label = go.GetComponent<Text>();
            if (label != null) label.alignment = anchor;
        }

        static void Icon(Stone stone, float x, float y)
        {
            GameObject go = new GameObject("icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(34f, 34f);
            go.GetComponent<Image>().sprite = Runes.Icon(stone);
        }

        static void Button(string text, float x, float y, UnityEngine.Events.UnityAction onClick)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            GameObject go = GUIManager.Instance.CreateButton(text, panel.transform, top, top, new Vector2(x, y), 140f, 34f);
            Button button = go.GetComponent<Button>();
            // A clicked button would stay "selected" and lit; let go of it.
            button.onClick.AddListener(() => { if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null); });
            button.onClick.AddListener(onClick);
        }

        internal static string OfferingText(Stone stone)
        {
            List<string> parts = new List<string>();
            foreach (Offering offering in stone.Offerings)
            {
                ItemDrop item = ItemOf(offering);
                parts.Add(Texts.Amount(offering.Amount, item != null ? item.m_itemData.m_shared.m_name : offering.Item));
            }
            return string.Join(", ", parts.ToArray());
        }

        static ItemDrop ItemOf(Offering offering)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(offering.Item) : null;
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }

        // Takes the offering from the inventory (none needed in no-cost mode) and binds the stone.
        static void Bind(Player player, Stone stone)
        {
            Inventory inventory = player.GetInventory();
            bool free = player.NoCostCheat();
            foreach (Offering offering in stone.Offerings)
            {
                ItemDrop item = ItemOf(offering);
                if (item == null || free) continue;
                if (inventory.CountItems(item.m_itemData.m_shared.m_name, -1, false) < offering.Amount)
                {
                    Tell(Texts.Get("ssov_altar_missing", OfferingText(stone)), new Color(1f, 0.55f, 0.45f));
                    return;
                }
            }
            if (!free)
                foreach (Offering offering in stone.Offerings)
                {
                    ItemDrop item = ItemOf(offering);
                    if (item != null) inventory.RemoveItem(item.m_itemData.m_shared.m_name, offering.Amount, -1, false);
                }
            StoneData.AddBound(player, stone.Id);
            Tell(Texts.Get("ssov_altar_bound", stone.NameToken), Color.Lerp(stone.Glow, Color.white, 0.55f));
            StoneEvents.PlayChooseEffect(player);
        }

        static void Tell(string text, Color color)
        {
            status = text;
            statusColor = color;
        }
    }
}
