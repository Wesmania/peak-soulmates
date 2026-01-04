using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Soulmates;

[HarmonyPatch(typeof(SlipperyJellyfish))]
public static class SlipPatch1
{
    [HarmonyPrefix]
    [HarmonyPatch("OnTriggerEnter", typeof(Collider))]
    public static void OnTriggerEnterPrefix(SlipperyJellyfish __instance, Collider other)
    {
        if (!Plugin.config.SharedSlip()) return;

        // Repeat function's logic, but for soulmate.
        if (!(__instance.counter < 3f))
        {
            Character componentInParent = other.GetComponentInParent<Character>();
            if (!(bool)componentInParent) return;
            int cnum = componentInParent.photonView.Owner.ActorNumber;
            Pid? cpid = SteamComms.PhotonIdToPid(cnum);
            if (cpid == null) return;
            if (Plugin.globalSoulmates.PidIsSoulmate(cpid.Value))
            {
                // A bit awkward since now the timeout is shared between both players. Oh well.
                __instance.counter = 0f;
                __instance.relay.view.RPC("RPCA_TriggerWithTarget", RpcTarget.All, __instance.transform.GetSiblingIndex(), Character.localCharacter.refs.view.ViewID);
            }
        }
    }
}

[HarmonyPatch(typeof(BananaPeel))]
public static class SlipPatch2
{
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    public static void UpdatePrefix(BananaPeel __instance)
    {
        if (!Plugin.config.SharedSlip()) return;

        // Repeat slip check for all soulmates.
        foreach (PlayerCharacterInfo i in Plugin.globalSoulmates.MySoulmateCharacters())
        {
            if (__instance.item.itemState == ItemState.Ground)
            {
                __instance.counter += Time.deltaTime;
                if (!(__instance.counter < 3f) &&
                    !(Vector3.Distance(i.c.Center, __instance.transform.position) > 1f) &&
                    i.c.data.isGrounded &&
                    !(i.c.data.avarageVelocity.magnitude < 1.5f))
                {
                    // A bit awkward since now the timeout is shared between all soulmates. Oh well.
                    __instance.counter = 0f;
                    __instance.GetComponent<PhotonView>().RPC("RPCA_TriggerBanana", RpcTarget.All, Character.localCharacter.refs.view.ViewID);
                }
            }
        }
    }
}