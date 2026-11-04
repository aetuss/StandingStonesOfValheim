using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace StandingStonesOfValheim
{
    // With DebugLog on: when the compendium shows a text while a stone is chosen, writes how its text box
    // laid the text out (testers saw the stone's tooltip cut off there).
    [HarmonyPatch(typeof(TextsDialog), "ShowText", new[] { typeof(TextsDialog.TextInfo) })]
    static class CompendiumDebug
    {
        static void Postfix(TextsDialog __instance)
        {
            if (!Plugin.DebugLog.Value || Player.m_localPlayer == null || StoneData.Active(Player.m_localPlayer) == null) return;
            TMP_Text area = __instance.m_textArea;
            if (area == null) return;
            area.ForceMeshUpdate();
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
