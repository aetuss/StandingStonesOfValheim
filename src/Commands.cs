using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // ssov_info: how many of each stone this world has, and what this character has found and chosen.
    // ssov_goto [stone] (cheat): teleport to the closest stone (of that god, or one not found yet).
    // ssov_reset / ssov_findall (cheats, for testing): forget or find every stone for this character.
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    static class Commands
    {
        static void Postfix()
        {
            new Terminal.ConsoleCommand("ssov_info",
                "- StandingStonesOfValheim: how many of each standing stone this world has, and which ones you have found",
                args =>
                {
                    Player player = Player.m_localPlayer;
                    if (player != null)
                    {
                        Stone active = StoneData.Active(player);
                        args.Context.AddString("Chosen: " + (active != null ? active.Id : "none") + ". Found: " + Join(StoneData.Found(player)) + ". Bound to the altar: " + Join(StoneData.Bound(player)) + ".");
                    }
                    if (!OnServer(args.Context)) return;
                    Vector3 from = player != null ? player.transform.position : Vector3.zero;
                    List<ZoneSystem.LocationInstance> stones = Instances(null);
                    if (stones.Count == 0)
                    {
                        args.Context.AddString("This world has no standing stones. An existing world only gets them after `genloc` (devcommands), and only where nobody has been yet.");
                        return;
                    }
                    foreach (Stone stone in Stone.All)
                    {
                        List<ZoneSystem.LocationInstance> mine = Instances(stone);
                        if (mine.Count == 0) continue;
                        mine.Sort((a, b) => Distance(a, from).CompareTo(Distance(b, from)));
                        args.Context.AddString(stone.Id + ": " + mine.Count + " in the world, closest " + Distance(mine[0], from).ToString("0", CultureInfo.InvariantCulture) + " m away");
                    }
                    args.Context.AddString("Teleport to one: ssov_goto [stone] (needs devcommands)");
                });

            new Terminal.ConsoleCommand("ssov_goto",
                "[stone] - StandingStonesOfValheim: teleport to the closest standing stone of that god, or without a name to the closest one you haven't found",
                args =>
                {
                    Player player = Player.m_localPlayer;
                    if (!OnServer(args.Context) || player == null) return;
                    Stone wanted = null;
                    if (args.Length > 1)
                    {
                        foreach (Stone stone in Stone.All)
                            if (string.Equals(stone.Id, args[1], System.StringComparison.OrdinalIgnoreCase)) wanted = stone;
                        if (wanted == null)
                        {
                            args.Context.AddString("Unknown stone " + args[1] + ".");
                            return;
                        }
                    }
                    Vector3 from = player.transform.position;
                    List<string> found = StoneData.Found(player);
                    bool any = false;
                    ZoneSystem.LocationInstance best = new ZoneSystem.LocationInstance();
                    foreach (ZoneSystem.LocationInstance instance in Instances(wanted))
                    {
                        Stone stone = Stone.ByLocation(instance.m_location.m_prefabName);
                        if (wanted == null && found.Contains(stone.Id)) continue;
                        if (!any || Distance(instance, from) < Distance(best, from)) best = instance;
                        any = true;
                    }
                    if (!any)
                    {
                        args.Context.AddString(wanted == null ? "You have found every stone this world has." : "This world has no " + wanted.Id + " stone.");
                        return;
                    }
                    Vector3 p = best.m_position;
                    player.TeleportTo(new Vector3(p.x + 6f, p.y + 1f, p.z + 6f), player.transform.rotation, true);
                    args.Context.AddString("Teleporting " + Distance(best, from).ToString("0", CultureInfo.InvariantCulture) + " m to " + best.m_location.m_prefabName + ".");
                }, true, optionsFetcher: () =>
                {
                    List<string> names = new List<string>();
                    foreach (Stone stone in Stone.Placed()) names.Add(stone.Id);
                    return names;
                });

            new Terminal.ConsoleCommand("ssov_reset",
                "- StandingStonesOfValheim: this character forgets every standing stone (for testing)",
                args =>
                {
                    if (Player.m_localPlayer == null) return;
                    StoneData.Reset(Player.m_localPlayer);
                    StoneEffects.Apply(Player.m_localPlayer);
                    args.Context.AddString("Forgot every standing stone.");
                }, true);

            new Terminal.ConsoleCommand("ssov_findall",
                "- StandingStonesOfValheim: this character has found every standing stone (for testing)",
                args =>
                {
                    if (Player.m_localPlayer == null) return;
                    foreach (Stone stone in Stone.All) StoneData.AddFound(Player.m_localPlayer, stone.Id);
                    args.Context.AddString("Found every standing stone.");
                }, true);
        }

        static string Join(List<string> ids)
        {
            return ids.Count == 0 ? "none" : string.Join(", ", ids.ToArray());
        }

        static bool OnServer(Terminal context)
        {
            if (ZoneSystem.instance != null && ZNet.instance != null && ZNet.instance.IsServer()) return true;
            context.AddString("Where the stones are is only known on the server (single player or the host).");
            return false;
        }

        // All standing stone location instances, or only those of one stone.
        internal static List<ZoneSystem.LocationInstance> Instances(Stone only)
        {
            List<ZoneSystem.LocationInstance> result = new List<ZoneSystem.LocationInstance>();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null) continue;
                Stone stone = Stone.ByLocation(instance.m_location.m_prefabName);
                if (stone != null && (only == null || only == stone)) result.Add(instance);
            }
            return result;
        }

        internal static float Distance(ZoneSystem.LocationInstance instance, Vector3 from)
        {
            Vector3 p = instance.m_position;
            return new Vector2(p.x - from.x, p.z - from.z).magnitude;
        }
    }
}
