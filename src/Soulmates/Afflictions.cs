using ExitGames.Client.Photon;
using HarmonyLib;
using Peak.Afflictions;

namespace Soulmates;

static class AfflictionUtil
{
    public static bool sharedLolli()
    {
        return Plugin.previousSoulmates.HasValue && Plugin.previousSoulmates.Value.config.sharedLolli;
    }
    public static bool sharedEnergol()
    {
        return Plugin.previousSoulmates.HasValue && Plugin.previousSoulmates.Value.config.sharedEnergol;
    }
    public static void onSharedAfflictionEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var affliction = SharedAffliction.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        if (!Plugin.localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        if (Plugin.globalSoulmate != senderActorNumber)
        {
            return;
        }

        Affliction a = Affliction.CreateBlankAffliction(affliction.type);
        a.totalTime = affliction.totalTime;
        AfflictionPatch.skipMessage = true;
        localChar.refs.afflictions.AddAffliction(a, false);
        AfflictionPatch.skipMessage = false;
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
        if (affliction.GetAfflictionType() == Affliction.AfflictionType.InfiniteStamina && AfflictionUtil.sharedLolli())
        {
            e.type = Affliction.AfflictionType.InfiniteStamina;
            e.totalTime = affliction.totalTime;
            Events.SendSharedAfflictionEvent(e);
        }
        if (affliction.GetAfflictionType() == Affliction.AfflictionType.FasterBoi && AfflictionUtil.sharedEnergol())
        {
            e.type = Affliction.AfflictionType.FasterBoi;
            e.totalTime = affliction.totalTime;
            Events.SendSharedAfflictionEvent(e);
        }
    }
}