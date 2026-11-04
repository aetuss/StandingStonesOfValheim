using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // What a stone does, shown by the colour of its runes: path stones speed up a group of skills (blue),
    // always-on stones just help (green), cost stones help more but take something (red), daily stones
    // give a power once a day (gold).
    enum StoneKind { Path, Always, Cost, Daily }

    // A number in a stone's effect, editable in the config ("Stone - Tyr" / "SkillGain" ...). Effect texts
    // show them as {0}, {1}... in this order.
    class StoneValue
    {
        internal readonly string Name;
        internal readonly float Default;
        internal readonly float Max;
        internal readonly string Description;
        internal ConfigEntry<float> Entry;

        internal StoneValue(string name, float value, float max, string description)
        {
            Name = name;
            Default = value;
            Max = max;
            Description = description;
        }
    }

    // One thing to bring to the rune altar to bind a stone there: a vanilla item and how many.
    class Offering
    {
        internal readonly string Item;
        internal readonly int Amount;
        internal Offering(string item, int amount) { Item = item; Amount = amount; }
    }

    // The sixteen standing stones, each a Norse god's, placed in the land of the game that fits it.
    class Stone
    {
        internal readonly string Id;            // also the location name: SSoV_<Id>
        internal readonly StoneKind Kind;
        internal readonly Heightmap.Biome Biome;
        internal readonly bool Coast;           // Njord's: on any shore, a little above the sea
        internal readonly string Land;          // the text key of its land, shared by the stones of one land
        internal readonly Rune Rune;
        internal readonly Skills.SkillType[] Skills;
        internal readonly StoneValue[] Values;
        internal readonly Offering[] Offerings;
        internal readonly bool Ready;           // its effect is built (the stones come in phases while testing)

        internal string Key { get { return "ssov_" + Id.ToLowerInvariant(); } }
        internal string NameToken { get { return "$" + Key; } }
        internal string LocationName { get { return "SSoV_" + Id; } }
        internal string EffectName { get { return "SSoV_SE_" + Id; } }
        internal Color Glow { get { return KindColor(Kind); } }

        Stone(string id, StoneKind kind, Heightmap.Biome biome, string land, Rune rune, bool ready,
            Skills.SkillType[] skills, StoneValue[] values, Offering[] offerings)
        {
            Id = id;
            Kind = kind;
            Biome = biome;
            Land = land;
            Coast = land == "ssov_land_coast";
            Rune = rune;
            Ready = ready;
            Skills = skills ?? new Skills.SkillType[0];
            Values = values ?? new StoneValue[0];
            Offerings = offerings;
        }

        internal static Color KindColor(StoneKind kind)
        {
            switch (kind)
            {
                case StoneKind.Path: return new Color(0.35f, 0.65f, 1f);
                case StoneKind.Always: return new Color(0.4f, 1f, 0.45f);
                case StoneKind.Cost: return new Color(1f, 0.28f, 0.22f);
                default: return new Color(1f, 0.78f, 0.3f);
            }
        }

        internal float V(int index)
        {
            StoneValue value = Values[index];
            return value.Entry != null ? value.Entry.Value : value.Default;
        }

        // V as a fraction: 25 (%) -> 0.25.
        internal float P(int index)
        {
            return V(index) / 100f;
        }

        // The effect, with this world's numbers filled in.
        internal string EffectText()
        {
            string[] args = new string[Values.Length];
            for (int i = 0; i < Values.Length; i++) args[i] = Mathf.RoundToInt(V(i)).ToString();
            return Texts.Get(Key + "_effect", args);
        }

        internal static Stone Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (Stone stone in All)
                if (stone.Id == id) return stone;
            return null;
        }

        internal static Stone ByLocation(string locationName)
        {
            foreach (Stone stone in All)
                if (stone.LocationName == locationName) return stone;
            return null;
        }

        internal static void BindConfig(ConfigFile config)
        {
            foreach (Stone stone in All)
                foreach (StoneValue value in stone.Values)
                    value.Entry = config.Bind("Stone - " + stone.Id, value.Name, value.Default,
                        new ConfigDescription(value.Description, new AcceptableValueRange<float>(0f, value.Max), Plugin.Admin()));
        }

        static StoneValue Gain()
        {
            return new StoneValue("SkillGain", 25f, 300f, "How much faster its skills rise, in percent.");
        }

        static Offering[] Gift(string item, int amount, string item2, int amount2)
        {
            return item2 == null ? new[] { new Offering(item, amount) } : new[] { new Offering(item, amount), new Offering(item2, amount2) };
        }

        const Heightmap.Biome Shore = Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest | Heightmap.Biome.Plains;

        internal static readonly Stone[] All =
        {
            // Meadows
            new Stone("Tyr", StoneKind.Path, Heightmap.Biome.Meadows, "ssov_land_meadows", Rune.Tiwaz, true,
                new[] { global::Skills.SkillType.Swords, global::Skills.SkillType.Knives, global::Skills.SkillType.Clubs, global::Skills.SkillType.Polearms,
                        global::Skills.SkillType.Spears, global::Skills.SkillType.Axes, global::Skills.SkillType.Unarmed, global::Skills.SkillType.Blocking },
                new[] { Gain() }, Gift("Flint", 10, "DeerHide", 5)),
            new Stone("Bragi", StoneKind.Always, Heightmap.Biome.Meadows, "ssov_land_meadows", Rune.Mannaz, true, null,
                new[] { new StoneValue("RestedLonger", 50f, 300f, "How much longer Rested lasts, in percent.") }, Gift("Flint", 10, "DeerHide", 5)),
            new Stone("Frigg", StoneKind.Cost, Heightmap.Biome.Meadows, "ssov_land_meadows", Rune.Othala, true, null,
                new[] { new StoneValue("FoodLonger", 50f, 300f, "How much longer food lasts, in percent."),
                        new StoneValue("RunSlower", 10f, 90f, "The cost: how much slower you run, in percent.") }, Gift("Flint", 10, "DeerHide", 5)),
            // Black Forest
            new Stone("Freyr", StoneKind.Path, Heightmap.Biome.BlackForest, "ssov_land_blackforest", Rune.Ingwaz, true,
                new[] { global::Skills.SkillType.WoodCutting, global::Skills.SkillType.Pickaxes, global::Skills.SkillType.Fishing, global::Skills.SkillType.Farming },
                new[] { Gain() }, Gift("Copper", 10, "TrollHide", 2)),
            new Stone("Ullr", StoneKind.Path, Heightmap.Biome.BlackForest, "ssov_land_blackforest", Rune.Eihwaz, true,
                new[] { global::Skills.SkillType.Bows, global::Skills.SkillType.Crossbows, global::Skills.SkillType.Sneak },
                new[] { Gain() }, Gift("Copper", 10, "TrollHide", 2)),
            // Swamp
            new Stone("Hel", StoneKind.Cost, Heightmap.Biome.Swamp, "ssov_land_swamp", Rune.Hagalaz, false, null,
                new[] { new StoneValue("LowHealthDamage", 40f, 300f, "How much more damage you deal while your health is low, in percent."),
                        new StoneValue("LowHealthBelow", 30f, 90f, "Below how much of your health (in percent) the extra damage starts."),
                        new StoneValue("MarkWeakness", 25f, 90f, "The cost: Hel's mark after you die with her stone takes this much (in percent) from your max health and stamina, until you take your things back from your grave or a day goes by.") },
                Gift("Iron", 10, "Guck", 5)),
            new Stone("Eir", StoneKind.Always, Heightmap.Biome.Swamp, "ssov_land_swamp", Rune.Berkano, true, null,
                new[] { new StoneValue("HealthRegen", 50f, 300f, "How much faster your health comes back, in percent.") }, Gift("Iron", 10, "Guck", 5)),
            // Mountain
            new Stone("Skadi", StoneKind.Always, Heightmap.Biome.Mountain, "ssov_land_mountain", Rune.Isa, false, null,
                new[] { new StoneValue("ColdLess", 50f, 100f, "How much of the cold's and freezing's penalties it takes away, in percent."),
                        new StoneValue("FrostResistant", 1f, 1f, "1: you are resistant to frost damage (and the cold can't freeze you, only chill you, as with a frost resistance mead). 0: no.") },
                Gift("Silver", 10, "WolfPelt", 3)),
            new Stone("Heimdall", StoneKind.Always, Heightmap.Biome.Mountain, "ssov_land_mountain", Rune.Algiz, false, null,
                new[] { new StoneValue("Range", 40f, 150f, "How far away (metres) an enemy coming for you shows on the minimap.") }, Gift("Silver", 10, "WolfPelt", 3)),
            // Plains
            new Stone("Thor", StoneKind.Always, Heightmap.Biome.Plains, "ssov_land_plains", Rune.Thurisaz, true, null,
                new[] { new StoneValue("CarryWeight", 100f, 1000f, "How much more you can carry.") }, Gift("BlackMetal", 10, "LoxPelt", 2)),
            new Stone("Baldr", StoneKind.Daily, Heightmap.Biome.Plains, "ssov_land_plains", Rune.Sowilo, false, null,
                new[] { new StoneValue("HealthLeft", 20f, 100f, "Once a day, a blow that would kill you leaves you with this much of your health (percent) instead."),
                        new StoneValue("UnharmedSeconds", 3f, 30f, "For how many seconds after that nothing can hurt you.") },
                Gift("BlackMetal", 10, "LoxPelt", 2)),
            // Mistlands
            new Stone("Odin", StoneKind.Path, Heightmap.Biome.Mistlands, "ssov_land_mistlands", Rune.Ansuz, true,
                new[] { global::Skills.SkillType.ElementalMagic, global::Skills.SkillType.BloodMagic },
                new[] { Gain() }, Gift("Eitr", 5, "BlackCore", 1)),
            new Stone("Freyja", StoneKind.Daily, Heightmap.Biome.Mistlands, "ssov_land_mistlands", Rune.Fehu, false, null,
                new[] { new StoneValue("Damage", 15f, 300f, "How much more damage you deal while her blessing lasts, in percent."),
                        new StoneValue("KillHeal", 10f, 100f, "How much of your max health (percent) each kill gives back while it lasts."),
                        new StoneValue("Seconds", 60f, 600f, "How long her blessing lasts.") },
                Gift("Eitr", 5, "BlackCore", 1)),
            // Ashlands
            new Stone("Loki", StoneKind.Cost, Heightmap.Biome.AshLands, "ssov_land_ashlands", Rune.Kenaz, false, null,
                new[] { new StoneValue("SneakAttack", 50f, 300f, "How much harder your sneak attacks hit, in percent."),
                        new StoneValue("Quieter", 50f, 100f, "How much quieter you are while sneaking, in percent."),
                        new StoneValue("LessHealth", 20f, 90f, "The cost: how much less max health you have, in percent.") },
                Gift("FlametalNew", 10, null, 0)), // the Ashlands' flametal ("Flametal" is the old Ancient Metal)
            // Deep North
            new Stone("Vidarr", StoneKind.Cost, Heightmap.Biome.DeepNorth, "ssov_land_deepnorth", Rune.Uruz, false, null,
                new[] { new StoneValue("MeleeDamage", 20f, 300f, "How much more damage your melee attacks deal, in percent."),
                        new StoneValue("WeakerBlock", 50f, 90f, "The cost: how much weaker your blocks are, in percent.") },
                Gift("FrostCore", 3, "Frostwood", 10)), // Frostcore, Timberwood
            // Any shore
            new Stone("Njord", StoneKind.Always, Shore, "ssov_land_coast", Rune.Laguz, false, null,
                new[] { new StoneValue("ShipSpeed", 10f, 100f, "How much faster a ship you steer sails, in percent."),
                        new StoneValue("SwimStamina", 50f, 100f, "How much less stamina swimming costs, in percent.") },
                Gift("SerpentScale", 5, null, 0)),
        };

        internal static IEnumerable<Stone> Placed()
        {
            foreach (Stone stone in All)
                if (stone.Ready) yield return stone;
        }
    }
}
