using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;

namespace Soulmates;

// Check for soulmate death, update our status as necessary.
// Right now the only thing we update is shared weight/thorns.
public static class ConnectSoulmate
{
    private static int globalConnectedSoulmateCount = 0;

    // Count is good enough, chances that 2 soulmates die/revive at the same time are super low.
    public static int ConnectedToSoulmateCount()
    {
        if (!Plugin.LocalCharIsReady())
        {
            return 0;
        }
        if (!Character.localCharacter.isLiv())
        {
            return 0;
        }
        return Plugin.globalSoulmates.LiveSoulmateCount();
    }
    public static void UpdateSoulmateStatus()
    {
        if (Character.localCharacter == null)
        {
            return;
        }
        int soulmate_count = ConnectedToSoulmateCount();
        if (soulmate_count != globalConnectedSoulmateCount)
        {
            UpdateConnectedSoulmates();
        }
    }
    private static void UpdateConnectedSoulmates()
    {
        if (!Plugin.LocalCharIsReady())
        {
            return;
        }
        Character localChar = Character.localCharacter;
        localChar.refs.afflictions.UpdateWeight();
    }
}