using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Runs every frame on the plugin's object, for the things a stone does over time.
    class StoneUpdater : MonoBehaviour
    {
        void Update()
        {
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
