using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Hel's cost. Dying with her stone leaves her mark: less max health and stamina (Powers.FoodValuePatch)
    // until you take your things back from your grave (the moment the game gives Corpse Run), or one
    // in-game day goes by. No grave (nothing on you, or a world that deletes your things): no mark. The mark
    // is the character's, so it stays when you switch stones and over a logout.
    static class HelMark
    {
        internal const string EffectName = "SSoV_SE_HelMark";
        static int Hash { get { return EffectName.GetStableHashCode(); } }

        static bool dyingWithHel;
        static bool graveMade;

        static double Now()
        {
            return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0;
        }

        internal static bool IsMarked(Player player)
        {
            return StoneData.MarkUntil(player) > Now();
        }

        internal static double SecondsLeft(Player player)
        {
            return StoneData.MarkUntil(player) - Now();
        }

        // The mark's icon is on the player while the mark lasts (and comes back after a logout).
        internal static void Refresh(Player player, bool tell)
        {
            SEMan seman = player.GetSEMan();
            bool marked = IsMarked(player);
            if (!marked && StoneData.MarkUntil(player) > 0) StoneData.SetMarkUntil(player, 0);
            if (marked && !seman.HaveStatusEffect(Hash))
            {
                seman.AddStatusEffect(Hash, false, 0, 0f, 0);
                if (tell) player.Message(MessageHud.MessageType.Center, "$ssov_hel_marked", 0, null);
            }
            else if (!marked && seman.HaveStatusEffect(Hash)) seman.RemoveStatusEffect(Hash, true);
        }

        internal static void Lift(Player player)
        {
            if (StoneData.MarkUntil(player) <= 0) return;
            StoneData.SetMarkUntil(player, 0);
            Refresh(player, false);
            player.Message(MessageHud.MessageType.Center, "$ssov_hel_lifted", 0, null);
            Plugin.Debug("Hel's mark lifted");
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        static class DeathPatch
        {
            static void Prefix(Player __instance)
            {
                dyingWithHel = __instance == Player.m_localPlayer && StoneEffects.LocalHas("Hel");
                graveMade = false;
            }

            static void Postfix(Player __instance)
            {
                if (dyingWithHel && graveMade && EnvMan.instance != null)
                {
                    StoneData.SetMarkUntil(__instance, Now() + EnvMan.instance.m_dayLengthSec);
                    Plugin.Debug("Hel's mark until " + StoneData.MarkUntil(__instance).ToString("F0"));
                }
                dyingWithHel = false;
            }
        }

        // A grave made for the dying player (only when they had something on them to leave in it).
        [HarmonyPatch(typeof(TombStone), "Setup")]
        static class GravePatch
        {
            static void Postfix(long ownerUID)
            {
                if (dyingWithHel && Player.m_localPlayer != null && ownerUID == Player.m_localPlayer.GetPlayerID()) graveMade = true;
            }
        }
    }

    // The mark's icon, with how long it has left. The time is the world's (sleeping through the night
    // counts), so it's worked out from the world clock rather than the effect's own timer.
    class SE_HelMark : StatusEffect
    {
        public override bool IsDone()
        {
            Player player = m_character as Player;
            return base.IsDone() || (player != null && !HelMark.IsMarked(player));
        }

        public override string GetIconText()
        {
            Player player = m_character as Player;
            if (player == null) return "";
            double left = HelMark.SecondsLeft(player);
            if (left <= 0) return "";
            // Numbers only (no unit letters to translate): 25:40, or 1:05:00 past an hour.
            int seconds = Mathf.CeilToInt((float)left);
            int hours = seconds / 3600, minutes = seconds / 60 % 60;
            return hours > 0 ? hours + ":" + minutes.ToString("00") + ":" + (seconds % 60).ToString("00") : minutes + ":" + (seconds % 60).ToString("00");
        }

        public override string GetTooltipString()
        {
            return Texts.Get("ssov_hel_mark_tooltip", Mathf.RoundToInt(Stone.Find("Hel").V(2)).ToString());
        }
    }
}
