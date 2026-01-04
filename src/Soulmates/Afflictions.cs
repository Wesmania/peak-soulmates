using ExitGames.Client.Photon;
using HarmonyLib;
using Peak.Afflictions;

namespace Soulmates;

static class AfflictionUtil
{
    public static bool sharedLolli()
    {
        return SoulmateProtocol.instance.previousSoulmates.HasValue && SoulmateProtocol.instance.previousSoulmates.Value.config.sharedLolli;
    }
    public static bool sharedEnergol()
    {
        return SoulmateProtocol.instance.previousSoulmates.HasValue && SoulmateProtocol.instance.previousSoulmates.Value.config.sharedEnergol;
    }
    public static void onSharedAfflictionEvent(Pid sender, string json)
    {
        if (!Plugin.LocalCharIsReady()) return;
        if (!Plugin.globalSoulmates.PidIsSoulmate(sender)) return;

        var affliction = SharedAffliction.Deserialize(json);
        Character localChar = Character.localCharacter;
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