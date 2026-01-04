using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;

namespace Soulmates;

class TellMeMySoulmate
{
    public static void OnWhoIsMySoulmate(Pid sender, string json)
    {
        var damage = WhoIsMySoulmate.Deserialize(json);

        if (!PhotonNetwork.IsMasterClient) return;
        if (!SoulmateProtocol.instance.previousSoulmates.HasValue) return;

        Events.SendThisIsYourSoulmatesEvent(SoulmateProtocol.instance.previousSoulmates.Value, sender);
    }
}

[HarmonyPatch(typeof(NetworkConnector))]
class WhoIsMySoulmatePatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnJoinedRoom")]
    public static void OnJoinedRoomPostfix(ReconnectHandler __instance)
    {
        if (!SoulmateProtocol.instance.previousSoulmates.HasValue)
        {
            Events.SendWhoIsMySoulmatesEvent();
        }
    }
}
