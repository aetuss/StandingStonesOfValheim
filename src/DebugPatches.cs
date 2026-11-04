using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace StandingStonesOfValheim
{
    // The compendium's text box (testers saw the stone's tooltip cut off there). With DebugLog on, while a
    // stone is chosen, also writes how the box laid the text out.
    [HarmonyPatch(typeof(TextsDialog), "ShowText", new[] { typeof(TextsDialog.TextInfo) })]
    static class CompendiumDebug
    {
        static void Postfix(TextsDialog __instance)
        {
            TMP_Text area = __instance.m_textArea;
            if (area == null) return;
            // The game's text box cuts off what doesn't fit (overflow Truncate), and its height catches up
            // with a new text only a moment later; the game's own texts end with empty lines, so only those
            // are lost, but ours lost its last words. Let the text run on and the box grow to it.
            area.overflowMode = TextOverflowModes.Overflow;
            area.ForceMeshUpdate();
            if (!Plugin.DebugLog.Value || Player.m_localPlayer == null || StoneData.Active(Player.m_localPlayer) == null) return;
            string text = area.text ?? "";
            int shown = area.textInfo != null ? area.textInfo.characterCount : -1;
            string missing = "";
            TMP_FontAsset font = area.font;
            if (font != null)
            {
                List<char> chars;
                if (!font.HasCharacters(text, out chars) && chars != null)
                    foreach (char c in chars) missing += ((int)c).ToString("X4") + " ";
            }
            string end = "";
            if (area.textInfo != null && shown > 0)
                for (int i = System.Math.Max(0, shown - 12); i < shown; i++) end += area.textInfo.characterInfo[i].character;
            Plugin.Log.LogInfo("Compendium text: length " + text.Length + ", laid out " + shown + ", font " + (font != null ? font.name : "?") +
                ", overflow " + area.overflowMode + ", max chars " + area.maxVisibleCharacters + ", max lines " + area.maxVisibleLines +
                ", wrapping " + area.textWrappingMode + ", rect " + area.rectTransform.rect.size + ", missing glyphs: " + (missing.Length > 0 ? missing : "none") +
                ", ends with \"" + end.Replace("\n", "\\n") + "\"");
            Plugin.Log.LogInfo("Compendium text is: " + text.Replace("\n", "\\n"));
        }
    }
}
