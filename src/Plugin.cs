using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

// Thunderstore asks mods made with AI to say so here and in the README.
[assembly: System.Reflection.AssemblyMetadata("AI_Assisted_Creation", "This assembly was fully created with the assistance of Generative AI: the code was written by Claude (Anthropic) through Claude Code.")]
[assembly: System.Reflection.AssemblyMetadata("AI_Model_Vendor", "Anthropic")]

namespace StandingStonesOfValheim
{
    [BepInPlugin(Guid, ModName, Version)]
    [BepInDependency("com.jotunn.jotunn")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "StandingStonesOfValheim";
        public const string ModName = "StandingStonesOfValheim";
        public const string Version = "1.0.0";

        internal static ConfigEntry<int> CopiesPerStone;
        internal static ConfigEntry<float> StoneSpacing;
        internal static ConfigEntry<KeyboardShortcut> DailyKey;
        internal static ConfigEntry<bool> DebugLog;

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static Plugin Instance;

        void Awake()
        {
            Log = Logger;
            Instance = this;

            CopiesPerStone = Config.Bind("1 - World generation", "CopiesPerStone", 3,
                new ConfigDescription("How many times the game tries to place each stone in its land. A copy that finds no good spot is left out, so a world usually has 2-3 of each. Only used when locations are generated (new world, or genloc for the parts of an old world nobody has been to).",
                    new AcceptableValueRange<int>(1, 10), Admin()));
            StoneSpacing = Config.Bind("1 - World generation", "StoneSpacing", 400f,
                new ConfigDescription("The least distance in metres between any two standing stones.",
                    new AcceptableValueRange<float>(100f, 3000f), Admin()));
            DailyKey = Config.Bind("2 - Keys", "DailyPowerKey", new KeyboardShortcut(KeyCode.H),
                "The key that calls your stone's daily power (Freyja's blessing). Ignored while Alt is held, so Alt+H stays free for other mods.");
            DebugLog = Config.Bind("9 - Debug", "DebugLog", false,
                "Writes how the stones were built and where they are to the BepInEx log. Useful when reporting a problem.");
            Stone.BindConfig(Config);

            Texts.LoadOwn();
            gameObject.AddComponent<StoneUpdater>();
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            ZoneManager.OnVanillaLocationsAvailable += OnVanillaLocations;

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        }

        // Values that change the game for everyone come from the server (Jötunn syncs them to the players).
        internal static ConfigurationManagerAttributes Admin()
        {
            return new ConfigurationManagerAttributes { IsAdminOnly = true };
        }

        void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            StoneEffects.Register();
            RuneAltar.Register();
        }

        void OnVanillaLocations()
        {
            ZoneManager.OnVanillaLocationsAvailable -= OnVanillaLocations;
            StoneBuilder.RegisterLocations();
        }

        internal static void Debug(string message)
        {
            if (DebugLog.Value) Log.LogInfo(message);
        }
    }
}
