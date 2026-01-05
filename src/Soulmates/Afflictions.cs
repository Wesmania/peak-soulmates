using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soulmates;

static class AfflictionUtil
{
    public static void OnSharedAfflictionEvent(Pid sender, string json)
    {
        if (!Plugin.LocalCharIsReady()) return;
        if (!Plugin.globalSoulmates.PidIsSoulmate(sender)) return;

        var affliction = SharedAffliction.Deserialize(json);
        Character localChar = Character.localCharacter;
        if (affliction.type.HasValue)
        {
            Affliction a = Affliction.CreateBlankAffliction(affliction.type.Value);
            a.totalTime = affliction.totalTime;
            AfflictionPatch.skipMessage = true;
            localChar.refs.afflictions.AddAffliction(a, false);
            AfflictionPatch.skipMessage = false;
        }
        else if (affliction.other_type.HasValue)
        {
            var v = affliction.other_type.Value;
            switch (v)
            {
                case OtherAfflictions.PARALYSIS:
                case OtherAfflictions.INDIGESTION:
                    var obj = localChar.gameObject;
                    var shroomBehaviourCopy = obj.GetComponent<ShroomBehaviourCopy>() ?? obj.AddComponent<ShroomBehaviourCopy>();
                    if (v == OtherAfflictions.PARALYSIS)
                    {
                        shroomBehaviourCopy.ApplyParalysis();
                    }
                    else if (v == OtherAfflictions.INDIGESTION)
                    {
                        shroomBehaviourCopy.ApplyIndigestion();
                    }
                    break;
            }
        }
    }
}

[HarmonyPatch(typeof(CharacterAfflictions))]
public static class AfflictionPatch
{
    public static bool skipMessage = false;

    [HarmonyPostfix]
    [HarmonyPatch("AddAffliction", typeof(Affliction), typeof(bool))]
    public static void AddAfflictionPostfix(CharacterAfflictions __instance, Affliction affliction, bool fromRPC)
    {
        if (!__instance.character.IsLocal) return;
        if (!__instance.character.isLiv()) return;
        if (skipMessage) return;

        SharedAffliction e;

        Dictionary<Affliction.AfflictionType, Func<bool>> simpleTimedAfflictions = new()
        {
            { Affliction.AfflictionType.InfiniteStamina, Plugin.config.SharedLolli },
            { Affliction.AfflictionType.FasterBoi, Plugin.config.SharedEnergol },
            { Affliction.AfflictionType.Blind, Plugin.config.SharedBlindness },
            { Affliction.AfflictionType.LowGravity, Plugin.config.SharedFloating },
            { Affliction.AfflictionType.Invincibility, Plugin.config.SharedMilk },
            { Affliction.AfflictionType.Numb, Plugin.config.SharedSporedMeter },
        };

        var t = affliction.GetAfflictionType();
        if (simpleTimedAfflictions.ContainsKey(t) && simpleTimedAfflictions[t]())
        {
            e.type = t;
            e.other_type = null;
            e.totalTime = affliction.totalTime;
            Events.SendSharedAfflictionEvent(e);
        }
    }
}
public class ShroomBehaviourCopy : MonoBehaviour
{
    public void ApplyParalysis()
    {
        if (!Plugin.LocalCharIsReady()) return;
        Character localChar = Character.localCharacter;
        StartCoroutine(ParalysisCoroutine(localChar));
        IEnumerator ParalysisCoroutine(Character character)
        {
            yield return new WaitForSeconds(3f);
            character.Fall(8f);
        }
    }
    public void ApplyIndigestion()
    {
        if (!Plugin.LocalCharIsReady()) return;
        Character localChar = Character.localCharacter;
        StartCoroutine(IndigestionCoroutine(localChar));
        IEnumerator IndigestionCoroutine(Character character)
        {
            yield return new WaitForSeconds(3f);
            GameUtils.instance.SpawnResourceAtPositionNetworked("VFX_SporeExploExploEdibleSpawn", character.Center, RpcTarget.Others);
            GameUtils.instance.RPC_SpawnResourceAtPosition("VFX_SporeExploExploEdibleSpawn_NoKnockback", character.Center);
            character.AddForceToBodyPart(character.GetBodypartRig(BodypartType.Hip), Vector3.zero, Vector3.up * 100f);
        }
    }
}


[HarmonyPatch(typeof(Action_RandomMushroomEffect))]
public static class MushroomEffectPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("RunRandomEffect", typeof(int))]
    public static void RunRandomEffectPrefix(Action_RandomMushroomEffect __instance, int effect)
    {
        if (!__instance.character.IsLocal) return;

        SharedAffliction e;
        e.type = null;
        e.totalTime = 0f;
        OtherAfflictions? other_type = null;
        // Magic values taken from Action_RandomMushroomEffect
        if (effect == 5 && Plugin.config.SharedFarts())
        {
            other_type = OtherAfflictions.INDIGESTION;
        }
        if (effect == 7 && Plugin.config.SharedParalysis())
        {
            other_type = OtherAfflictions.PARALYSIS;
        }
        if (other_type.HasValue)
        {
            e.other_type = other_type.Value;
            Events.SendSharedAfflictionEvent(e);
        }
    }
}