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
            int cnum = (bool) componentInParent ? componentInParent.photonView.Owner.ActorNumber : -1;
            if ((bool)componentInParent && Soulmates.ActorIsSoulmate(cnum))
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

        // Repeat slip check for all soulmates.
        foreach (Character c in Soulmates.SoulmateCharacters())
        {

            if (__instance.item.itemState == ItemState.Ground)
            {
                __instance.counter += Time.deltaTime;
                if (!(__instance.counter < 3f) &&
                    !(Vector3.Distance(c.Center, __instance.transform.position) > 1f) &&
                    c.data.isGrounded &&
                    !(c.data.avarageVelocity.magnitude < 1.5f))
                {
                    // A bit awkward since now the timeout is shared between all soulmates. Oh well.
                    __instance.counter = 0f;
                    __instance.GetComponent<PhotonView>().RPC("RPCA_TriggerBanana", RpcTarget.All, Character.localCharacter.refs.view.ViewID);
                }
            }
        }
    }
}