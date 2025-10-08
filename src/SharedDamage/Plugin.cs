using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace SharedDamage;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin {
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> EnablePoison { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableInjury { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableThorns { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableCold { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableCurse { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableDrowsy { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableHot { get; private set; } = null!;
    
    internal const byte SHARED_DAMAGE_EVENT_CODE = 199;
    
    private void Awake() {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");
        
        EnablePoison = Config.Bind("Shared Status Effects", "EnablePoison", true, "Share Poison damage");
        EnableInjury = Config.Bind("Shared Status Effects", "EnableInjury", true, "Share Injury damage");
        EnableThorns = Config.Bind("Shared Status Effects", "EnableThorns", true, "Share Thorns damage");
        EnableCold = Config.Bind("Shared Status Effects", "EnableCold", true, "Share Cold damage");
        EnableCurse = Config.Bind("Shared Status Effects", "EnableCurse", false, "Share Curse damage");
        EnableDrowsy = Config.Bind("Shared Status Effects", "EnableDrowsy", true, "Share Drowsy damage");
        EnableHot = Config.Bind("Shared Status Effects", "EnableHot", true, "Share Hot damage");
        
        PhotonNetwork.NetworkingClient.EventReceived += OnSharedDamageEvent;
        
        Harmony harmony = new("com.github.Ryocery.SharedDamage");
        
        try {
            harmony.PatchAll();
        } catch (System.Exception ex) {
            Log.LogError($"Failed to load mod: {ex}");
        }
    }
    
    private void OnDestroy() {
        if (PhotonNetwork.NetworkingClient != null) {
            PhotonNetwork.NetworkingClient.EventReceived -= OnSharedDamageEvent;
        }
    }
    
    private void OnSharedDamageEvent(EventData photonEvent) {
        if (photonEvent.Code == SHARED_DAMAGE_EVENT_CODE) {
            object[] data = (object[]) photonEvent.CustomData;
            int statusTypeInt = (int) data[0];
            float amount = (float) data[1];
            int senderActorNumber = photonEvent.Sender;
        
            Character localChar = Character.localCharacter;
            if (localChar == null || localChar.data.dead || localChar.warping) return;
            if (localChar.photonView.Owner.ActorNumber == senderActorNumber) return;
        
            CharacterAfflictions.STATUSTYPE statusType = (CharacterAfflictions.STATUSTYPE)statusTypeInt;
            SharedDamagePatch.isReceivingSharedDamage.Add(localChar.photonView.ViewID);
            
            try {
                localChar.refs.afflictions.AddStatus(statusType, amount);
                Log.LogInfo($"Received shared damage: {amount} {statusType}");
            } finally {
                SharedDamagePatch.isReceivingSharedDamage.Remove(localChar.photonView.ViewID);
            }
        }
    }
}

[HarmonyPatch(typeof(CharacterAfflictions))]
public class SharedDamagePatch {
    internal static readonly HashSet<int> isReceivingSharedDamage = new();

    [HarmonyPostfix]
    [HarmonyPatch("AddStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
    public static void AddStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool __result) {
        try {
            if (fromRPC) return;
            if (!__instance.character.IsLocal) return;
            if (amount <= 0) return;
            if (!__result) return;
            if (__instance.character.data.dead || __instance.character.warping) return;
            if (!ShouldPropagate(statusType)) return;
            if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;
            
            SendSharedDamageEvent(statusType, amount);
        } catch (System.Exception ex) {
            Plugin.Log.LogError($"Error in AddStatusPostfix: {ex}");
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float))]
    public static void SetStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, out float __state) {
        __state = __instance.GetCurrentStatus(statusType);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float))]
    public static void SetStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, float __state) {
        try {
            if (!__instance.character.IsLocal) return;
            if (__instance.character.data.dead || __instance.character.warping) return;
            if (!ShouldPropagate(statusType)) return;
            if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;
            
            float increase = amount - __state;
            if (increase > 0) {
                SendSharedDamageEvent(statusType, increase);
            }
        } catch (System.Exception ex) {
            Plugin.Log.LogError($"Error in SetStatusPostfix: {ex}");
        }
    }

    private static bool ShouldPropagate(CharacterAfflictions.STATUSTYPE statusType) {
        return statusType switch {
            CharacterAfflictions.STATUSTYPE.Poison => Plugin.EnablePoison.Value,
            CharacterAfflictions.STATUSTYPE.Injury => Plugin.EnableInjury.Value,
            CharacterAfflictions.STATUSTYPE.Thorns => Plugin.EnableThorns.Value,
            CharacterAfflictions.STATUSTYPE.Cold => Plugin.EnableCold.Value,
            CharacterAfflictions.STATUSTYPE.Curse => Plugin.EnableCurse.Value,
            CharacterAfflictions.STATUSTYPE.Drowsy => Plugin.EnableDrowsy.Value,
            CharacterAfflictions.STATUSTYPE.Hot => Plugin.EnableHot.Value,
            _ => false
        };
    }
    
    private static void SendSharedDamageEvent(CharacterAfflictions.STATUSTYPE statusType, float amount) {
        object[] content = [(int) statusType, amount];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
}