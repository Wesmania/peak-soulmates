using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;

namespace Soulmates;

class TellMeMySoulmate
{
    public static void OnWhoIsMySoulmate(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var damage = WhoIsMySoulmate.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        if (!PhotonNetwork.IsMasterClient) return;
        if (!Plugin.previousSoulmates.HasValue) return;

        Events.SendThisIsYourSoulmatesEvent(Plugin.previousSoulmates.Value, senderActorNumber);
    }
}

[HarmonyPatch(typeof(NetworkConnector))]
class WhoIsMySoulmatePatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnJoinedRoom")]
    public static void OnJoinedRoomPostfix(ReconnectHandler __instance)
    {
        if (!Plugin.previousSoulmates.HasValue)
        {
            Events.SendWhoIsMySoulmatesEvent();
        }
    }
}
