using System.Collections.Generic;

namespace StandingStonesOfValheim
{
    // What one character knows of the stones, saved with the character (Player.m_customData), so it goes
    // with them from world to world like their skills: the chosen stone, the stones they have found and
    // those bound to their rune altar, Hel's mark and when they last used a daily power.
    static class StoneData
    {
        const string ActiveKey = "SSoV_Active";
        const string FoundKey = "SSoV_Found";
        const string BoundKey = "SSoV_Bound";
        const string MarkKey = "SSoV_HelMarkUntil";
        const string DailyPrefix = "SSoV_Daily_";

        static string Get(Player player, string key)
        {
            string value;
            return player != null && player.m_customData.TryGetValue(key, out value) ? value : "";
        }

        static void Set(Player player, string key, string value)
        {
            if (player == null) return;
            if (string.IsNullOrEmpty(value)) player.m_customData.Remove(key);
            else player.m_customData[key] = value;
        }

        internal static Stone Active(Player player)
        {
            return Stone.Find(Get(player, ActiveKey));
        }

        internal static bool IsActive(Player player, string id)
        {
            return Get(player, ActiveKey) == id;
        }

        internal static void SetActive(Player player, Stone stone)
        {
            Set(player, ActiveKey, stone != null ? stone.Id : "");
        }

        static List<string> List(Player player, string key)
        {
            List<string> ids = new List<string>();
            foreach (string id in Get(player, key).Split(','))
                if (Stone.Find(id) != null && !ids.Contains(id)) ids.Add(id);
            return ids;
        }

        internal static List<string> Found(Player player) { return List(player, FoundKey); }
        internal static List<string> Bound(Player player) { return List(player, BoundKey); }
        internal static bool HasFound(Player player, string id) { return Found(player).Contains(id); }
        internal static bool HasBound(Player player, string id) { return Bound(player).Contains(id); }

        // true the first time.
        internal static bool AddFound(Player player, string id)
        {
            List<string> ids = Found(player);
            if (ids.Contains(id)) return false;
            ids.Add(id);
            Set(player, FoundKey, string.Join(",", ids.ToArray()));
            return true;
        }

        internal static void AddBound(Player player, string id)
        {
            List<string> ids = Bound(player);
            if (ids.Contains(id)) return;
            ids.Add(id);
            Set(player, BoundKey, string.Join(",", ids.ToArray()));
        }

        // Hel's mark lasts until this world time (seconds); 0 or past: no mark.
        internal static double MarkUntil(Player player)
        {
            double until;
            return double.TryParse(Get(player, MarkKey), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out until) ? until : 0;
        }

        internal static void SetMarkUntil(Player player, double until)
        {
            Set(player, MarkKey, until > 0 ? until.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "");
        }

        // The in-game day a daily power was last used on, -1 for never.
        internal static int LastDaily(Player player, string id)
        {
            int day;
            return int.TryParse(Get(player, DailyPrefix + id), out day) ? day : -1;
        }

        internal static void SetLastDaily(Player player, string id, int day)
        {
            Set(player, DailyPrefix + id, day.ToString());
        }

        internal static void Reset(Player player)
        {
            foreach (string key in new[] { ActiveKey, FoundKey, BoundKey, MarkKey }) Set(player, key, "");
            foreach (Stone stone in Stone.All) Set(player, DailyPrefix + stone.Id, "");
        }
    }
}
