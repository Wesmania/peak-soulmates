using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using ExitGames.Client.Photon;
using System.Linq;
using pworld.Scripts.Extensions;
using System;
using pworld.Scripts;
using Zorro.Core;
using UnityEngine.UI;

namespace Soulmates;

public static class Extensions
{
    public static bool isAbsolute(this CharacterAfflictions.STATUSTYPE t)
    {
        return t == CharacterAfflictions.STATUSTYPE.Weight || t == CharacterAfflictions.STATUSTYPE.Thorns;
    }
    public static bool isShared(this CharacterAfflictions.STATUSTYPE t)
    {
        return t != CharacterAfflictions.STATUSTYPE.Curse;
    }
    public static bool isLiv(this Character c)
    {
        return !c.data.dead && !c.warping;
    }
}

public static class Soulmates
{
    private static HashSet<string> globalSoulmates = [];
    public static Dictionary<string, int> soulmateSets = [];

    public static HashSet<int> SoulmateNumbers()
    {
        var ps = PhotonNetwork.PlayerList.ToDictionary(p => p.NickName);
        return [.. globalSoulmates.Select(sn => ps.ContainsKey(sn) ? ps[sn].ActorNumber : -1).Where(n => n != -1)];
    }
    public static bool ActorIsSoulmate(int actor)
    {
        return SoulmateNumbers().Contains(actor);
    }
    public static void SetGlobalSoulmates(HashSet<string> s)
    {
        globalSoulmates = s;
    }
    public static bool NoSoulmates()
    {
        return globalSoulmates.Count == 0;
    }
    public static string SoulmateLog()
    {
        return String.Join(", ", globalSoulmates);
    }

    public static string SoulmateText()
    {
        if (NoSoulmates())
        {
            return "Soulmate: None";
        }
        else if (globalSoulmates.Count == 1)
        {
            return "Soulmate: " + globalSoulmates.First();
        }
        else
        {
            return "Soulmates:\n" + String.Join("\n", globalSoulmates);
        }
    }
    public static int LiveSoulmateCount()
    {
        return SoulmateNumbers().Count(n =>
        {
            var c = Plugin.GetSoulmate(n);
            return c != null && c.isLiv();
        });
    }

