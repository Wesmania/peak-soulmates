using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;

namespace Soulmates;

public static class Weight
{
    private static Dictionary<int, UpdateWeight> playerWeights = new Dictionary<int, UpdateWeight>();
    public static bool shouldSendWeight;

    public static void Clear()
    {
        playerWeights.Clear();
        shouldSendWeight = false;
    }
    // Returns true is weight has to be propagated.
    private static bool updateLocalWeightAndCheckIfChanged(UpdateWeight w)
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

    public static UpdateWeight getLocalWeight()
    {
        UpdateWeight w;
        w.weight = 0.0f;
        w.thorns = 0.0f;

        Character localChar = Character.localCharacter;
        if (localChar == null)
        {
            return w;
        }
        int id = localChar.photonView.Owner.ActorNumber;
        if (!playerWeights.ContainsKey(id))
        {
            return w;
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

    public static UpdateWeight RecalculateSharedWeight(UpdateWeight original)
    {
        if (!Plugin.localCharIsReady())
        {
            return original;
        }

        Character localChar = Character.localCharacter;
        var affs = localChar.refs.afflictions;

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

        float coeff = Plugin.GetSoulmateStrength();

        original.weight = (original.weight + soulmateWeights.weight * coeff) / (coeff * soulmateCount + 1);
        original.thorns += soulmateWeights.thorns * coeff;    // Thorns are cumulative

        return original;
    }

    private static bool ShouldSendWeight()
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
