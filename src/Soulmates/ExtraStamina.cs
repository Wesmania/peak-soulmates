using ExitGames.Client.Photon;
using HarmonyLib;

namespace Soulmates;

static class StamUtil
{
    public static float SingleStaminaMult()
    {
        return SoulmateProtocol.instance.GetSoulmateStrength();
    }

    public static bool sharedExtraStaminaUse()
    {
        return SoulmateProtocol.instance.previousSoulmates.HasValue && SoulmateProtocol.instance.previousSoulmates.Value.config.sharedExtraStaminaUse;
    }

    public static bool sharedExtraStaminaGain()
    {
        return SoulmateProtocol.instance.previousSoulmates.HasValue && SoulmateProtocol.instance.previousSoulmates.Value.config.sharedExtraStaminaGain;
    }
    public static bool onlySharesGain()
    {
        return sharedExtraStaminaGain() && !sharedExtraStaminaUse();
    }

    public static void OnSharedExtraStaminaEvent(Pid sender, string json)
    {
        var stamina = SharedExtraStamina.Deserialize(json);

        if (!Plugin.localCharIsReady()) return;
        if (!Plugin.globalSoulmates.PidIsSoulmate(sender)) return;

        Character localChar = Character.localCharacter;
        StaminaPatch.skipMessage += 1;
        localChar.AddExtraStamina(stamina.diff * SingleStaminaMult());
        StaminaPatch.skipMessage -= 1;
    }

    public static float MyStaminaGain()
    {
        var strength = SoulmateProtocol.instance.GetSoulmateStrength();
        var count = SoulmateProtocol.instance.GetSoulmateGroupSize();

        var total = 1 + strength * (count - 1);
        return 1 / total;
    }
    public static float TheirStaminaGain()
    {
        var strength = SoulmateProtocol.instance.GetSoulmateStrength();
        var count = SoulmateProtocol.instance.GetSoulmateGroupSize();

        var total = 1 + strength * (count - 1);
        return strength / total;
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
        // Otherwise, give it to the other side with a multiplier since pool is shared.
        if (diff > 0.0f)
        {
            if (StamUtil.onlySharesGain())
            {
                diff *= StamUtil.TheirStaminaGain();
            }
            else
            {
                diff *= SoulmateProtocol.instance.GetSoulmateStrength();
            }
        }

        if (diff < 0.0f)
        {
            // We share gain and loss, so stamina is fully shared.
            // Burn everyone else's stamina multiplied by strength.
            diff *= SoulmateProtocol.instance.GetSoulmateStrength();
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
            float real_val = diff * StamUtil.MyStaminaGain();
            __instance.AddExtraStamina(real_val - diff);
            skipMessage -= 1;
        }
        // Unreduced value, we reduce it on the other side
        SendStaminaDiff(__instance, diff);
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddExtraStamina", typeof(float))]
    public static void AddExtraStaminaPrefix(Character __instance, ref float add, out float __state)
    {
        __state = add;

        if (!__instance.IsLocal) return;
        // Careful! If we added too much, stamina will clamp.
        // Reduce the value here to prevent that.
        if (add > 0.0f && StamUtil.onlySharesGain())
        {
            add *= StamUtil.MyStaminaGain();
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