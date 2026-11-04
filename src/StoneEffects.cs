using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // The chosen stone works as a status effect on the player: it shows as an icon by the others on the HUD,
    // with its effect as the tooltip, and most effects are the game's own status effect hooks. The rest are
    // patches (Powers) that ask whether the local player's stone is a given one.
    class SE_Stone : SE_Stats
    {
        internal string StoneId;

        internal Stone Stone { get { return Stone.Find(StoneId); } }

        public override string GetTooltipString()
        {
            Stone stone = Stone;
            if (stone == null) return base.GetTooltipString();
            string daily = stone.DailyText(m_character as Player);
            return stone.EffectText() + (daily.Length > 0 ? "\n" + daily : "");
        }

        // Path stones: their skills rise faster (added like the game's own skill bonuses).
        public override void ModifyRaiseSkill(Skills.SkillType skill, ref float value)
        {
            base.ModifyRaiseSkill(skill, ref value);
            Stone stone = Stone;
            if (stone != null && stone.Kind == StoneKind.Path && System.Array.IndexOf(stone.Skills, skill) >= 0) value += stone.P(0);
        }

        public override void ModifyHealthRegen(ref float regenMultiplier)
        {
            base.ModifyHealthRegen(ref regenMultiplier);
            if (StoneId == "Eir") regenMultiplier *= 1f + Stone.P(0);
        }

        public override void ModifyMaxCarryWeight(float baseLimit, ref float limit)
        {
            base.ModifyMaxCarryWeight(baseLimit, ref limit);
            if (StoneId == "Thor") limit += Stone.V(0);
        }

        // Frigg's cost: slower running (walking, swimming and sneaking are as they were).
        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            base.ModifySpeed(baseSpeed, ref speed, character, dir);
            if (StoneId == "Frigg" && character != null && character.IsRunning()) speed -= baseSpeed * Stone.P(1);
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            base.ModifyAttack(skill, ref hitData);
            Stone stone = Stone;
            if (stone == null || hitData == null) return;
            switch (StoneId)
            {
                case "Hel": // stronger while near death
                    if (m_character != null && m_character.GetHealthPercentage() < stone.P(1)) hitData.m_damage.Modify(1f + stone.P(0));
                    break;
                case "Vidarr":
                    if (Powers.IsMelee(skill)) hitData.m_damage.Modify(1f + stone.P(0));
                    break;
                case "Loki": // the weapon's sneak attack bonus, set just before this, goes up
                    if (hitData.m_backstabBonus > 1f) hitData.m_backstabBonus *= 1f + stone.P(0);
                    break;
            }
        }

        // Loki: quieter while sneaking.
        public override void ModifyNoise(float baseNoise, ref float noise)
        {
            base.ModifyNoise(baseNoise, ref noise);
            if (StoneId == "Loki" && m_character != null && m_character.IsCrouching()) noise -= baseNoise * Stone.P(1);
        }

        // Njord: swimming costs less stamina.
        public override void ModifySwimStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            base.ModifySwimStaminaUsage(baseStaminaUse, ref staminaUse);
            if (StoneId == "Njord") staminaUse -= baseStaminaUse * Stone.P(1);
        }

        static readonly List<HitData.DamageModPair> frostResistant = new List<HitData.DamageModPair>
        {
            new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant }
        };

        // Skadi: resistant to frost, like a frost resistance mead (so the cold can chill but not freeze).
        public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
        {
            base.ModifyDamageMods(ref modifiers);
            if (StoneId == "Skadi" && Stone.V(1) >= 0.5f) modifiers.Apply(frostResistant);
        }
    }

    static class StoneEffects
    {
        static readonly Dictionary<string, int> hashes = new Dictionary<string, int>();
        internal static readonly System.Reflection.FieldInfo effectTime = AccessTools.Field(typeof(StatusEffect), "m_time");

        internal static void Register()
        {
            foreach (Stone stone in Stone.All)
            {
                SE_Stone se = ScriptableObject.CreateInstance<SE_Stone>();
                se.name = stone.EffectName;
                se.StoneId = stone.Id;
                se.m_name = stone.NameToken;
                se.m_tooltip = "";
                se.m_icon = Runes.Icon(stone);
                se.m_ttl = 0f; // as long as it is chosen
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
                hashes[stone.Id] = stone.EffectName.GetStableHashCode();
            }
            Powers.RegisterEffects();
            Plugin.Debug("Registered " + hashes.Count + " stone effects");
        }

        // Puts the player's chosen stone's effect on them, taking any other stone's off.
        internal static void Apply(Player player)
        {
            if (player == null) return;
            SEMan seman = player.GetSEMan();
            Stone active = StoneData.Active(player);
            foreach (Stone stone in Stone.All)
            {
                int hash;
                if (!hashes.TryGetValue(stone.Id, out hash)) continue;
                bool wanted = stone == active;
                bool has = seman.HaveStatusEffect(hash);
                if (has && !wanted) seman.RemoveStatusEffect(hash, true);
                else if (!has && wanted) seman.AddStatusEffect(hash, false, 0, 0f, 0);
            }
            Powers.OnStoneChanged(player);
        }

        // The local player's chosen stone, if it is this one.
        internal static bool LocalHas(string id)
        {
            Player player = Player.m_localPlayer;
            int hash;
            return player != null && StoneData.IsActive(player, id) && hashes.TryGetValue(id, out hash) && player.GetSEMan().HaveStatusEffect(hash);
        }

        internal static bool Has(Character character, string id)
        {
            Player player = character as Player;
            return player != null && player == Player.m_localPlayer && LocalHas(id);
        }

        // Status effects aren't kept over a logout or a death: the stone's goes on again whenever the
        // player appears, and so does Hel's mark if it's still on them.
        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class SpawnPatch
        {
            static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                Apply(__instance);
                HelMark.Refresh(__instance, true);
            }
        }

        // Bragi: Rested lasts longer. The game works the time out from the comfort around you whenever you
        // rest; this does the same with the stone's bonus on top.
        [HarmonyPatch(typeof(SE_Rested), "UpdateTTL")]
        static class RestedPatch
        {
            static void Postfix(SE_Rested __instance)
            {
                Player player = __instance.m_character as Player;
                if (player == null || !Has(player, "Bragi")) return;
                float ttl = (__instance.m_baseTTL + (player.GetComfortLevel() - 1) * __instance.m_TTLPerComfortLevel) * (1f + Stone.Find("Bragi").P(0));
                if (ttl > __instance.GetRemaningTime())
                {
                    __instance.m_ttl = ttl;
                    effectTime.SetValue(__instance, 0f);
                }
            }
        }

        // Frigg: food lasts longer. The game takes a second off every food per second (times its food
        // rate); with Frigg the rate is lower for this player only while their food is updated.
        [HarmonyPatch(typeof(Player), "UpdateFood")]
        static class FoodPatch
        {
            static void Prefix(Player __instance, out float __state)
            {
                __state = Game.m_foodRate;
                if (Has(__instance, "Frigg")) Game.m_foodRate = __state / (1f + Stone.Find("Frigg").P(0));
            }

            static void Postfix(float __state)
            {
                Game.m_foodRate = __state;
            }
        }
    }
}
