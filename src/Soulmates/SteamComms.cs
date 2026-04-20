global using Pid = string;

using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

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

// Actually Photon still, don't worry about it.
public class SteamComms
{
    public static SteamComms instance = new();
    internal const byte SHARED_DAMAGE_EVENT_CODE = 198;

    Action<Pid, SoulmateEventType, string>? eventHandle;

    public static void Awake(Action<Pid, SoulmateEventType, string> handle)
    {
        instance.eventHandle = handle;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }
    public static void OnDestroy()
    {
        instance.eventHandle = null;
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
    }

    public static Pid MyNumber()
    {
        return PhotonNetwork.LocalPlayer.NickName;
    }
    public static string MyNick()
    {
        return PhotonNetwork.LocalPlayer.NickName;
    }
    public static bool IAmHost()
    {
        return PhotonNetwork.IsMasterClient;
    }

    public static Pid[] AllPlayerNumbers()
    {
        return [.. PhotonNetwork.PlayerList.Select(c => c.NickName)];
    }

    // TODO is it expensive to create that dict on-the-fly?
    public static PlayerInfo[] AllPlayers()
    {
        return [.. PhotonNetwork.PlayerList.Select(c =>
        new PlayerInfo
        {
            id = c.NickName,
            nickname = c.NickName,
        }
        )];
    }

    public static string? IdToNick(Pid id)
    {
        return id;
    }
    public static Pid? PhotonIdToId(int id)
    {
        return PhotonNetwork.PlayerList.FirstOrDefault(c => c.ActorNumber == id)?.NickName;
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
        var c = PhotonNetwork.PlayerList.FirstOrDefault(c => c.ActorNumber == actorNumber);
        if (c == null) return null;
        var nick = c.NickName;
        var players = AllPlayers().ToLookup(p => p.nickname, p => p.id);
        return players.Contains(nick) ? players[nick].First() : null;
    }
    public static PlayerCharacterInfo[] NicksToInfos(IEnumerable<string> nicks)
    {
        var players = AllPlayers().ToLookup(p => p.nickname);
        var chars = Character.AllCharacters.ToDictionary(c => c.photonView.Owner.NickName);
        return [.. nicks.Where(n => players.Contains(n) && chars.ContainsKey(n))
                       .Select(n => new PlayerCharacterInfo
                       {
                           p = players[n].First(),
                           c = chars[n]
                       })];
    }

    public static void SendEvent(SoulmateEventType eventType, string e, ReceiverGroup who, bool reliable = false)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { Receivers = who };
        var r = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
        PhotonNetwork.RaiseEvent(SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, r);
    }

    public static int[] PidsToActors(Pid[] targets)
    {
        return [.. targets.Select(t =>
        {
            var player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.NickName == t);
            return player != null ? player.ActorNumber : -1;
        }).Where(n => n != -1)];
    }
    public static void SendEventTo(SoulmateEventType eventType, string e, Pid[] targets, bool reliable = false)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.Others, TargetActors = PidsToActors(targets) };
        var r = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
        PhotonNetwork.RaiseEvent(SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, r);
    }

    public static void OnEvent(EventData e)
    {
        if (e.Code != SHARED_DAMAGE_EVENT_CODE) return;
        object[] data = (object[])e.CustomData;
        Pid? sender = PhotonIdToPid(e.Sender);
        if (sender == null) return;
        SoulmateEventType eventType = (SoulmateEventType)(int)data[0];
        string s = (string)data[1];
        HandleEvent(sender, eventType, s);
    }
    public static void HandleEvent(Pid sender, SoulmateEventType eventType, string e)
    {
        instance.eventHandle?.Invoke(sender, eventType, e);
    }
}