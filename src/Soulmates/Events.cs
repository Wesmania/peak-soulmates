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
    private static void SendEventTo(SoulmateEventType eventType, string e, int target)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { TargetActors = [target] };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    private static void SendToSoulmate(SoulmateEventType eventType, string e)
    {
        if (Plugin.globalSoulmate == "") return;

        SendEventTo(eventType, e, Plugin.soulmateNumber());
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
        SendToSoulmate(SoulmateEventType.DAMAGE, e.Serialize());
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
        SendToSoulmate(SoulmateEventType.SHARED_EXTRA_STAMINA, e.Serialize());
    }
    public static void SendSharedAfflictionEvent(SharedAffliction e)
    {
        SendToSoulmate(SoulmateEventType.SHARED_AFFLICTION, e.Serialize());
    }
    public static void SendWhoIsMySoulmateEvent()
    {
        WhoIsMySoulmate w;
        SendEvent(SoulmateEventType.WHO_IS_MY_SOULMATE, w.Serialize(), ReceiverGroup.Others);
    }
    public static void SendThisIsYourSoulmateEvent(RecalculateSoulmatesEvent e, int target)
    {
        SendEventTo(SoulmateEventType.RECALCULATE, e.Serialize(), target);
    }
}
