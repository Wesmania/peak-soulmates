global using Pid = ulong;

using System;
using System.Collections.Generic;
using System.Linq;
using NetworkingLibrary;
using NetworkingLibrary.Modules;
using NetworkingLibrary.Services;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;

namespace Soulmates;
public struct PlayerInfo
{
    public Pid id;
    public String nickname;
}

public struct PlayerCharacterInfo
{
    public PlayerInfo p;
    public Character c;
}
public class SteamComms
{
    internal static INetworkingService Service = Net.Service!;
    static readonly uint MOD_ID = ModId.FromGuid("com.github.Wesmania.Soulmates");
    Action<Pid, SoulmateEventType, string>? eventHandle;

    public static SteamComms instance = new();
    public static void Awake(Action<Pid, SoulmateEventType, string> handle)
    {
        instance.eventHandle = handle;
        SteamNetworkingService? s = (SteamNetworkingService) Service;
        s?.SetSharedSecret(null);
        Service.RegisterNetworkType(typeof(SteamComms), MOD_ID);
    }
    public static void OnDestroy() {
        Service.DeregisterNetworkType(typeof(SteamComms), MOD_ID);
    }

    public static Pid MyNumber()
    {
        return Service.GetLocalSteam64();
    }
    public static string MyNick() {
        return SteamFriends.GetPersonaName();
    }
    public static bool IAmHost()
    {
        return PhotonNetwork.IsMasterClient;
    }

    public static Pid[] AllPlayerNumbers()
    {
        return Service.GetLobbyMemberSteamIds();
    }

    // TODO is it expensive to create that dict on-the-fly?
    public static PlayerInfo[] AllPlayers()
    {
        var ids = Service.GetLobbyMemberSteamIds();
        PlayerInfo[] players = [.. ids.Select(id => new PlayerInfo
        {
            id = id,
            nickname = SteamFriends.GetFriendPersonaName(new CSteamID(id))
        })];
        return players;
    }

    public static string? IdToNick(Pid id)
    {
        var s = SteamFriends.GetFriendPersonaName(new CSteamID(id));
        if (s == "" || s == "[unknown]") return null;
        return s;
    }
    public static Character? IdToCharacter(Pid id)
    {
        var chars = Character.AllCharacters;
        PlayerInfo? players = AllPlayers().FirstOrDefault(p => p.id == id);
        if (!players.HasValue) return null;
        return chars.FirstOrDefault(c => c.photonView.Owner.NickName == players.Value.nickname);
    }

    public static Pid? PhotonIdToPid(int actorNumber)
    {
        var c = Character.AllCharacters.FirstOrDefault(c => c.photonView.Owner.ActorNumber == actorNumber);
        if (c == null) return null;
        var nick = c.photonView.Owner.NickName;
        var players = AllPlayers().ToDictionary(p => p.nickname, p => p.id);
        return players.ContainsKey(nick) ? players[nick] : null;
    }
    public static PlayerCharacterInfo[] NicksToInfos(IEnumerable<string> nicks)
    {
        var players = AllPlayers().ToDictionary(p => p.nickname);
        var chars = Character.AllCharacters.ToDictionary(c => c.photonView.Owner.NickName);
        return [.. nicks.Where(n => players.ContainsKey(n) && chars.ContainsKey(n))
                       .Select(n => new PlayerCharacterInfo
                       {
                           p = players[n],
                           c = chars[n]
                       })];
    }

    public static void SendEvent(SoulmateEventType eventType, string e, ReceiverGroup who, bool reliable = false)
    {
        var r = reliable ? ReliableType.Reliable : ReliableType.Unreliable;
        var h = nameof(HandleEvent);
        Pid sender = MyNumber();
        if (who == ReceiverGroup.All || who == ReceiverGroup.Others)
        {
            Plugin.Log.LogInfo($"Sending RPC {eventType}");
            Service.RPC(MOD_ID, h, r, sender, eventType, e);
        }
        else
        {
            Plugin.Log.LogInfo($"Sending RPCToHost {eventType}");
            Service.RPCToHost(MOD_ID, h, r, sender, eventType, e);
        }
    }

    public static void SendEventTo(SoulmateEventType eventType, string e, Pid[] targets, bool reliable = false)
    {
        object[] content = [(int)eventType, e];
        var r = reliable ? ReliableType.Reliable : ReliableType.Unreliable;
        Pid[] others = Service.GetLobbyMemberSteamIds();
        Pid sender = MyNumber();

        foreach (var target in targets)
        {
            Service.RPCTarget(MOD_ID, nameof(HandleEvent), target, r, sender, eventType, e);
        }
    }

    [CustomRPC]
    public static void HandleEvent(Pid sender, SoulmateEventType eventType, string e)
    {
        Plugin.Log.LogInfo($"Received RPC from {sender}, type {eventType}");
        instance.eventHandle?.Invoke(sender, eventType, e);
    }
}