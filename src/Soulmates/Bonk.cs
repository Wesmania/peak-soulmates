using ExitGames.Client.Photon;
using HarmonyLib;
using UnityEngine;

namespace Soulmates;

public static class Bonk
{
    public static void OnSharedBonkEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var bonk = SharedBonk.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        if (!Plugin.localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        if (Plugin.globalSoulmate != bonk.victim)
        {
            return;
        }

        localChar.Fall(bonk.ragdollTime);
        localChar.AddForceAtPosition(bonk.force.toVector3(), bonk.contactPoint.toVector3(), bonk.range);
    }
}


[HarmonyPatch(typeof(Bonkable))]
public static class BonkPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Bonk", typeof(Collision))]
    public static void BonkPrefix(Bonkable __instance, Collision coll)
    {
        // Same checks as original function.
        Character componentInParent = coll.gameObject.GetComponentInParent<Character>();
        if ((bool)componentInParent && Time.time > __instance.lastBonkedTime + __instance.bonkCooldown)
        {
            if (!Plugin.previousSoulmates.HasValue || !Plugin.previousSoulmates.Value.config.sharedBonk)
            {
                return;
            }

            SharedBonk b;
            b.ragdollTime = __instance.ragdollTime;
            b.force = new V3(-coll.relativeVelocity.normalized * __instance.bonkForce);
            b.contactPoint = new V3(coll.contacts[0].point);
            b.range = __instance.bonkRange;
            b.victim = componentInParent.photonView.Owner.ActorNumber;
            Events.SendSharedBonkEvent(b);
        }
    }
}