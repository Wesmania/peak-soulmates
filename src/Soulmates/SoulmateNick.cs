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

        if (!Soulmates.ActorIsSoulmate(co))
        {
            // FIXME what is the original color?
            t.color = Color.white;
        }
        else
        {
            t.color = new Color(0.3f, 1f, 0.3f);
        }
    }
}
