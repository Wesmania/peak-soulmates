using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;

namespace Soulmates;

// Check fou soulmate death, disconnects and reconnects soulmates as necessary.
public static class ConnectSoulmate
{
    private static bool globalConnectedToSoulmate = false;
    private static ConnectToSoulmate? connectToSoulmateMe;
    private static ConnectToSoulmate? connectToSoulmateThem;

    public static bool ConnectedToSoulmateStatus()
    {
        if (!Plugin.localCharIsReady())
        {
            return false;
        }
        var soulmate = Plugin.GetSoulmate(Plugin.globalSoulmate);
        if (soulmate == null)
        {
            return false;
        }
        if (!Character.localCharacter.isLiv())
        {
            return false;
        }
        if (!soulmate.isLiv())
        {
            return false;
        }
        return true;
    }
    public static void UpdateSoulmateStatus()
    {
        if (!Plugin.localCharIsReady())
        {
            return;
        }
        bool connected_to_soulmate = ConnectedToSoulmateStatus();
        if (!connected_to_soulmate && globalConnectedToSoulmate)
        {
            DisconnectFromSoulmate();
        }
        if (connected_to_soulmate && !globalConnectedToSoulmate)
        {
            DoConnectToSoulmate();
        }
        globalConnectedToSoulmate = connected_to_soulmate;
        TryPerformConnectionToSoulmate();
    }

    private static void DisconnectFromSoulmate()
    {
        Plugin.Log.LogInfo("Disconnecting from soulmate.");
        if (!Plugin.localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        // Soulmate is dead or disconnected. Keep his burden.
        // Only set our weight and thorns to local values.
        localChar.refs.afflictions.UpdateWeight();
    }
    private static void DoConnectToSoulmate()
    {
        Plugin.Log.LogInfo("Trying to connect to soulmate.");
        if (!Plugin.localCharIsReady())
        {
            return;
        }

        ConnectToSoulmate e;
        e.from = Character.localCharacter.photonView.Owner.ActorNumber;
        e.to = Plugin.globalSoulmate;
        if (Plugin.globalSoulmate == -1)
        {
            return;
        }
        e.status = new Dictionary<CharacterAfflictions.STATUSTYPE, float>();

        foreach (CharacterAfflictions.STATUSTYPE s in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
        {
            if (s.isAbsolute() || !s.isShared())
            {
                continue;
            }
            e.status[s] = Character.localCharacter.refs.afflictions.GetCurrentStatus(s);
        }
        Events.SendConnectToSoulmateEvent(e);
        connectToSoulmateMe = e;
    }
    public static void OnConnectToSoulmate(EventData photonEvent)
    {
        if (Character.localCharacter == null)
        {
            return;
        }

        object[] data = (object[])photonEvent.CustomData;
        var c = ConnectToSoulmate.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;
        if (senderActorNumber != Plugin.globalSoulmate)
        {
            return;
        }
        if (c.from != Plugin.globalSoulmate)
        {
            return;
        }
        if (c.to != Character.localCharacter.photonView.Owner.ActorNumber)
        {
            return;
        }
        connectToSoulmateThem = c;
    }

    private static void TryPerformConnectionToSoulmate()
    {
        if (!Plugin.localCharIsReady())
        {
            return;
        }

        if (!connectToSoulmateThem.HasValue || !connectToSoulmateMe.HasValue)
        {
            return;
        }

        var me = connectToSoulmateMe.Value;
        var them = connectToSoulmateThem.Value;

        var me_id = Character.localCharacter.photonView.Owner.ActorNumber;
        var them_id = Plugin.globalSoulmate;
        if (me_id != me.from || me_id != them.to || them_id != me.to || them_id != them.to)
        {
            return;
        }

        // All is checked. Share the burden.
        connectToSoulmateMe = null;
        connectToSoulmateThem = null;

        var affs = Character.localCharacter.refs.afflictions;
        foreach (var s in me.status.Keys)
        {
            if (s.isAbsolute() || !s.isShared())
            {
                continue;
            }
            if (!them.status.ContainsKey(s))
            {
                continue;
            }
            var sum = me.status[s] + them.status[s];
            affs.SetStatus(s, sum);
        }
    }

    public static void OnNewSoulmate(bool firstTime)
    {
        globalConnectedToSoulmate = ConnectedToSoulmateStatus();
        if (firstTime)
        {
            connectToSoulmateMe = null;
            connectToSoulmateThem = null;
        }
    }
}