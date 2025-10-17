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

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
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
        Log.LogInfo($"Plugin {Name} version 0.1.16 is loaded!");

        Enabled = Config.Bind("Enabled", "Enabled", true, "Enable/disable the mod with this");
        EnableSharedBonk = Config.Bind("Shared Bonk", "EnableSharedBonk", true, "Bonking a player bonks his soulmate too");
        EnableSharedSlip = Config.Bind("Shared Slip", "EnableSharedSlip", true, "Slipping on something makes the soulmate slip too");
        EnableSharedExtraStaminaGain = Config.Bind("Shared extra stamina gain",
                                                   "EnableSharedExtraStaminaGain",
                                                   true,
                                                   "Soulmates share extra stamina gained");
        EnableSharedExtraStaminaUse = Config.Bind("Shared extra stamina use",
                                                  "EnableSharedExtraStaminaUse",
                                                  true,
                                                  "Soulmates use a single extra stamina pool");
        EnableSharedLolli = Config.Bind("Shared lollipops",
                                        "EnableSharedLolli",
                                        true,
                                        "Soulmates share lollipop boost");
        EnableSharedEnergol = Config.Bind("Shared energy drinks",
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

    private void OnDestroy()
    {
        if (!Enabled.Value) return;

        if (PhotonNetwork.NetworkingClient != null)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }
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

    public static int globalSoulmate = -1;
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
            case (int)SoulmateEventType.CONNECT_TO_SOULMATE:
                ConnectSoulmate.OnConnectToSoulmate(photonEvent);
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
        // Sanity check
        //if (localChar.photonView.Owner.ActorNumber == senderActorNumber) return;
        if (senderActorNumber != globalSoulmate)
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

    private static int findSoulmate(List<int> soulmates)
    {
        var my_actor = PhotonNetwork.LocalPlayer.ActorNumber;
        var pos = soulmates.FindIndex(x => x == my_actor);
        if (pos == -1)
        {
            Log.LogInfo($"Did not find myself ({my_actor}) on soulmate list!");
            return -1;
        }
        Log.LogInfo($"Found my index: {pos}");
        var soulmate_index = pos % 2 == 0 ? pos + 1 : pos - 1;
        if (soulmate_index >= soulmates.Count)
        {
            Log.LogInfo(String.Format("I am last player on the list and have no soulmate"));
            return -1;
        }
        else
        {
            return soulmates[soulmate_index];
        }
    }

    public static Character? GetSoulmate(int actor)
    {
        try
        {
            return Character.AllCharacters.Find(c => c.photonView.Owner.ActorNumber == actor);
        } catch(Exception) {
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

        if (!e.playerStatus.ContainsKey(my_number))
        {
            localChar.refs.afflictions.UpdateWeight();
            return;
        }
        var stat = e.playerStatus[my_number];

        foreach (var s in stat.Keys)
        {
            if (s.isAbsolute() || !s.isShared())
            {
                continue;
            }
            localChar.refs.afflictions.SetStatus(s, stat[s]);
        }

        localChar.refs.afflictions.UpdateWeight();
    }

    private static void OnRecalculateSoulmateEvent(EventData photonEvent)
    {
        Log.LogInfo("Received recalculate soulmate event");
        object[] data = (object[])photonEvent.CustomData;
        var soulmates = RecalculateSoulmatesEvent.Deserialize((string)data[1]);

        previousSoulmates = soulmates;

        var oldSoulmateIndex = globalSoulmate;
        globalSoulmate = findSoulmate(soulmates.soulmates);

        if (globalSoulmate == -1)
        {
            Log.LogInfo("No soulmate");
        }
        else
        {
            Log.LogInfo($"New soulmate: {globalSoulmate}");
        }

        ConnectSoulmate.OnNewSoulmate(soulmates.firstTime);
        if (soulmates.firstTime)
        {
            // Starting game. Clear data, do nothing else.
            Weight.Clear();
        }
        else
        {
            ConnectToNewSoulmate(soulmates);
        }

        if (globalSoulmate == -1)
        {
            SoulmateTextPatch.SetSoulmateText("Soulmate: None", 10);
            return;
        }

        try
        {
            var soulmate = PhotonNetwork.PlayerList.ToList().Find(c => c.ActorNumber == globalSoulmate);
            var name = soulmate.NickName;
            Log.LogInfo(String.Format("My soulmate is {0} (actor {1})", name, globalSoulmate));
            SoulmateTextPatch.SetSoulmateText("Soulmate: " + name, 10);
        } catch (ArgumentNullException)
        {
            Log.LogError(String.Format("Uh-oh, did not find soulmate with actor ID {0}!", globalSoulmate));
        }
    }

    public static RecalculateSoulmatesEvent? RecalculateSoulmate(bool firstTime)
    {
        Log.LogInfo("Recalculating soulmate");

        Log.LogInfo(String.Format("Character count: {0}", PhotonNetwork.PlayerList.Count()));
        var actors = PhotonNetwork.PlayerList.Select(x => x.ActorNumber).ToList();
        actors.Sort();
        var all = String.Join(" ", PhotonNetwork.PlayerList.Select(x => x.ToString()));
        Log.LogInfo(($"Characters: {all}"));

        // Lowest actor number is responsible for recalculating soulmates.
        if (actors.Count == 0 || actors[0] != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return null;
        }

        Log.LogInfo("I am the lowest numbered player, preparing new soulmate list");

        actors.Shuffle();
        RecalculateSoulmatesEvent soulmates;

        soulmates.soulmates = actors;
        soulmates.firstTime = firstTime;
        soulmates.playerStatus = new Dictionary<int, Dictionary<CharacterAfflictions.STATUSTYPE, float>>();

        if (firstTime)
        {
            previousSoulmates = null;
        }
        if (previousSoulmates.HasValue) {
            soulmates.config = previousSoulmates.Value.config;
        } else {
            soulmates.config.sharedBonk = EnableSharedBonk.Value;
            soulmates.config.sharedSlip = EnableSharedSlip.Value;
            soulmates.config.sharedExtraStaminaGain = EnableSharedExtraStaminaGain.Value;
            soulmates.config.sharedExtraStaminaUse = EnableSharedExtraStaminaUse.Value;
            soulmates.config.sharedLolli = EnableSharedLolli.Value;
            soulmates.config.sharedEnergol = EnableSharedEnergol.Value;
        }

        // Fill in base values first.
        foreach (var c in Character.AllCharacters)
        {
            var d = new Dictionary<CharacterAfflictions.STATUSTYPE, float>();
            soulmates.playerStatus[c.photonView.Owner.ActorNumber] = d;
            foreach (CharacterAfflictions.STATUSTYPE s in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
            {
                if (s.isAbsolute() || !s.isShared())
                {
                    continue;
                }
                d[s] = c.refs.afflictions.GetCurrentStatus(s);
            }
        }

        if (previousSoulmates.HasValue)
        {
            // Average the values between soulmates and split their share.
            var s = previousSoulmates.Value;
            for (int i = 0; i + 1 < s.soulmates.Count; i += 2)
            {
                var s1 = s.soulmates[i];
                var s2 = s.soulmates[i + 1];
                var ps = soulmates.playerStatus;
                if (!ps.ContainsKey(s1) || !ps.ContainsKey(s2))
                {
                    continue;
                }
                var s1d = ps[s1];
                var s2d = ps[s2];
                foreach (var st in s1d.Keys)
                {
                    var avg = (s1d[st] + s2d[st]) / 2;
                    var share = avg / 2;
                    s1d[st] = share;
                    s2d[st] = share;
                }
            }
        }


        // Combine the burdens of new soulmates.
        for (int i = 0; i + 1 < soulmates.soulmates.Count; i += 2)
        {
            var s1 = soulmates.soulmates[i];
            var s2 = soulmates.soulmates[i + 1];
            var ps = soulmates.playerStatus;
            if (!ps.ContainsKey(s1) || !ps.ContainsKey(s2))
            {
                continue;
            }
            var s1d = ps[s1];
            var s2d = ps[s2];
            foreach (var st in s1d.Keys)
            {
                var sum = s1d[st] + s2d[st];
                s1d[st] = sum;
                s2d[st] = sum;
            }
        }

        // FIXME: make sure to ignore dead soulmates...
        return soulmates;
    }
}