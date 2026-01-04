using UnityEngine;

namespace Soulmates;

[HarmonyLib.HarmonyPatch(typeof(UIPlayerNames))]
public class SoulmateNickPatch {
    [HarmonyLib.HarmonyPostfix]
    [HarmonyLib.HarmonyPatch("UpdateName", typeof(int), typeof(Vector3), typeof(bool), typeof(int))]
    public static void UpdateNamePostfix(UIPlayerNames __instance, int index, Vector3 position, bool visible, int speakingAmplitude) {
        if (!Character.localCharacter || index >= __instance.playerNameText.Length)
        {
            return;
        }
        var c = __instance.playerNameText[index].characterInteractable.character;
        var co = c.photonView.Owner.ActorNumber;
        var t = __instance.playerNameText[index].text;

        t.color = Color.white;

        var pid = SteamComms.PhotonIdToPid(co);
        if (pid == null) return;

        if (Plugin.globalSoulmates.PidIsSoulmate(pid.Value))
        {
            t.color = Colors.soulmateColor;
            return;
        }
        var grp = Plugin.globalSoulmates.NickToSoulmateGroup(c.photonView.Owner.NickName);
        if (grp == null) { return; }
        t.color = Colors.getColor(grp.Value);
    }
}
