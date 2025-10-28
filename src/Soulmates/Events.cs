using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace Soulmates;


// We don't want to be sending a gajillion events per second.
// For example, hunger ticks EVERY TICK. And it's some tiny value like 1e-05.
// So, batch some of the values that tick a lot (hunger, cold, hot) and only send them once they go beyond a 1% threshold.
public class EventCache
{
    public static EventCache instance = new EventCache();

    private Dictionary<(CharacterAfflictions.STATUSTYPE, SharedDamageKind), float> cache = new Dictionary<(CharacterAfflictions.STATUSTYPE, SharedDamageKind), float>
    {
        { (CharacterAfflictions.STATUSTYPE.Hunger, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hunger, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Cold, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Cold, SharedDamageKind.SUBTRACT), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hot, SharedDamageKind.ADD), 0.0f },
        { (CharacterAfflictions.STATUSTYPE.Hot, SharedDamageKind.SUBTRACT), 0.0f },
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
        if (cache[key] < 0.01f)
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
    private static void SendEvent(SoulmateEventType eventType, string e, ReceiverGroup who, bool reliable = true)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { Receivers = who };
        var r = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, r);
    }
    private static void SendEventTo(SoulmateEventType eventType, string e, int[] targets, bool reliable = true)
    {
        object[] content = [(int)eventType, e];
        RaiseEventOptions raiseEventOptions = new() { TargetActors = targets };
        var r = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, r);
    }
    private static void SendToSoulmates(SoulmateEventType eventType, string e, bool reliable = true)
    {
        if (Soulmates.NoSoulmates()) return;

        SendEventTo(eventType, e, Soulmates.SoulmateNumbers().ToArray(), reliable);
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        SendEvent(SoulmateEventType.RECALCULATE, e.Serialize(), ReceiverGroup.All);
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

        bool reliable = true;
        if (e.kind != SharedDamageKind.SET && e.value < 0.01)
        {
            reliable = false;
        }
        SendToSoulmates(SoulmateEventType.DAMAGE, e.Serialize(), reliable);
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        SendEvent(SoulmateEventType.UPDATE_WEIGHT, e.Serialize(), ReceiverGroup.Others);
    }

    public static void SendSharedBonkEvent(SharedBonk e)
    {
        Plugin.Log.LogInfo($"Sending bonk {e.victim} {e.ragdollTime} {e.force} {e.contactPoint} {e.range}");
        SendEvent(SoulmateEventType.SHARED_BONK, e.Serialize(), ReceiverGroup.All);
    }
    public static void SendSharedExtraStaminaEvent(SharedExtraStamina e)
    {
        var e2 = EventCache.instance.cacheStamina(e);
        if (!e2.HasValue) return;
        e = e2.Value;
        SendToSoulmates(SoulmateEventType.SHARED_EXTRA_STAMINA, e.Serialize());
    }
    public static void SendSharedAfflictionEvent(SharedAffliction e)
    {
        SendToSoulmates(SoulmateEventType.SHARED_AFFLICTION, e.Serialize());
    }
    public static void SendWhoIsMySoulmatesEvent()
    {
        WhoIsMySoulmate w;
        SendEvent(SoulmateEventType.WHO_IS_MY_SOULMATES, w.Serialize(), ReceiverGroup.Others);
    }
    public static void SendThisIsYourSoulmatesEvent(RecalculateSoulmatesEvent e, int target)
    {
        SendEventTo(SoulmateEventType.RECALCULATE, e.Serialize(), [target]);
    }
}
