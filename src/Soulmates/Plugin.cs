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
//[BepInDependency("off_grid.NetworkingLibrary")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    public static Soulmates globalSoulmates = new([], "None", -1);
    public static ModConfig config = new();

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} version 0.3.0 is loaded!");

        config = new(Config);
        if (!config.Enabled())
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

    private void OnDestroy()
    {
        if (!config.Enabled()) return;
        SteamComms.OnDestroy();
    }
    public static bool LocalCharIsReady()
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
        Log.LogInfo($"Inside OnEvent, {eventType} from {sender}");
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
                AfflictionUtil.OnSharedAfflictionEvent(sender, json);
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

        if (!LocalCharIsReady())
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
        damage.value *= config.SoulmateStrength();
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
                globalSoulmates = SoulmateProtocol.instance.OnNewSoulmates(json) ?? new Soulmates();
    }

    public static RecalculateSoulmatesEvent? RecalculateSoulmate(bool firstTime)
    {
        return SoulmateProtocol.instance.PrepareNewSoulmates(firstTime);
    }
}
