using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using Unity.Mathematics;

namespace Soulmates;


// We don't want to be sending a gajillion events per second.
// For example, hunger ticks EVERY TICK. And it's some tiny value like 1e-05.
// So, batch some of the values that tick a lot (hunger, cold, hot) and only send them once they go beyond a 1% threshold.
public class EventCache
{
    public static EventCache instance = new();

    private Dictionary<(CharacterAfflictions.STATUSTYPE, SharedDamageKind), float> cache = new()
    {
        { (CharacterAfflictions.STATUSTYPE.Hunger, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hunger, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Cold, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Cold, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hot, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hot, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Poison, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Poison, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Spores, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Spores, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Drowsy, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Drowsy, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Web, SharedDamageKind.SUBTRACT), 0.0f },
    };

    private float staminaCache = 0.0f;
    public SharedDamage? cacheEvent(SharedDamage e)
    {
        var key = (e.type, e.kind);
        if (!cache.ContainsKey(key))
        {
            return e;
        }
        cache[key] += e.value;
        if (cache[key] < 0.0125001f)
        {
            return null;
        }
        e.value = cache[key];
        cache[key] = 0;
        return e;
    }

    public SharedExtraStamina? cacheStamina(SharedExtraStamina e)
    {
        if (e.diff > 0)
        {
            // Gains are okay, they are always large.
            return e;
        }
        staminaCache += e.diff;
        if (staminaCache > -0.01f)
        {
            return null;
        }
        e.diff = staminaCache;
        staminaCache = 0.0f;
        return e;
    }
}
public static class Events
{
    private static void SendEvent(SoulmateEventType eventType, string e, ReceiverGroup who, bool reliable = false)
    {
        SteamComms.SendEvent(eventType, e, who, reliable);
    }
    private static void SendEventTo(SoulmateEventType eventType, string e, Pid[] targets, bool reliable = false)
    {
        SteamComms.SendEventTo(eventType, e, targets, reliable);
    }
    private static void SendToSoulmates(SoulmateEventType eventType, string e, bool reliable = false)
    {
        if (Plugin.globalSoulmates.NoSoulmates()) return;

        SendEventTo(eventType, e, Plugin.globalSoulmates.MySoulmatePids().ToArray(), reliable);
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        SendEvent(SoulmateEventType.RECALCULATE, e.Serialize(), ReceiverGroup.All, true);
    }

    private static bool IsUselessSubtract(SharedDamage e)
    {
        return e.kind == SharedDamageKind.SUBTRACT &&
        Plugin.globalSoulmates.MySoulmateCharacters().All(c =>
        {
            var a = c.c.refs.afflictions;
            return a.GetCurrentStatus(e.type) == 0.0f && a.GetIncrementalStatus(e.type) == 0.0f;
        });
    }

    public static void SendSharedDamageEvent(SharedDamage e)
    {
        if (!e.type.isShared() || e.type.isAbsolute())
        {
            Plugin.Log.LogInfo("$Tried to send a non-shared or absolute status type {statusType}");
            return;
        }

        var e2 = EventCache.instance.cacheEvent(e);
        if (!e2.HasValue) return;
        e = e2.Value;

        // Little optimization: if we think our soulmates don't have the status, don't decrease it.
        // Statuses are synced with a delay, but this should be fine.
        if (IsUselessSubtract(e)) return;

        Plugin.Log.LogInfo($"Sending shared damage {e.type} {e.kind} {e.value}");

        bool reliable = true;
        if (e.kind != SharedDamageKind.SET && math.abs(e.value) < 0.01)
        {
            reliable = false;
        }
        EventStats.instance.CountSharedDamage(e);
        SendToSoulmates(SoulmateEventType.DAMAGE, e.Serialize(), reliable);
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        EventStats.instance.CountUpdateWeight();
        SendEvent(SoulmateEventType.UPDATE_WEIGHT, e.Serialize(), ReceiverGroup.Others, true);
    }

    public static void SendSharedBonkEvent(SharedBonk e)
    {
        EventStats.instance.CountSharedBonk();
        SendEvent(SoulmateEventType.SHARED_BONK, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedExtraStaminaEvent(SharedExtraStamina e)
    {
        var e2 = EventCache.instance.cacheStamina(e);
        if (!e2.HasValue) return;
        e = e2.Value;
        EventStats.instance.CountSharedExtraStamina();
        SendToSoulmates(SoulmateEventType.SHARED_EXTRA_STAMINA, e.Serialize());
    }
    public static void SendSharedAfflictionEvent(SharedAffliction e)
    {
        EventStats.instance.CountSharedAffliction();
        SendToSoulmates(SoulmateEventType.SHARED_AFFLICTION, e.Serialize());
    }
    public static void SendWhoIsMySoulmatesEvent()
    {
        WhoIsMySoulmate w;
        SendEvent(SoulmateEventType.WHO_IS_MY_SOULMATES, w.Serialize(), ReceiverGroup.MasterClient, true);
    }
    public static void SendThisIsYourSoulmatesEvent(RecalculateSoulmatesEvent e, Pid target)
    {
        SendEventTo(SoulmateEventType.RECALCULATE, e.Serialize(), [target], true);
    }
}

class SharedDamageRecord
{
    CharacterAfflictions.STATUSTYPE type;
    SharedDamageKind kind;

    public SharedDamageRecord(SharedDamage e)
    {
        type = e.type;
        kind = e.kind;
    }
    public override string ToString()
    {
        return $"Shared Damage({type}, {kind})";
    }
}
public class EventStats
{
    public static EventStats instance = new();
    private Dictionary<SharedDamageRecord, int> sharedDamageCounts = [];
    int updateWeightCount = 0;
    int sharedBonkCount = 0;
    int sharedExtraStaminaCount = 0;
    int sharedAfflictionCount = 0;

    public void CountSharedDamage(SharedDamage e)
    {
        var record = new SharedDamageRecord(e);
        if (!sharedDamageCounts.ContainsKey(record))
        {
            sharedDamageCounts[record] = 0;
        }
        sharedDamageCounts[record]++;
    }

    public void CountUpdateWeight() => updateWeightCount++;
    public void CountSharedBonk() => sharedBonkCount++;
    public void CountSharedExtraStamina() => sharedExtraStaminaCount++;
    public void CountSharedAffliction() => sharedAfflictionCount++;

    public void PrintStats()
    {
        Plugin.Log.LogInfo("Event stats:");
        foreach (var kvp in sharedDamageCounts)
        {
            Plugin.Log.LogInfo($"{kvp.Key}: {kvp.Value}");
        }
        Plugin.Log.LogInfo($"UpdateWeight: {updateWeightCount}");
        Plugin.Log.LogInfo($"SharedBonk: {sharedBonkCount}");
        Plugin.Log.LogInfo($"SharedExtraStamina: {sharedExtraStaminaCount}");
        Plugin.Log.LogInfo($"SharedAffliction: {sharedAfflictionCount}");
    }

    public void Reset()
    {
        sharedDamageCounts.Clear();
        updateWeightCount = 0;
        sharedBonkCount = 0;
        sharedExtraStaminaCount = 0;
        sharedAfflictionCount = 0;
    }
}