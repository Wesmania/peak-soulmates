using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace Soulmates;

public static class Events
{
    public static void SendConnectToSoulmateEvent(ConnectToSoulmate e)
    {
        Plugin.Log.LogInfo("Sending connect to soulmate event...");
        object[] content = [(int)SoulmateEventType.CONNECT_TO_SOULMATE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        object[] content = [(int)SoulmateEventType.RECALCULATE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendSharedDamageEvent(SharedDamage e)
    {
        if (!e.type.isShared() || e.type.isAbsolute())
        {
            Plugin.Log.LogInfo("$Tried to send a non-shared or absolute status type {statusType}");
            return;
        }
        object[] content = [(int) SoulmateEventType.DAMAGE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        Plugin.Log.LogInfo($"Sending weight update: weight {e.weight}, thorns {e.thorns}");
        object[] content = [(int)SoulmateEventType.UPDATE_WEIGHT, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
}