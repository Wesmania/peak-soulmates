using System;
using System.Collections.Generic;
using System.Linq;
using pworld.Scripts.Extensions;

namespace Soulmates;

public class Soulmates
{
    private Dictionary<string, int> soulmatesByName;
    private Dictionary<int, HashSet<string>> soulmatesByGroup;
    private readonly string myName;
    private readonly int myGroup;

    public Soulmates() : this([], "None", 0) { }
    public Soulmates(Dictionary<string, int> soulmatesByName, string myName, int myGroup)
    {
        this.soulmatesByName = soulmatesByName;
        this.myName = myName;
        this.myGroup = myGroup;
        this.soulmatesByGroup = soulmatesByName.GroupBy(kv => kv.Value)
                                         .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToHashSet());
    }

    public HashSet<string> MySoulmates()
    {
        return soulmatesByGroup.ContainsKey(myGroup) ? [.. soulmatesByGroup[myGroup].Where(n => n != myName)] : [];
    }
    public HashSet<Pid> MySoulmatePids()
    {
        var my = MySoulmates();
        var ps = SteamComms.AllPlayers().ToDictionary(p => p.nickname);
        return [.. my.SelectMany(sn => ps.ContainsKey(sn) ? [ps[sn].id] : Array.Empty<Pid>())];
    }
    public HashSet<PlayerCharacterInfo> MySoulmateCharacters()
    {
        return [.. SteamComms.NicksToInfos(MySoulmates())];
    }
    public int? NickToSoulmateGroup(string nick)
    {
        return soulmatesByName.ContainsKey(nick) ? soulmatesByName[nick] : null;
    }
    public bool PidIsSoulmate(Pid id)
    {
        return MySoulmatePids().Contains(id);
    }
    public bool NoSoulmates()
    {
        return MySoulmates().Count == 0;
    }
    public string SoulmateLog()
    {
        return String.Join(", ", MySoulmates());
    }
    public string SoulmateText()
    {
        var mySoulmates = this.MySoulmates();
        if (mySoulmates.Count == 0)
        {
            return "Soulmate: None";
        }
        else if (mySoulmates.Count == 1)
        {
            return "Soulmate: " + mySoulmates.First();
        }
        else
        {
            return "Soulmates:\n" + String.Join("\n", mySoulmates);
        }
    }
    public int LiveSoulmateCount()
    {
        return MySoulmatePids().Count(n =>
        {
            var c = SteamComms.IdToCharacter(n);
            return c != null && c.isLiv();
        });
    }
}

public class SoulmateProtocol
{
    public static SoulmateProtocol instance = new();
    public RecalculateSoulmatesEvent? previousSoulmates = null;
    public Soulmates? OnNewSoulmates(string json)
    {
        Plugin.Log.LogInfo("Received recalculate soulmate event");
        var soulmates = RecalculateSoulmatesEvent.Deserialize(json);
        Plugin.config.SetReceivedConfig(soulmates.config);
        var newSoulmates = findSoulmates(soulmates.soulmates);

        if (newSoulmates == null)
        {
            Plugin.Log.LogWarning("Failed to processs new soulmates!");
            previousSoulmates = null;
            Plugin.config.ClearReceivedConfig();
            return null;
        }
        previousSoulmates = soulmates;

        if (newSoulmates.NoSoulmates())
        {
            Plugin.Log.LogInfo("No soulmates");
        }
        else
        {
            Plugin.Log.LogInfo($"New soulmates: {newSoulmates.SoulmateLog()}");
        }

        if (soulmates.firstTime)
        {
            // Starting game. Clear data, do nothing else.
            Weight.Clear();
        }
        else
        {
            ConnectToNewSoulmate(soulmates);
        }

        // Some time after biome title card
        SoulmateTextPatch.SetSoulmateText(newSoulmates.SoulmateText(), soulmates.firstTime ? 10 : 15);
        return newSoulmates;
    }

    private static void ConnectToNewSoulmate(RecalculateSoulmatesEvent e)
    {
        if (!Plugin.LocalCharIsReady()) return;
        Character localChar = Character.localCharacter;
        localChar.refs.afflictions.UpdateWeight();
    }
    private Soulmates? findSoulmates(List<Pid> soulmates)
    {
        var groupSize = Plugin.config.SoulmateGroupSize();
        var soulmateSets = soulmates.Select((id, idx) => (idx / groupSize, SteamComms.IdToNick(id)))
                                    .Where(p => p.Item2 != null)
                                    .Select(p => (p.Item1, p.Item2!))
                                    .ToDictionary(p => p.Item2, p => p.Item1);

        var my_pid = SteamComms.MyNumber();
        var pos = soulmates.FindIndex(x => x == my_pid);
        if (pos == -1)
        {
            Plugin.Log.LogInfo($"Did not find myself ({my_pid}) on soulmate list!");
            return null;
        }
        Plugin.Log.LogInfo($"Found my index: {pos}");
        var myGroup = pos / groupSize;

        var mates = new Soulmates(soulmateSets, SteamComms.MyNick(), myGroup);
        Plugin.Log.LogInfo(String.Format($"Soulmate group size: {mates.MySoulmates().Count + 1}"));
        return mates;
    }

    private static void ReorderForFixedPairings(ref List<Pid> actors)
    {
	if (!Plugin.config.HasFixedSoulmates()) return;
        var actorsWithNames = actors.ToDictionary(a => SteamComms.IdToNick(a));
        var fixedPairs = Plugin.config.GetFixedSoulmates();
        if (fixedPairs.Count == 0) return;

        if (!fixedPairs.All(l => l.Count == Plugin.config.SoulmateGroupSize()))
        {
            Plugin.Log.LogWarning("Fixed soulmate groups don't match soulmate group size! FIXME we should be able to handle this.");
            return;
        }
        var fittingFixedPairs = fixedPairs.Where(l => l.All(s => actorsWithNames.ContainsKey(s)));
        var fixedList = fittingFixedPairs.SelectMany(l => l.Select(s => actorsWithNames[s])).ToList();
        var fixedSet = fixedList.ToHashSet();
        if (fixedSet.Count < fixedList.Count)
        {
            Plugin.Log.LogWarning("Fixed soulmate groups have repeating names!");
            return;
        }
        var rest = actors.Where(a => !fixedSet.Contains(a));
        actors = [.. fixedList, .. rest];
    }

    public RecalculateSoulmatesEvent? PrepareNewSoulmates(bool firstTime)
    {
        if (!SteamComms.IAmHost()) return null;
        Plugin.Log.LogInfo("I am master client, preparing new soulmate list");

        var players = SteamComms.AllPlayers();
        var ids = players.Select(x => x.id).ToList();
        var nicks = players.Select(x => x.nickname).ToList();
        nicks.Sort();

        var all = String.Join(" ", nicks);
        Plugin.Log.LogInfo($"Character count: {nicks.Count()}, Characters: {all}");

        ids.Shuffle();
        ReorderForFixedPairings(ref ids);

        RecalculateSoulmatesEvent soulmates;

        soulmates.soulmates = ids;
        soulmates.firstTime = firstTime;

        if (firstTime)
        {
            Plugin.config.ClearReceivedConfig();
            previousSoulmates = null;
        }
        soulmates.config = Plugin.config.GetConfigToSend();
        // FIXME: make sure to ignore dead soulmates...
        return soulmates;
    }
    }
