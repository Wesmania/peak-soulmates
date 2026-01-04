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
using Sirenix.Utilities;

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
[BepInDependency("off_grid.NetworkingLibrary")]
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

    public static Soulmates globalSoulmates = new([], "None", -1);

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

        Harmony harmony = new("com.github.Wesmania.Soulmates");
        try
        {
            harmony.PatchAll();
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Failed to load mod: {ex}");
        }
        SteamComms.Awake(OnEvent);
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
        SteamComms.OnDestroy();
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
    private void OnEvent(Pid sender, SoulmateEventType eventType, string json)
    {
        switch (eventType)
        {
            case SoulmateEventType.RECALCULATE:
                OnRecalculateSoulmateEvent(sender, json);
                break;
            case SoulmateEventType.DAMAGE:
                OnSharedDamageEvent(sender, json);
                break;
            case SoulmateEventType.UPDATE_WEIGHT:
                Weight.OnUpdateWeightEvent(sender, json);
                break;
            case SoulmateEventType.SHARED_BONK:
                Bonk.OnSharedBonkEvent(sender, json);
                break;
            case SoulmateEventType.SHARED_EXTRA_STAMINA:
                StamUtil.OnSharedExtraStaminaEvent(sender, json);
                break;
            case SoulmateEventType.SHARED_AFFLICTION:
                AfflictionUtil.onSharedAfflictionEvent(sender, json);
                break;
            case SoulmateEventType.WHO_IS_MY_SOULMATES:
                TellMeMySoulmate.OnWhoIsMySoulmate(sender, json);
                break;
            default:
                return;
        }
    }
    private void OnSharedDamageEvent(Pid sender, string json)
    {
        var damage = SharedDamage.Deserialize(json);

        if (!localCharIsReady())
        {
            return;
        }
        Character localChar = Character.localCharacter;
        if (globalSoulmates == null || !globalSoulmates.PidIsSoulmate(sender))
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
        damage.value *= SoulmateProtocol.instance.GetSoulmateStrength();
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
    public static Character? GetSoulmate(Pid actor)
    {
        return SteamComms.IdToCharacter(actor);
    }

    private static void OnRecalculateSoulmateEvent(Pid sender, string json)
    {
        Log.LogInfo("Received recalculate soulmate event");
        globalSoulmates = SoulmateProtocol.instance.OnNewSoulmates(json) ?? new Soulmates();
    }

    public static RecalculateSoulmatesEvent? RecalculateSoulmate(bool firstTime)
    {
        return SoulmateProtocol.instance.PrepareNewSoulmates(firstTime);
    }
}
