using UnityEngine;

namespace StandingStonesOfValheim
{
    // The pillar of a standing stone. Using it (E) reads it like a runestone: the god's lore and what the
    // stone does. Shift+E chooses it. The first touch marks the stone as found for this character.
    // Nothing about a stone is stored in the world; it's all the player's (StoneData).
    class StandingStone : MonoBehaviour, Hoverable, Interactable
    {
        public string m_stoneId;

        Stone Stone { get { return Stone.Find(m_stoneId); } }

        public string GetHoverName()
        {
            Stone stone = Stone;
            return stone != null ? Localization.instance.Localize(stone.NameToken) : "";
        }

        public string GetHoverText()
        {
            Stone stone = Stone;
            Player player = Player.m_localPlayer;
            if (stone == null || player == null) return "";
            string text = stone.NameToken + "\n[<color=yellow><b>$KEY_Use</b></color>] $ssov_read";
            if (!stone.Ready) text += "\n$ssov_not_ready";
            else if (StoneData.IsActive(player, stone.Id)) text += "\n<color=#9fd49f>$ssov_is_chosen</color>";
            else text += "\n[<color=yellow><b>" + (ZInput.IsGamepadActive() ? "$KEY_AltKeys + $KEY_Use" : "$KEY_AltPlace + $KEY_Use") + "</b></color>] $ssov_choose";
            return Localization.instance.Localize(text);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            Player player = user as Player;
            Stone stone = Stone;
            if (player == null || player != Player.m_localPlayer || stone == null) return false;
            bool first = StoneData.AddFound(player, stone.Id);
            if (alt) Choose(player, stone);
            else Read(stone);
            if (first) StoneEvents.FoundFirstTime(player, stone, transform.position);
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

        // Like reading a runestone: the god's lore, then what the stone does.
        static void Read(Stone stone)
        {
            string kind = Texts.Get("ssov_kind_" + stone.Kind.ToString().ToLowerInvariant());
            string color = ColorUtility.ToHtmlStringRGB(Color.Lerp(stone.Glow, Color.white, 0.25f));
            string text = Texts.Get(stone.Key + "_lore") + "\n\n<color=#" + color + ">" + kind + "</color>\n" + stone.EffectText();
            if (!stone.Ready) text += "\n\n" + Texts.Get("ssov_not_ready");
            TextViewer.instance.ShowText(TextViewer.Style.Rune, Localization.instance.Localize(stone.NameToken), text, true);
        }

        internal static void Choose(Player player, Stone stone)
        {
            if (!stone.Ready)
            {
                player.Message(MessageHud.MessageType.Center, "$ssov_not_ready", 0, null);
                return;
            }
            if (StoneData.IsActive(player, stone.Id))
            {
                player.Message(MessageHud.MessageType.Center, Texts.Get("ssov_already_chosen", stone.NameToken), 0, null);
                return;
            }
            StoneData.SetActive(player, stone);
            StoneEffects.Apply(player);
            player.Message(MessageHud.MessageType.Center, Texts.Get("ssov_chosen", stone.NameToken), 0, Runes.Icon(stone));
            StoneEvents.PlayChooseEffect(player);
        }
    }

    static class StoneEvents
    {
        static readonly string[] ChooseEffects = { "fx_GP_Activation", "sfx_runestone_activate" };

        internal static void PlayChooseEffect(Player player)
        {
            foreach (string name in ChooseEffects)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                if (prefab == null) continue;
                Object.Instantiate(prefab, player.transform.position, Quaternion.identity);
                return;
            }
        }

        // The first time a character touches one of the god's stones.
        internal static void FoundFirstTime(Player player, Stone stone, Vector3 at)
        {
            player.Message(MessageHud.MessageType.TopLeft, Texts.Get("ssov_found", stone.NameToken), 0, Runes.Icon(stone));
            Plugin.Debug(player.GetPlayerName() + " found " + stone.Id + " at " + at.ToString("F0"));
        }
    }
}
