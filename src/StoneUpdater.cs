using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Runs every frame on the plugin's object, for the things a stone does over time.
    class StoneUpdater : MonoBehaviour
    {
        void Update()
        {
            Heimdall.Tick();
            DailyPower.Tick();
            AltarPanel.Tick();
        }
    }

    // Heimdall: enemies that have you as their target, near enough, show as red dots on the minimap (and
    // the big map) while they come for you.
    static class Heimdall
    {
        static readonly Dictionary<BaseAI, Minimap.PinData> pins = new Dictionary<BaseAI, Minimap.PinData>();
        static float nextScan;

        internal static void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null || !StoneEffects.LocalHas("Heimdall"))
            {
                Clear();
                return;
            }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.25f;
                Scan(player);
            }
            foreach (KeyValuePair<BaseAI, Minimap.PinData> pair in pins)
                if (pair.Key != null) pair.Value.m_pos = pair.Key.transform.position;
        }

        static void Scan(Player player)
        {
            float range = Stone.Find("Heimdall").V(0);
            HashSet<BaseAI> seen = new HashSet<BaseAI>();
            foreach (BaseAI ai in BaseAI.BaseAIInstances)
            {
                if (ai == null || ai.GetTargetCreature() != player) continue;
                Character character = ai.GetComponent<Character>();
                if (character == null || character.IsDead() || character.IsTamed()) continue;
                if (Vector3.Distance(ai.transform.position, player.transform.position) > range) continue;
                seen.Add(ai);
            }
            List<BaseAI> gone = new List<BaseAI>();
            foreach (BaseAI ai in pins.Keys)
                if (ai == null || !seen.Contains(ai)) gone.Add(ai);
            foreach (BaseAI ai in gone)
            {
                Minimap.instance.RemovePin(pins[ai]);
                pins.Remove(ai);
            }
            foreach (BaseAI ai in seen)
            {
                if (pins.ContainsKey(ai)) continue;
                Minimap.PinData pin = Minimap.instance.AddPin(ai.transform.position, Minimap.PinType.Icon3, "", false, false);
                pin.m_icon = Runes.Dot(); // before the map draws it (next frame)
                pins[ai] = pin;
            }
        }

        static void Clear()
        {
            if (pins.Count == 0) return;
            if (Minimap.instance != null)
                foreach (Minimap.PinData pin in pins.Values) Minimap.instance.RemovePin(pin);
            pins.Clear();
        }
    }

    // The daily power key (H by default, rebindable): calls the chosen stone's power if it has one to call
    // (Freyja's). Not while typing, in a menu or the inventory, and not with Alt held (other mods use
    // Alt+H).
    static class DailyPower
    {
        static readonly System.Reflection.MethodInfo takeInput = AccessTools.Method(typeof(Player), "TakeInput");

        internal static string KeyText()
        {
            return Plugin.DailyKey.Value.ToString();
        }

        internal static void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !Plugin.DailyKey.Value.IsDown()) return;
            bool altInShortcut = false;
            foreach (KeyCode modifier in Plugin.DailyKey.Value.Modifiers)
                if (modifier == KeyCode.LeftAlt || modifier == KeyCode.RightAlt || modifier == KeyCode.AltGr) altInShortcut = true;
            if (!altInShortcut && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr))) return;
            if (AltarPanel.IsOpen || player.IsDead() || !(bool)takeInput.Invoke(player, null)) return;
            Powers.CallDaily(player);
        }
    }

    // While the game looks for a spot for a stone, it asks whether a similar location is too close. A spot
    // on lava (the Ashlands) gets a yes here, so the game tries another one.
    [HarmonyPatch(typeof(ZoneSystem), "HaveLocationInRange")]
    static class LavaCheck
    {
        static void Postfix(string group, Vector3 p, bool maxGroup, ref bool __result)
        {
            if (__result || maxGroup || group != StoneBuilder.Group) return;
            if (ZoneSystem.IsLavaPreHeightmap(p, 0.6f)) __result = true;
        }
    }
}