    public static List<Character> SoulmateCharacters()
    {
        return SoulmateNumbers().Select(n => Plugin.GetSoulmate(n)).Where(c => c != null).ToList()!;
    }
}

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<int> SoulmateGroupSize { get; private set; } = null!;
    internal static ConfigEntry<float> SoulmateStrength { get; private set; } = null!;
    internal static ConfigEntry<string> FixedSoulmates { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedBonk { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedSlip { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedExtraStaminaGain { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedExtraStaminaUse { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedLolli { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSharedEnergol { get; private set; } = null!;

    internal const byte SHARED_DAMAGE_EVENT_CODE = 198;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} version 0.2.10 is loaded!");

        Enabled = Config.Bind("Config", "Enabled", true, "Enable/disable the mod with this");
        SoulmateGroupSize = Config.Bind("Config", "SoulmateGroupSize", 2, "How many people are bound in one group. Defaults to 2.");
        SoulmateStrength = Config.Bind("Config", "SoulmateStrength", 1.0f, "How much of soulmate's status is applied to you");
        FixedSoulmates = Config.Bind("Config", "FixedSoulmates", "", "Fixed soulmate assignments, matched by nick. Format is \"name1,name2;name3,name4\".\nThis will match name1 with name2 and name3 with name4.");
        EnableSharedBonk = Config.Bind("Config", "EnableSharedBonk", true, "Bonking a player bonks his soulmate too");
        EnableSharedSlip = Config.Bind("Config", "EnableSharedSlip", true, "Slipping on something makes the soulmate slip too");
        EnableSharedExtraStaminaGain = Config.Bind("Config",
                                                   "EnableSharedExtraStaminaGain",
                                                   true,
                                                   "Soulmates share extra stamina gained");
        EnableSharedExtraStaminaUse = Config.Bind("Config",
                                                  "EnableSharedExtraStaminaUse",
                                                  true,
                                                  "Soulmates use a single extra stamina pool");
        EnableSharedLolli = Config.Bind("Config",
                                        "EnableSharedLolli",
                                        true,
                                        "Soulmates share lollipop boost");
        EnableSharedEnergol = Config.Bind("Config",
                                        "EnableSharedEnergol",
                                        true,
                                        "Soulmates share energy drink boost");
        if (!Enabled.Value)
        {
            Log.LogInfo("Soulmates disabled");
            return;
        }
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;

        Harmony harmony = new("com.github.Wesmania.Soulmates");

        try
        {
            harmony.PatchAll();
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Failed to load mod: {ex}");
        }
    }

    public static bool HasFixedSoulmates()
    {
        return FixedSoulmates.Value != "";
    }
    public static List<List<string>> GetFixedSoulmates()
    {
        return FixedSoulmates.Value.Split(";").ToList().Select(s => s.Split(",").ToList()).ToList();
    }
    private void OnDestroy()
    {
        if (!Enabled.Value) return;

        if (PhotonNetwork.NetworkingClient != null)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }
    }

    public static int GetSoulmateGroupSize()
    {
        return previousSoulmates.HasValue ? previousSoulmates.Value.config.soulmateGroupSize : SoulmateGroupSize.Value;
    }
    public static float GetSoulmateStrength()
    {
        return previousSoulmates.HasValue ? previousSoulmates.Value.config.soulmateStrength : SoulmateStrength.Value;
    }


    public static bool localCharIsReady()
    {
        Character localChar = Character.localCharacter;
        if (localChar == null || !localChar.isLiv())
        {
            return false;
        }
        return true;
    }
    public static string indexToNick(int idx)
    {
        var s = PhotonNetwork.PlayerList.ToList().Find(p => p.ActorNumber == idx);
        if (s == null) return "";
        return s.NickName;
    }
    public static RecalculateSoulmatesEvent? previousSoulmates;

    private void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != SHARED_DAMAGE_EVENT_CODE)
        {
            return;
        }

        object[] data = (object[])photonEvent.CustomData;
        int eventType = (int)data[0];
        switch (eventType)
        {
            case (int)SoulmateEventType.RECALCULATE:
                OnRecalculateSoulmateEvent(photonEvent);
                break;
            case (int)SoulmateEventType.DAMAGE:
                OnSharedDamageEvent(photonEvent);
                break;
            case (int)SoulmateEventType.UPDATE_WEIGHT:
                Weight.OnUpdateWeightEvent(photonEvent);
                break;
            case (int)SoulmateEventType.SHARED_BONK:
                Bonk.OnSharedBonkEvent(photonEvent);
                break;
            case (int)SoulmateEventType.SHARED_EXTRA_STAMINA:
                StamUtil.OnSharedExtraStaminaEvent(photonEvent);
                break;
            case (int)SoulmateEventType.SHARED_AFFLICTION:
                AfflictionUtil.onSharedAfflictionEvent(photonEvent);
                break;
            case (int)SoulmateEventType.WHO_IS_MY_SOULMATES:
                TellMeMySoulmate.OnWhoIsMySoulmate(photonEvent);
                break;
            default:
                return;
        }
    }
    private void OnSharedDamageEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var damage = SharedDamage.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        if (!localCharIsReady())
        {
            return;
        }
        Character localChar = Character.localCharacter;
        if (!Soulmates.ActorIsSoulmate(senderActorNumber))
        {
            return;
        }

        if (!damage.type.isShared())
        {
            Log.LogInfo($"Received update for non-shared damage type {damage.type}");
            return;
        }
        if (damage.type.isAbsolute())
        {
            Log.LogInfo($"Received an update request for an absolute damage type {damage.type}");
            return;
        }

        SharedDamagePatch.isReceivingSharedDamage.Add(localChar.photonView.ViewID);
        var affs = localChar.refs.afflictions;
        damage.value *= GetSoulmateStrength();
        try
        {
            switch (damage.kind)
            {
                case SharedDamageKind.ADD:
                    affs.AddStatus(damage.type, damage.value);
                    break;
                case SharedDamageKind.SUBTRACT:
                    affs.SubtractStatus(damage.type, damage.value);
                    break;
                case SharedDamageKind.SET:
                    // It's a diff
                    float existing = affs.GetCurrentStatus(damage.type);
                    affs.SetStatus(damage.type, existing + damage.value);
                    break;
            }
        }
        finally
        {
            SharedDamagePatch.isReceivingSharedDamage.Remove(localChar.photonView.ViewID);
        }
    }

    private static HashSet<string> findSoulmates(List<int> soulmates)
    {
        var groupSize = GetSoulmateGroupSize();
        Soulmates.soulmateSets = soulmates.Select((id, idx) => (id, idx / groupSize))
                                          .ToDictionary(p => indexToNick(p.Item1), p => p.Item2);

        var my_actor = PhotonNetwork.LocalPlayer.ActorNumber;
        var pos = soulmates.FindIndex(x => x == my_actor);
        if (pos == -1)
        {
            Log.LogInfo($"Did not find myself ({my_actor}) on soulmate list!");
            return [];
        }
        Log.LogInfo($"Found my index: {pos}");
        var soulmatesBase = pos - (pos % groupSize);
        var soulmateIndices = Enumerable.Range(soulmatesBase, groupSize)
                                        .Where(i => i != pos && i < soulmates.Count).ToList();
        Log.LogInfo(String.Format($"Soulmate group size: {soulmateIndices.Count + 1}"));
        return soulmateIndices.Select(i => indexToNick(soulmates[i])).ToHashSet();
    }

    public static Character? GetSoulmate(int actor)
    {
        try
        {
            return Character.AllCharacters.Find(c => c.photonView.Owner.ActorNumber == actor);
        }
        catch (Exception)
        {
            return null;
        }
    }
    private static void ConnectToNewSoulmate(RecalculateSoulmatesEvent e)
    {
        if (!localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        var my_number = localChar.photonView.Owner.ActorNumber;

        localChar.refs.afflictions.UpdateWeight();
    }

    private static void OnRecalculateSoulmateEvent(EventData photonEvent)
    {
        Log.LogInfo("Received recalculate soulmate event");
        object[] data = (object[])photonEvent.CustomData;
        var soulmates = RecalculateSoulmatesEvent.Deserialize((string)data[1]);

        previousSoulmates = soulmates;

        Soulmates.SetGlobalSoulmates(findSoulmates(soulmates.soulmates));

        if (Soulmates.NoSoulmates())
        {
            Log.LogInfo("No soulmates");
        }
        else
        {
            Log.LogInfo($"New soulmates: {Soulmates.SoulmateLog()}");
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

        int delay;
        if (soulmates.firstTime)
        {
            delay = 10;
        }
        else
        {
            // Some time after biome title card
            delay = 15;
        }
        SoulmateTextPatch.SetSoulmateText(Soulmates.SoulmateText(), delay);
    }

    private static void ReorderForFixedPairings(ref List<int> actors)
    {
        var actorsWithNames = actors.ToDictionary(a => indexToNick(a));
        var fixedPairs = GetFixedSoulmates();
        if (fixedPairs.Count == 0) return;

        if (!fixedPairs.All(l => l.Count == GetSoulmateGroupSize()))
        {
            Log.LogWarning("Fixed soulmate groups don't match soulmate group size! FIXME we should be able to handle this.");
            return;
        }
        var fittingFixedPairs = fixedPairs.Where(l => l.All(s => actorsWithNames.ContainsKey(s)));
        var fixedList = fittingFixedPairs.SelectMany(l => l.Select(s => actorsWithNames[s])).ToList();
        var fixedSet = fixedList.ToHashSet();
        if (fixedSet.Count < fixedList.Count)
        {
            Log.LogWarning("Fixed soulmate groups have repeating names!");
            return;
        }
        var rest = actors.Where(a => !fixedSet.Contains(a));
        actors = [.. fixedList, .. rest];
    }

    public static RecalculateSoulmatesEvent? RecalculateSoulmate(bool firstTime)
    {
        Log.LogInfo("Recalculating soulmate");

        Log.LogInfo(String.Format("Character count: {0}", PhotonNetwork.PlayerList.Count()));
        var actors = PhotonNetwork.PlayerList.Select(x => x.ActorNumber).ToList();
        actors.Sort();
        var all = String.Join(" ", PhotonNetwork.PlayerList.Select(x => x.ToString()));
        Log.LogInfo(($"Characters: {all}"));

        if (!PhotonNetwork.IsMasterClient)
        {
            return null;
        }
        Log.LogInfo("I am master client, preparing new soulmate list");

        actors.Shuffle();
        ReorderForFixedPairings(ref actors);

        RecalculateSoulmatesEvent soulmates;

        soulmates.soulmates = actors;
        soulmates.firstTime = firstTime;

        if (firstTime)
        {
            previousSoulmates = null;
        }
        if (previousSoulmates.HasValue)
        {
            soulmates.config = previousSoulmates.Value.config;
        }
        else
        {
            soulmates.config.sharedBonk = EnableSharedBonk.Value;
            soulmates.config.sharedSlip = EnableSharedSlip.Value;
            soulmates.config.sharedExtraStaminaGain = EnableSharedExtraStaminaGain.Value;
            soulmates.config.sharedExtraStaminaUse = EnableSharedExtraStaminaUse.Value;
            soulmates.config.sharedLolli = EnableSharedLolli.Value;
            soulmates.config.sharedEnergol = EnableSharedEnergol.Value;
            soulmates.config.soulmateGroupSize = SoulmateGroupSize.Value;
            soulmates.config.soulmateStrength = SoulmateStrength.Value;
        }

        // FIXME: make sure to ignore dead soulmates...
        return soulmates;
    }
}
