using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;

namespace Soulmates;

public static class Weight
{
    public static Dictionary<int, UpdateWeight> playerWeights = new Dictionary<int, UpdateWeight>();
    public static bool shouldSendWeight;

    public static void Clear()
    {
        playerWeights.Clear();
        shouldSendWeight = false;
    }
    // Returns true is weight has to be propagated.
    public static bool updateLocalWeight(UpdateWeight w)
    {
        Character localChar = Character.localCharacter;
        if (localChar == null)
        {
            return false;
        }
        int id = localChar.photonView.Owner.ActorNumber;
        if (!playerWeights.ContainsKey(id))
        {
            playerWeights[id] = w;
            return true;
        }
        var old = playerWeights[id];
        playerWeights[id] = w;
        return old.weight != w.weight || old.thorns != w.thorns;
    }

    public static UpdateWeight? getLocalWeight()
    {
        Character localChar = Character.localCharacter;
        if (localChar == null)
        {
            return null;
        }
        int id = localChar.photonView.Owner.ActorNumber;
        if (!playerWeights.ContainsKey(id))
        {
            return null;
        }
        return playerWeights[id];
    }

    public static void OnUpdateWeightEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var weight = UpdateWeight.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        playerWeights[senderActorNumber] = weight;

        if (Soulmates.ActorIsSoulmate(senderActorNumber))
        {
            if (Plugin.localCharIsReady())
            {
                // Will recalculate shared weight
                Character.localCharacter.refs.afflictions.UpdateWeight();
            }
        }
    }

    // Called after UpdateWeight.
    public static void RecalculateSharedWeight()
    {
        if (!Plugin.localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        var affs = localChar.refs.afflictions;

        float thorns = affs.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Thorns);
        float weight = affs.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Weight);

        var allSoulmates = Soulmates.SoulmateCharacters();
        float soulmateCount = allSoulmates.Count;

        UpdateWeight soulmateWeights;
        soulmateWeights.weight = 0;
        soulmateWeights.thorns = 0;

        foreach (var soulmate in allSoulmates) {
            if (!soulmate.isLiv())
            {
                continue; // Sanity check: don't share status of dead people
            }
            if (!playerWeights.ContainsKey(soulmate.photonView.Owner.ActorNumber))
            {
                continue;
            }
            soulmateWeights.weight += playerWeights[soulmate.photonView.Owner.ActorNumber].weight;
            soulmateWeights.thorns += playerWeights[soulmate.photonView.Owner.ActorNumber].thorns;
        }

        float finalWeight = (weight + soulmateWeights.weight) / (soulmateCount + 1);
        float finalThorns = thorns + soulmateWeights.thorns;    // Thorns are cumulative

        affs.SetStatus(CharacterAfflictions.STATUSTYPE.Weight, finalWeight);
        affs.SetStatus(CharacterAfflictions.STATUSTYPE.Thorns, finalThorns);
    }

    public static bool ShouldSendWeight()
    {
        bool o = shouldSendWeight;
        shouldSendWeight = false;
        return o;
    }

    public static void MaybeSendWeight()
    {
        if (Weight.ShouldSendWeight())
        {
            var w = getLocalWeight();
            if (!w.HasValue) return;

            Events.SendUpdateWeightEvent(w.Value);
        }
    }
}

[HarmonyPatch(typeof(CharacterAfflictions))]
public class WeightPatch1
{
    [HarmonyPostfix]
    [HarmonyPatch("UpdateWeight")]
    public static void UpdateWeightPostfix(CharacterAfflictions __instance)
    {
        // After updating local weight, adjust for shared weight. Setup weight update if needed.
        UpdateWeight w;

        if (Character.localCharacter == null) return;

        var aff = Character.localCharacter.refs.afflictions;
        w.weight = aff.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Weight);
        w.thorns = aff.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Thorns);
        if (Weight.updateLocalWeight(w))
        {
            Weight.shouldSendWeight = true;
        }
        Weight.RecalculateSharedWeight();
        return;
    }
}