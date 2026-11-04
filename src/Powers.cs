using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // The stone effects that aren't simple status effect numbers: daily powers (Baldr, Freyja), Hel's mark,
    // Skadi and the cold, Víðarr's weaker block, Njord's ship, and the max health / stamina costs.
    static class Powers
    {
        internal const string Freyja = "Freyja";
        internal const string Baldr = "Baldr";
        internal const string BaldrWardName = "SSoV_SE_BaldrWard";
        internal const string BlessingName = "SSoV_SE_FreyjaBlessing";
        internal const string KillRpc = "SSoV_Kill";

        internal static int BaldrWardHash { get { return BaldrWardName.GetStableHashCode(); } }
        internal static int BlessingHash { get { return BlessingName.GetStableHashCode(); } }
        static readonly int coldHash = "Cold".GetStableHashCode();
        static readonly int freezingHash = "Freezing".GetStableHashCode();
        static readonly int corpseRunHash = "CorpseRun".GetStableHashCode();

        static readonly System.Reflection.FieldInfo seCharacter = AccessTools.Field(typeof(SEMan), "m_character");

        internal static bool IsMelee(Skills.SkillType skill)
        {
            switch (skill)
            {
                case Skills.SkillType.Swords:
                case Skills.SkillType.Knives:
                case Skills.SkillType.Clubs:
                case Skills.SkillType.Polearms:
                case Skills.SkillType.Spears:
                case Skills.SkillType.Axes:
                case Skills.SkillType.Unarmed:
                    return true;
                default:
                    return false;
            }
        }

        // In-game days, as the game counts them (they turn over at dawn).
        internal static int Today()
        {
            return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
        }

        internal static bool UsedToday(Player player, string id)
        {
            return StoneData.LastDaily(player, id) >= Today();
        }

        internal static void RegisterEffects()
        {
            Stone baldr = Stone.Find(Baldr), freyja = Stone.Find(Freyja), hel = Stone.Find("Hel");
            SE_Timed ward = ScriptableObject.CreateInstance<SE_Timed>();
            ward.name = BaldrWardName;
            ward.m_name = "$ssov_baldr_ward";
            ward.TooltipKey = "ssov_baldr_ward_tooltip";
            ward.m_icon = Runes.Icon(baldr.Rune, Color.white, "BaldrWard");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(ward, false));

            SE_Blessing blessing = ScriptableObject.CreateInstance<SE_Blessing>();
            blessing.name = BlessingName;
            blessing.m_name = "$ssov_freyja_blessing";
            blessing.m_icon = Runes.Icon(freyja.Rune, new Color(1f, 0.55f, 0.7f), "FreyjaBlessing");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(blessing, false));

            SE_HelMark mark = ScriptableObject.CreateInstance<SE_HelMark>();
            mark.name = HelMark.EffectName;
            mark.m_name = "$ssov_hel_mark";
            mark.m_icon = Runes.Icon(hel.Rune, new Color(0.6f, 0.45f, 0.9f), "HelMark");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(mark, false));
        }

        // Skadi changes how the cold holds you: the game puts Cold and Freezing on again at once, softened
        // or not, so taking them off is enough.
        internal static void OnStoneChanged(Player player)
        {
            SEMan seman = player.GetSEMan();
            softened.Clear();
            if (seman.HaveStatusEffect(coldHash)) seman.RemoveStatusEffect(coldHash, true);
            if (seman.HaveStatusEffect(freezingHash)) seman.RemoveStatusEffect(freezingHash, true);
        }

        static readonly HashSet<StatusEffect> softened = new HashSet<StatusEffect>();

        // Skadi: the cold's and freezing's penalties, softened on this player's copy of the effect.
        static void Soften(StatusEffect effect)
        {
            SE_Stats stats = effect as SE_Stats;
            if (stats == null || !softened.Add(effect)) return;
            float k = Stone.Find("Skadi").P(0);
            Plugin.Debug("Skadi softens " + effect.name + ": health regen " + stats.m_healthRegenMultiplier + ", stamina regen " + stats.m_staminaRegenMultiplier +
                ", eitr regen " + stats.m_eitrRegenMultiplier + ", health per tick " + stats.m_healthPerTick + ", speed " + stats.m_speedModifier);
            stats.m_healthRegenMultiplier = Mathf.Lerp(stats.m_healthRegenMultiplier, 1f, k);
            stats.m_staminaRegenMultiplier = Mathf.Lerp(stats.m_staminaRegenMultiplier, 1f, k);
            stats.m_eitrRegenMultiplier = Mathf.Lerp(stats.m_eitrRegenMultiplier, 1f, k);
            stats.m_healthPerTick *= 1f - k;
            stats.m_speedModifier *= 1f - k;
            stats.m_staminaDrainPerSec *= 1f - k;
        }

        // Every status effect that goes on the local player passes here: Cold and Freezing for Skadi, and
        // Corpse Run, which the game gives when you take your things back from your grave: Hel lifts her mark.
        [HarmonyPatch(typeof(SEMan), "Internal_AddStatusEffect")]
        static class AddEffectPatch
        {
            static void Postfix(SEMan __instance, int nameHash, StatusEffect __result)
            {
                if (__result == null || Player.m_localPlayer == null || seCharacter.GetValue(__instance) as Player != Player.m_localPlayer) return;
                if (nameHash == corpseRunHash) HelMark.Lift(Player.m_localPlayer);
                else if ((nameHash == coldHash || nameHash == freezingHash) && StoneEffects.LocalHas("Skadi")) Soften(__result);
            }
        }

        // Max health and stamina come from food; Loki's cost and Hel's mark take their share of the total.
        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        static class FoodValuePatch
        {
            static void Postfix(Player __instance, ref float hp, ref float stamina)
            {
                if (__instance != Player.m_localPlayer) return;
                if (StoneEffects.LocalHas("Loki")) hp *= 1f - Stone.Find("Loki").P(2);
                if (HelMark.IsMarked(__instance))
                {
                    float keep = 1f - Stone.Find("Hel").P(2);
                    hp *= keep;
                    stamina *= keep;
                }
            }
        }

        // Baldr: once a day, a killing blow leaves you standing; then nothing hurts you for a moment.
        // Also, a creature killed by a player tells that player (for Freyja's healing).
        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        static class DamagePatch
        {
            static bool Prefix(Character __instance, HitData hit, out bool __state)
            {
                __state = !__instance.IsDead() && __instance.GetHealth() > 0f;
                Player player = __instance as Player;
                if (player == null || player != Player.m_localPlayer || hit == null) return true;
                if (player.GetSEMan().HaveStatusEffect(BaldrWardHash)) return false;
                return !BaldrSaves(player, hit);
            }

            static void Postfix(Character __instance, HitData hit, bool __state)
            {
                if (!__state || hit == null || __instance is Player || __instance.GetHealth() > 0f) return;
                KilledBy(hit.GetAttacker() as Player);
            }
        }

        static bool BaldrSaves(Player player, HitData hit)
        {
            if (!StoneEffects.LocalHas(Baldr) || player.InGodMode()) return false;
            float damage = hit.GetTotalDamage() * Game.m_localDamgeTakenRate;
            if (player.GetHealth() - damage > 0f || UsedToday(player, Baldr)) return false;
            Stone baldr = Stone.Find(Baldr);
            StoneData.SetLastDaily(player, Baldr, Today());
            player.SetHealth(Mathf.Max(1f, player.GetMaxHealth() * baldr.P(0)));
            StatusEffect ward = player.GetSEMan().AddStatusEffect(BaldrWardHash, true, 0, 0f, 0);
            if (ward != null) ward.m_ttl = baldr.V(1);
            player.Message(MessageHud.MessageType.Center, "$ssov_baldr_saved", 0, null);
            StoneEvents.PlayChooseEffect(player);
            Plugin.Debug("Baldr turned aside a blow of " + damage.ToString("F0"));
            return true;
        }

        // Freyja: the daily power key calls her blessing.
        internal static void CallDaily(Player player)
        {
            Stone active = StoneData.Active(player);
            if (active == null || active.Id != Freyja || !StoneEffects.LocalHas(Freyja))
            {
                player.Message(MessageHud.MessageType.Center, "$ssov_no_power", 0, null);
                return;
            }
            if (UsedToday(player, Freyja))
            {
                player.Message(MessageHud.MessageType.Center, "$ssov_freyja_used", 0, null);
                return;
            }
            StoneData.SetLastDaily(player, Freyja, Today());
            StatusEffect blessing = player.GetSEMan().AddStatusEffect(BlessingHash, true, 0, 0f, 0);
            if (blessing != null) blessing.m_ttl = active.V(2);
            player.Message(MessageHud.MessageType.Center, "$ssov_freyja_called", 0, null);
            StoneEvents.PlayChooseEffect(player);
        }

        // The creature's owner may be another player's game: the killer is told over the network.
        static void KilledBy(Player killer)
        {
            if (killer == null) return;
            if (killer == Player.m_localPlayer) HealIfBlessed();
            else if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(killer.GetOwner(), KillRpc);
        }

        static void HealIfBlessed()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !player.GetSEMan().HaveStatusEffect(BlessingHash)) return;
            player.Heal(player.GetMaxHealth() * Stone.Find(Freyja).P(1), true);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register(KillRpc, sender => HealIfBlessed());
                Hints.RegisterRpcs();
            }
        }

        // Víðarr's cost: weaker blocks. The game asks the shield for its block power while a block is
        // worked out; only that one is made weaker (the tooltip keeps the shield's own number).
        static bool weakBlock;

        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        static class BlockPatch
        {
            static void Prefix(Humanoid __instance) { weakBlock = StoneEffects.Has(__instance, "Vidarr"); }
            static void Postfix() { weakBlock = false; }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetBlockPower", new[] { typeof(float) })]
        static class BlockPowerPatch
        {
            static void Postfix(ref float __result)
            {
                if (weakBlock) __result *= 1f - Stone.Find("Vidarr").P(1);
            }
        }

        // Njord: the ship the player steers sails and rows faster.
        static bool Steering(Ship ship)
        {
            Player player = Player.m_localPlayer;
            return player != null && ship.IsOwner() && StoneEffects.LocalHas("Njord") && player.GetControlledShip() == ship;
        }

        [HarmonyPatch(typeof(Ship), "GetSailForce")]
        static class SailPatch
        {
            static void Postfix(Ship __instance, ref Vector3 __result)
            {
                if (Steering(__instance)) __result *= 1f + Stone.Find("Njord").P(0);
            }
        }

        [HarmonyPatch(typeof(Ship), "CustomFixedUpdate")]
        static class RowPatch
        {
            static void Prefix(Ship __instance, out float __state)
            {
                __state = __instance.m_backwardForce;
                if (Steering(__instance)) __instance.m_backwardForce = __state * (1f + Stone.Find("Njord").P(0));
            }

            static void Postfix(Ship __instance, float __state)
            {
                __instance.m_backwardForce = __state;
            }
        }
    }

    // An effect that only shows for a while (its time is set when it goes on), with a text tooltip.
    class SE_Timed : StatusEffect
    {
        internal string TooltipKey;

        public override string GetTooltipString()
        {
            return TooltipKey != null ? Texts.Get(TooltipKey) : base.GetTooltipString();
        }
    }

    // Freyja's blessing: more damage while it lasts (kills heal: Powers.HealIfBlessed).
    class SE_Blessing : SE_Timed
    {
        public override string GetTooltipString()
        {
            Stone freyja = Stone.Find(Powers.Freyja);
            return Texts.Get("ssov_freyja_blessing_tooltip", Mathf.RoundToInt(freyja.V(0)).ToString(), Mathf.RoundToInt(freyja.V(1)).ToString());
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            base.ModifyAttack(skill, ref hitData);
            if (hitData != null) hitData.m_damage.Modify(1f + Stone.Find(Powers.Freyja).P(0));
        }
    }
}
