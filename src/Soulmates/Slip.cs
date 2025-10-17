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
        if (!Plugin.previousSoulmates.HasValue || !Plugin.previousSoulmates.Value.config.sharedSlip)
        {
            return;
        }

        // Repeat function's logic, but for soulmate.
        if (!(__instance.counter < 3f))
        {
            Character componentInParent = other.GetComponentInParent<Character>();
            if ((bool)componentInParent && componentInParent.photonView.Owner.ActorNumber == Plugin.globalSoulmate)
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
        if (!Plugin.previousSoulmates.HasValue || !Plugin.previousSoulmates.Value.config.sharedSlip)
        {
            return;
        }

        // Repeat function's logic, but for soulmate.
        var soulmate = Plugin.GetSoulmate(Plugin.globalSoulmate);
        if (soulmate == null) return;

        if (__instance.item.itemState == ItemState.Ground)
        {
            __instance.counter += Time.deltaTime;
            if (!(__instance.counter < 3f) &&
                !(Vector3.Distance(soulmate.Center, __instance.transform.position) > 1f) &&
                soulmate.data.isGrounded &&
                !(soulmate.data.avarageVelocity.magnitude < 1.5f))
            {
                // A bit awkward since now the timeout is shared between both players. Oh well.
                __instance.counter = 0f;
                __instance.GetComponent<PhotonView>().RPC("RPCA_TriggerBanana", RpcTarget.All, Character.localCharacter.refs.view.ViewID);
            }
        }
    }
}