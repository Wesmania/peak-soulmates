using BepInEx.Configuration;

namespace Soulmates;

public class ModConfig
{
    ConfigEntry<bool> CEnabled { get; set; } = null!;
    ConfigEntry<int> CSoulmateGroupSize { get; set; } = null!;
    ConfigEntry<float> CSoulmateStrength { get; set; } = null!;
    ConfigEntry<string> CFixedSoulmates { get; private set; } = null!;
    ConfigEntry<bool> CEnableSharedBonk { get; set; } = null!;
    ConfigEntry<bool> CEnableSharedSlip { get; set; } = null!;
    ConfigEntry<bool> CEnableSharedExtraStaminaGain { get; set; } = null!;
    ConfigEntry<bool> CEnableSharedExtraStaminaUse { get; set; } = null!;
    ConfigEntry<bool> CEnableSharedLolli { get; set; } = null!;
    ConfigEntry<bool> CEnableSharedEnergol { get; set; } = null!;

    public Config? ReceivedConfig { get; private set; } = null;

    public ModConfig() { }
    public ModConfig(ConfigFile pluginConfig)
    {
        CEnabled = pluginConfig.Bind("Config", "Enabled", true, "Enable/disable the mod with this");
        CSoulmateGroupSize = pluginConfig.Bind("Config", "SoulmateGroupSize", 2, "How many people are bound in one group. Defaults to 2.");
        CSoulmateStrength = pluginConfig.Bind("Config", "SoulmateStrength", 1.0f, "How much of soulmate's status is applied to you");
        CFixedSoulmates = Config.Bind("Config", "FixedSoulmates", "", "Fixed soulmate assignments, matched by nick. Format is \"name1,name2;name3,name4\".\nThis will match name1 with name2 and name3 with name4.");
        CEnableSharedBonk = pluginConfig.Bind("Config", "EnableSharedBonk", true, "Bonking a player bonks his soulmate too");
        CEnableSharedSlip = pluginConfig.Bind("Config", "EnableSharedSlip", true, "Slipping on something makes the soulmate slip too");
        CEnableSharedExtraStaminaGain = pluginConfig.Bind("Config",
                                                    "EnableSharedExtraStaminaGain",
                                                    true,
                                                    "Soulmates share extra stamina gained");
        CEnableSharedExtraStaminaUse = pluginConfig.Bind("Config",
                                                   "EnableSharedExtraStaminaUse",
                                                   true,
                                                   "Soulmates use a single extra stamina pool");
        CEnableSharedLolli = pluginConfig.Bind("Config",
                                         "EnableSharedLolli",
                                         true,
                                         "Soulmates share lollipop boost");
        CEnableSharedEnergol = pluginConfig.Bind("Config",
                                        "EnableSharedEnergol",
                                        true,
                                        "Soulmates share energy drink boost");
    }

    public bool Enabled() => CEnabled.Value;
    public void SetReceivedConfig(Config c)
    {
        ReceivedConfig = c;
    }
    public void ClearReceivedConfig()
    {
        ReceivedConfig = null;
    }

    public Config GetConfigToSend()
    {
        if (ReceivedConfig.HasValue)
        {
            return ReceivedConfig.Value;
        }
        return new Config
        {
            sharedBonk = CEnableSharedBonk.Value,
            sharedSlip = CEnableSharedSlip.Value,
            sharedExtraStaminaGain = CEnableSharedExtraStaminaGain.Value,
            sharedExtraStaminaUse = CEnableSharedExtraStaminaUse.Value,
            sharedLolli = CEnableSharedLolli.Value,
            sharedEnergol = CEnableSharedEnergol.Value,
            soulmateGroupSize = CSoulmateGroupSize.Value,
            soulmateStrength = CSoulmateStrength.Value
        };
    }

    public int SoulmateGroupSize()
    {
        if (!ReceivedConfig.HasValue)
        {
            Plugin.Log.LogError("SoulmateGroupSize accessed without active soulmate config!");
            return 2;
        }
        else
        {
            return ReceivedConfig.Value.soulmateGroupSize;
        }
    }
    public float SoulmateStrength()
    {
        if (!ReceivedConfig.HasValue)
        {
            Plugin.Log.LogError("SoulmateStrength accessed without active soulmate config!");
            return 1.0f;
        }
        else
        {
            return ReceivedConfig.Value.soulmateStrength;
        }
    }
    public bool SharedBonk() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedBonk;
    public bool SharedSlip() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedSlip;
    public bool SharedExtraStaminaGain() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedExtraStaminaGain;
    public bool SharedExtraStaminaUse() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedExtraStaminaUse;
    public bool SharedLolli() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedLolli;
    public bool SharedEnergol() => ReceivedConfig.HasValue && ReceivedConfig.Value.sharedEnergol;

    public bool HasFixedSoulmates()
    {
        return FixedSoulmates.Value != "";
    }
    public List<List<string>> GetFixedSoulmates()
    {
        return FixedSoulmates.Value.Split(";").ToList().Select(s => s.Split(",").ToList()).ToList();
    }
}
