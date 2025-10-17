using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace Soulmates;

public static class Events
{
    private static void SendEvent<T>(SoulmateEventType eventType, string e, ReceiverGroup who) where T : struct
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { Receivers = who };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    private static void SendToSoulmate<T>(SoulmateEventType eventType, string e) where T : struct
    {
        if (Plugin.globalSoulmate == -1) return;

        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { TargetActors = [Plugin.globalSoulmate] };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendConnectToSoulmateEvent(ConnectToSoulmate e)
    {
        Plugin.Log.LogInfo("Sending connect to soulmate event...");
        SendToSoulmate<ConnectToSoulmate>(SoulmateEventType.CONNECT_TO_SOULMATE, e.Serialize());
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        SendEvent<RecalculateSoulmatesEvent>(SoulmateEventType.RECALCULATE, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedDamageEvent(SharedDamage e)
    {
        if (!e.type.isShared() || e.type.isAbsolute())
        {
            Plugin.Log.LogInfo("$Tried to send a non-shared or absolute status type {statusType}");
            return;
        }
        SendToSoulmate<SharedDamage>(SoulmateEventType.DAMAGE, e.Serialize());
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        Plugin.Log.LogInfo($"Sending weight update: weight {e.weight}, thorns {e.thorns}");
        SendEvent<UpdateWeight>(SoulmateEventType.UPDATE_WEIGHT, e.Serialize(), ReceiverGroup.Others);
    }

    public static void SendSharedBonkEvent(SharedBonk e)
    {
        Plugin.Log.LogInfo($"Sending bonk {e.victim} {e.ragdollTime} {e.force} {e.contactPoint} {e.range}");
        SendEvent<SharedBonk>(SoulmateEventType.SHARED_BONK, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedExtraStaminaEvent(SharedExtraStamina e)
    {
        SendToSoulmate<SharedExtraStamina>(SoulmateEventType.SHARED_EXTRA_STAMINA, e.Serialize());
    }
    public static void SendSharedAfflictionEvent(SharedAffliction e)
    {
        SendToSoulmate<SharedAffliction>(SoulmateEventType.SHARED_AFFLICTION, e.Serialize());
    }
}
