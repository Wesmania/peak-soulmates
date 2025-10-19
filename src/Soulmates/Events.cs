using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace Soulmates;

public static class Events
{
    private static void SendEvent(SoulmateEventType eventType, string e, ReceiverGroup who)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { Receivers = who };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    private static void SendEventTo(SoulmateEventType eventType, string e, int[] targets)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { TargetActors = targets };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    private static void SendToSoulmates(SoulmateEventType eventType, string e)
    {
        if (Soulmates.NoSoulmates()) return;

        SendEventTo(eventType, e, Soulmates.SoulmateNumbers().ToArray());
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        SendEvent(SoulmateEventType.RECALCULATE, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedDamageEvent(SharedDamage e)
    {
        if (!e.type.isShared() || e.type.isAbsolute())
        {
            Plugin.Log.LogInfo("$Tried to send a non-shared or absolute status type {statusType}");
            return;
        }
        SendToSoulmates(SoulmateEventType.DAMAGE, e.Serialize());
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        SendEvent(SoulmateEventType.UPDATE_WEIGHT, e.Serialize(), ReceiverGroup.Others);
    }

    public static void SendSharedBonkEvent(SharedBonk e)
    {
        Plugin.Log.LogInfo($"Sending bonk {e.victim} {e.ragdollTime} {e.force} {e.contactPoint} {e.range}");
        SendEvent(SoulmateEventType.SHARED_BONK, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedExtraStaminaEvent(SharedExtraStamina e)
    {
        SendToSoulmates(SoulmateEventType.SHARED_EXTRA_STAMINA, e.Serialize());
    }
    public static void SendSharedAfflictionEvent(SharedAffliction e)
    {
        SendToSoulmates(SoulmateEventType.SHARED_AFFLICTION, e.Serialize());
    }
    public static void SendWhoIsMySoulmatesEvent()
    {
        WhoIsMySoulmate w;
        SendEvent(SoulmateEventType.WHO_IS_MY_SOULMATES, w.Serialize(), ReceiverGroup.Others);
    }
    public static void SendThisIsYourSoulmatesEvent(RecalculateSoulmatesEvent e, int target)
    {
        SendEventTo(SoulmateEventType.RECALCULATE, e.Serialize(), [target]);
    }
}
