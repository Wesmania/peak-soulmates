using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Voice.PUN.UtilityScripts;
using UnityEngine;

namespace Soulmates;

[HarmonyPatch(typeof(CharacterAfflictions))]
public class SharedDamagePatch
{
    internal static readonly HashSet<int> isReceivingSharedDamage = new();
    internal static readonly Dictionary<int, int> isRecursiveStatusCall = new();
    private static HashSet<float> SoulmateValues = new();
    public static void StatusPostfix(CharacterAfflictions __instance, SharedDamage _e)
    {
        try
        {
            if (!__instance.character.IsLocal) return;
            if (__instance.character.data.dead || __instance.character.warping) return;
            if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;
            var e = _e;
            if (e.type.isAbsolute() || !e.type.isShared()) return;

            if (e.type == CharacterAfflictions.STATUSTYPE.Hunger && e.kind == SharedDamageKind.ADD && e.value < 0.01f & e.value >= 0.0f)
            {
                // We handle this separately, don't spam
                return;
            }
            Events.SendSharedDamageEvent(e);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"Error in SetStatusPostfix: {ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
    public static void SetStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool pushStatus, out float __state)
    {
        __state = __instance.GetCurrentStatus(statusType);
        CharacterAfflictions.STATUSTYPE st = statusType;
        if (st.isAbsolute())
        {
            amount = Weight.PreSetWeight(__instance, statusType, amount);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
    public static void SetStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool pushStatus, float __state)
    {
        float current = __instance.GetCurrentStatus(statusType);
        float diff = current - __state;
        if (diff == 0.0f)
        {
            return;
        }
        var st = statusType;
        if (st.isAbsolute())
        {
            return;
        }

        SharedDamage e;
        e.type = statusType;
        e.value = diff;
        e.kind = SharedDamageKind.SET;
        StatusPostfix(__instance, e);
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool), typeof(bool))]
    public static void AddStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool playEffects, bool notify, out SharedDamage __state)
    {
        __state.type = statusType;
        __state.value = amount;
        __state.kind = SharedDamageKind.ADD;

        if (!isRecursiveStatusCall.ContainsKey(__instance.character.photonView.ViewID))
        {
            isRecursiveStatusCall[__instance.character.photonView.ViewID] = 0;
        }
        isRecursiveStatusCall[__instance.character.photonView.ViewID]++;
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool), typeof(bool))]
    public static void AddStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool playEffects, bool notify, SharedDamage __state)
    {
        isRecursiveStatusCall[__instance.character.photonView.ViewID]--;
        if (isRecursiveStatusCall[__instance.character.photonView.ViewID] == 0)
        {
            StatusPostfix(__instance, __state);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("SubtractStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool))]
    public static void SubtractStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool decreasedNaturally, out SharedDamage __state)
    {
        __state.type = statusType;
        __state.value = amount;
        __state.kind = SharedDamageKind.SUBTRACT;

        if (!isRecursiveStatusCall.ContainsKey(__instance.character.photonView.ViewID))
        {
            isRecursiveStatusCall[__instance.character.photonView.ViewID] = 0;
        }
        isRecursiveStatusCall[__instance.character.photonView.ViewID]++;
    }

    [HarmonyPostfix]
    [HarmonyPatch("SubtractStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool))]
    public static void SubtractStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool decreasedNaturally, SharedDamage __state)
    {
        isRecursiveStatusCall[__instance.character.photonView.ViewID]--;
        if (isRecursiveStatusCall[__instance.character.photonView.ViewID] == 0)
        {
            StatusPostfix(__instance, __state);
        }
    }
    [HarmonyPostfix]
    [HarmonyPatch("UpdateNormalStatuses")]
    public static void UpdateNormalStatusesPostfix(CharacterAfflictions __instance)
    {
        if (!__instance.photonView.IsMine) return;
        // We handle hunger locally since it's very predictable, and this way we don't spam messages all the time.
        if (!__instance.character.data.fullyConscious) return;
        foreach (var d in Plugin.globalSoulmates.MySoulmateCharacters())
        {
            var c = d.c;
            if (c.data.fullyConscious && !c.data.isSkeleton)
            {
                var h = Time.deltaTime * __instance.hungerPerSecond * Ascents.hungerRateMultiplier * Plugin.config.SoulmateStrength();
                __instance.AddStatus(CharacterAfflictions.STATUSTYPE.Hunger, h);
            }
        }
    }
}

[HarmonyPatch(typeof(Character))]
public static class RecalculateSoulmatesPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("StartPassedOutOnTheBeach")]
    public static void StartPassedOutOnTheBeachPostfix(Character __instance)
    {
        Plugin.Log.LogInfo("Passed out on the beach function");
        if (!__instance.IsLocal)
        {
            return;
        }
        var new_mates = Plugin.RecalculateSoulmate(true);
        if (new_mates.HasValue)
        {
            Events.SendRecalculateSoulmateEvent(new_mates.Value);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    public static void UpdatePostfix(Character __instance)
    {
        if (!__instance.IsLocal)
        {
            return;
        }
        ConnectSoulmate.UpdateSoulmateStatus();
        Weight.MaybeSendWeight();
    }
}

[HarmonyPatch(typeof(Campfire))]
public static class RecalculateSoulmatesPatch2
{
    [HarmonyPostfix]
    [HarmonyPatch("Light_Rpc")]
    public static void LightPostfix(Campfire __instance)
    {
        Plugin.Log.LogInfo("Campfire function");
        var new_mates = Plugin.RecalculateSoulmate(false);
        if (new_mates.HasValue)
        {
            Events.SendRecalculateSoulmateEvent(new_mates.Value);
        }
    }
}
