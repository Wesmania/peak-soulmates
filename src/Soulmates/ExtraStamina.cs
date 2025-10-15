using ExitGames.Client.Photon;
using HarmonyLib;

namespace Soulmates;

static class StamUtil
{
    public static bool sharedExtraStaminaUse()
    {
        return Plugin.previousSoulmates.HasValue && Plugin.previousSoulmates.Value.config.sharedExtraStaminaUse;
    }

    public static bool sharedExtraStaminaGain()
    {
        return Plugin.previousSoulmates.HasValue && Plugin.previousSoulmates.Value.config.sharedExtraStaminaGain;
    }
    public static bool onlySharesGain() {
        return sharedExtraStaminaGain() && !sharedExtraStaminaUse();
    }

    public static void OnSharedExtraStaminaEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var stamina = SharedExtraStamina.Deserialize((string)data[1]);
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

        StaminaPatch.skipMessage += 1;
        localChar.AddExtraStamina(stamina.diff);
        StaminaPatch.skipMessage -= 1;
    } 
}

[HarmonyPatch(typeof(Character))]
public static class StaminaPatch
{
    public static int skipMessage = 0;
    public static void SendStaminaDiff(Character __instance, float _diff)
    {
        if (!__instance.IsLocal) return;
        if (skipMessage > 0)
        {
            // We were called by message, change does not originate from us.
            return;
        }

        float diff = _diff;
        if (diff == 0.0f)
        {
            return;
        }
        if (diff < 0.0f && !StamUtil.sharedExtraStaminaUse())
        {
            return;
        }
        if (diff > 0.0f && !StamUtil.sharedExtraStaminaGain())
        {
            return;
        }

        // If only gain is enabled, we should split the stamina between soulmates.
        // Otherwise, give it to both sides since they both use the same pool.
        if (diff > 0.0f && StamUtil.onlySharesGain())
        {
            diff /= 2.0f;
        }

        SharedExtraStamina e;
        e.diff = diff;
        Events.SendSharedExtraStaminaEvent(e);
    }

    [HarmonyPrefix]
    [HarmonyPatch("UseStamina", typeof(float), typeof(bool))]
    public static void UseStaminaPrefix(Character __instance, float usage, bool useBonusStamina, out float __state)
    {
        __state = __instance.data.extraStamina;
    }

    [HarmonyPostfix]
    [HarmonyPatch("UseStamina", typeof(float), typeof(bool))]
    public static void UseStaminaSuffix(Character __instance, float usage, bool useBonusStamina, float __state)
    {
        if (!__instance.IsLocal) return;
        SendStaminaDiff(__instance, __instance.data.extraStamina - __state);
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetExtraStamina", typeof(float))]
    public static void SetExtraStaminaPrefix(Character __instance, float amt, out float __state)
    {
        __state = __instance.data.extraStamina;
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetExtraStamina", typeof(float))]
    public static void SetExtraStaminaSuffix(Character __instance, float amt, float __state)
    {
        if (!__instance.IsLocal) return;
        float diff = __instance.data.extraStamina - __state;
        if (diff > 0.0f && StamUtil.onlySharesGain())
        {
            // If only gain is shared, we should split the stamina between soulmates. Correct the gain.
            // Value is negative, so our modified AddExtraStamina won't meddle with things.
            skipMessage += 1;
            __instance.AddExtraStamina(-diff / 2.0f);
            skipMessage -= 1;

        }
        // Unhalved value, we halve it here
        SendStaminaDiff(__instance, diff);
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddExtraStamina", typeof(float))]
    public static void AddExtraStaminaPrefix(Character __instance, ref float add, out float __state)
    {
        __state = add;

        if (!__instance.IsLocal) return;
        // Careful! If we added too much, stamina will clamp.
        // Halve the value here to prevent that.
        if (add > 0.0f && StamUtil.onlySharesGain())
        {
            add /= 2.0f;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddExtraStamina", typeof(float))]
    public static void AddExtraStaminaSuffix(Character __instance, float add, float __state)
    {
        if (!__instance.IsLocal) return;
        // Pass the original diff so that our clamp won't affect the other side.
        SendStaminaDiff(__instance, __state);
    }
}

// AddAffliction for lollipops and energy drinks