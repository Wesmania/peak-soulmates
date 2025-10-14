using ExitGames.Client.Photon;

namespace Soulmates;

public static class Bonk
{
    public static void OnSharedBonkEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var bonk = SharedBonk.Deserialize((string)data[1]);
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

        localChar.Fall(bonk.ragdollTime);
        localChar.AddForceAtPosition(bonk.force, bonk.contactPoint, bonk.range);
    }
} 