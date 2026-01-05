using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;

namespace Soulmates;

public static class Weight
{
    private static Dictionary<Pid, UpdateWeight> playerWeights = [];
    public static bool shouldSendWeight;

    public static void Clear()
    {
        playerWeights.Clear();
        shouldSendWeight = false;
    }
    // Returns true is weight has to be propagated.
    private static bool updateLocalWeightAndCheckIfChanged(UpdateWeight w)
    {
        Pid id = SteamComms.MyNumber();
        if (!playerWeights.ContainsKey(id))
        {
            playerWeights[id] = w;
            return true;
        }
        var old = playerWeights[id];
        playerWeights[id] = w;
        return old.weight != w.weight || old.thorns != w.thorns;
    }

    public static UpdateWeight getLocalWeight()
    {
        UpdateWeight w;
        w.weight = 0.0f;
        w.thorns = 0.0f;

        Pid id = SteamComms.MyNumber();
        if (!playerWeights.ContainsKey(id))
        {
            return w;
        }
        return playerWeights[id];
    }

    public static void OnUpdateWeightEvent(Pid sender, string json)
    {
        var weight = UpdateWeight.Deserialize(json);
        playerWeights[sender] = weight;
        if (sender == SteamComms.MyNumber()) return;

        if (Plugin.globalSoulmates.PidIsSoulmate(sender))
        {
            if (Plugin.LocalCharIsReady())
            {
                // Will recalculate shared weight
                Character.localCharacter.refs.afflictions.UpdateWeight();
            }
        }
    }

    public static UpdateWeight RecalculateSharedWeight(UpdateWeight original)
    {
        if (!Plugin.LocalCharIsReady()) return original;
        if (!Plugin.config.ReceivedConfig.HasValue) return original;    // Wait for game to start

        Character localChar = Character.localCharacter;
        var affs = localChar.refs.afflictions;

        var allSoulmates = Plugin.globalSoulmates.MySoulmateCharacters();
        float soulmateCount = allSoulmates.Count;

        UpdateWeight soulmateWeights;
        soulmateWeights.weight = 0;
        soulmateWeights.thorns = 0;

        foreach (var soulmate in allSoulmates) {
            if (!soulmate.c.isLiv())
            {
                continue; // Sanity check: don't share status of dead people
            }
            if (!playerWeights.ContainsKey(soulmate.p.id))
            {
                continue;
            }
            soulmateWeights.weight += playerWeights[soulmate.p.id].weight;
            soulmateWeights.thorns += playerWeights[soulmate.p.id].thorns;
        }

        float coeff = Plugin.config.SoulmateStrength();

        original.weight = (original.weight + soulmateWeights.weight * coeff) / (coeff * soulmateCount + 1);
        original.thorns += soulmateWeights.thorns * coeff;    // Thorns are cumulative

        return original;
    }

    private static bool ShouldSendWeight()
    {
        // Wait until the game starts...
        if (!Plugin.config.ReceivedConfig.HasValue)
        {
            return false;
        }
        bool o = shouldSendWeight;
        shouldSendWeight = false;
        return o;
    }

    public static void MaybeSendWeight()
    {
        if (Weight.ShouldSendWeight())
        {
            var w = getLocalWeight();
            Events.SendUpdateWeightEvent(w);
        }
    }

    public static float PreSetWeight(CharacterAfflictions c, CharacterAfflictions.STATUSTYPE kind, float value)
    {
        if (!c.character.IsLocal) return value;
        if (!kind.isAbsolute()) return value;

        UpdateWeight w = getLocalWeight();
        if (kind == CharacterAfflictions.STATUSTYPE.Weight)
        {
            w.weight = value;
        }
        else
        {
            w.thorns = value;
        }

        if (updateLocalWeightAndCheckIfChanged(w))
        {
            shouldSendWeight = true;
        }
        w = RecalculateSharedWeight(w);

        if (kind == CharacterAfflictions.STATUSTYPE.Weight)
        {
            return w.weight;
        }
        else
        {
            return w.thorns;
        }
    }
}
