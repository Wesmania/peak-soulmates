using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;

namespace Soulmates;

// Check fou soulmate death, disconnects and reconnects soulmates as necessary.
public static class ConnectSoulmate
{
    private static bool globalConnectedToSoulmate = false;

    public static bool ConnectedToSoulmateStatus()
    {
        if (!Plugin.localCharIsReady())
        {
            return false;
        }
        var soulmate = Plugin.GetSoulmate(Plugin.soulmateNumber());
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
        if (!Plugin.localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        localChar.refs.afflictions.UpdateWeight();
    }
}