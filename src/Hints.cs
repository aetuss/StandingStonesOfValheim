using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Reading a stone hints at the nearest stone this character hasn't found: of the same land if any is
    // left there, else of any land. Only the server knows where the stones are, so the client asks it over
    // an RPC; the answer is a direction and a rough distance, told as text (no map pin).
    static class Hints
    {
        const string RequestRpc = "SSoV_HintRequest";
        const string AnswerRpc = "SSoV_HintAnswer";
        const float NearDistance = 700f;
        const float FarDistance = 2000f;

        static readonly string[] Directions = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };
        static readonly System.Reflection.MethodInfo serverPeerId = AccessTools.Method(typeof(ZRoutedRpc), "GetServerPeerID");
        static float lastAsked = -100f;

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
            ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
        }

        internal static void Ask(Stone touched, Vector3 at)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZRoutedRpc.instance == null || Time.time - lastAsked < 3f) return;
            lastAsked = Time.time;
            ZPackage request = new ZPackage();
            request.Write(touched.Id);
            request.Write(at);
            request.Write(string.Join(",", StoneData.Found(player).ToArray()));
            ZRoutedRpc.instance.InvokeRoutedRPC((long)serverPeerId.Invoke(ZRoutedRpc.instance, null), RequestRpc, request);
        }

        // Server: answers with a direction index (0 north, clockwise in eighths; -1 for none left) and a
        // distance band (0 near, 1 a long way, 2 very far).
        static void OnRequest(long sender, ZPackage request)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            Stone touched = Stone.Find(request.ReadString());
            Vector3 from = request.ReadVector3();
            List<string> found = new List<string>(request.ReadString().Split(','));
            bool any = false, anySameLand = false;
            float best = float.MaxValue, bestSameLand = float.MaxValue;
            Vector3 target = Vector3.zero, targetSameLand = Vector3.zero;
            foreach (ZoneSystem.LocationInstance instance in Commands.Instances(null))
            {
                Stone stone = Stone.ByLocation(instance.m_location.m_prefabName);
                if (stone == null || found.Contains(stone.Id)) continue;
                float distance = Commands.Distance(instance, from);
                if (distance < best) { best = distance; target = instance.m_position; any = true; }
                if (touched != null && stone.Land == touched.Land && distance < bestSameLand)
                {
                    bestSameLand = distance;
                    targetSameLand = instance.m_position;
                    anySameLand = true;
                }
            }
            if (anySameLand) { target = targetSameLand; best = bestSameLand; }
            ZPackage answer = new ZPackage();
            if (!any)
            {
                answer.Write(-1);
                answer.Write(0);
            }
            else
            {
                float angle = Mathf.Atan2(target.x - from.x, target.z - from.z) * Mathf.Rad2Deg; // 0 = north (+z), 90 = east
                answer.Write(((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8);
                answer.Write(best < NearDistance ? 0 : best < FarDistance ? 1 : 2);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
            Plugin.Debug("Hint for " + sender + ": " + (any ? "stone " + best.ToString("F0") + " m away" : "none left"));
        }

        static void OnAnswer(long sender, ZPackage answer)
        {
            int direction = answer.ReadInt();
            int band = answer.ReadInt();
            string text = direction < 0 ? Texts.Get("ssov_hint_none")
                : Texts.Get("ssov_hint", Texts.Get("ssov_dir_" + Directions[direction]), Texts.Get("ssov_far_" + band));
            Plugin.Instance.StartCoroutine(ShowWhenRead(text));
        }

        // After the player closes the stone's text, so the message isn't hidden behind it.
        static IEnumerator ShowWhenRead(string text)
        {
            float until = Time.time + 120f;
            while (TextViewer.instance != null && TextViewer.instance.IsVisible() && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.4f);
            if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, text, 0, null);
        }
    }
}
