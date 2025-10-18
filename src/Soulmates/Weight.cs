using System.Collections.Generic;
using ExitGames.Client.Photon;

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
    public static void OnUpdateWeightEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var weight = UpdateWeight.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        playerWeights[senderActorNumber] = weight;

        if (senderActorNumber == Plugin.soulmateNumber())
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

        var soulmate = Plugin.GetSoulmate(Plugin.soulmateNumber());
        if (soulmate == null)
        {
            return;
        }
        if (!soulmate.isLiv())
        {
            return; // Sanity check: don't share status of dead people
        }
        if (!playerWeights.ContainsKey(Plugin.soulmateNumber()))
        {
            Plugin.Log.LogInfo($"No player weight entry for soulmate {Plugin.globalSoulmate}");
            return;
        }
        var soulmateWeights = playerWeights[Plugin.soulmateNumber()];

        float finalWeight = (weight + soulmateWeights.weight) / 2;
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
}