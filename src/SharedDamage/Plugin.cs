using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;
using pworld.Scripts.Extensions;
using pworld.Scripts;
using System;

namespace SharedDamage;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> EnablePoison { get; private set; } = null!;

    internal const byte SHARED_DAMAGE_EVENT_CODE = 200;
    internal const byte RECALCULATE_SOULMATES_EVENT_CODE = 201;

    private static int globalSoulmate = -1;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");

        EnablePoison = Config.Bind("Shared Status Effects", "EnablePoison", true, "Share Poison damage");

        PhotonNetwork.NetworkingClient.EventReceived += OnSharedDamageEvent;
        PhotonNetwork.NetworkingClient.EventReceived += OnRecalculateSoulmateEvent;

        Harmony harmony = new("com.github.Wesmania.Soulmates");

        try
        {
            harmony.PatchAll();
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Failed to load mod: {ex}");
        }
    }

    private void OnDestroy()
    {
        if (PhotonNetwork.NetworkingClient != null)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnSharedDamageEvent;
            PhotonNetwork.NetworkingClient.EventReceived -= OnRecalculateSoulmateEvent;
        }
    }

    private void OnSharedDamageEvent(EventData photonEvent)
    {
        if (photonEvent.Code == SHARED_DAMAGE_EVENT_CODE)
        {
            object[] data = (object[])photonEvent.CustomData;
            int statusTypeInt = (int)data[0];
            float amount = (float)data[1];
            int senderActorNumber = photonEvent.Sender;

            Character localChar = Character.localCharacter;
            if (localChar == null || localChar.data.dead || localChar.warping) return;
            // Sanity check
            if (localChar.photonView.Owner.ActorNumber == senderActorNumber) return;
            if (senderActorNumber != globalSoulmate) return;

            CharacterAfflictions.STATUSTYPE statusType = (CharacterAfflictions.STATUSTYPE)statusTypeInt;
            SharedDamagePatch.isReceivingSharedDamage.Add(localChar.photonView.ViewID);

            try
            {
                localChar.refs.afflictions.AddStatus(statusType, amount);
                Log.LogInfo($"Received shared damage: {amount} {statusType}");
            }
            finally
            {
                SharedDamagePatch.isReceivingSharedDamage.Remove(localChar.photonView.ViewID);
            }
        }
    }
    private static List<int> RecalculateSoulmate()
    {
        Log.LogInfo("Recalculating soulmate");

        var actors = Character.AllCharacters.Select(c => c.photonView.Owner.ActorNumber).ToList();
        actors.Sort();

        // Lowest actor number is responsible for recalculating soulmates.
        if (actors.Count == 0 || actors[0] != Character.localCharacter.photonView.Owner.ActorNumber)
        {
            return null;
        }
        Log.LogInfo("I am the lowest numbered player, preparing new soulmate list");
        actors.Shuffle();
        return actors;
    }

    private static void OnRecalculateSoulmateEvent(EventData photonEvent)
    {
        if (photonEvent.Code == RECALCULATE_SOULMATES_EVENT_CODE)
        {
            Log.LogInfo("Received recalculate soulmate event");
            object[] data = (object[])photonEvent.CustomData;
            List<int> actors = data.Select(x => (int)x).ToList();

            var my_actor = Character.localCharacter.photonView.Owner.ActorNumber;
            var pos = actors.FindIndex(x => x == my_actor);
            if (pos == -1)
            {
                return;
            }
            Log.LogInfo(String.Format("Found my index: {0}", pos));
            var soulmate_index = pos % 2 == 0 ? pos + 1 : pos - 1;
            if (soulmate_index >= actors.Count)
            {
                return;
            }
            globalSoulmate = actors[soulmate_index];

            // Tell the player who his soulmate is. TODO: delay?
            var soulmate = Character.AllCharacters.Find(c => c.photonView.Owner.ActorNumber == globalSoulmate);
            if (soulmate == null)
            {
                Log.LogError(String.Format("Did not find soulmate with actor ID {0}!", globalSoulmate));
                return;
            }
            var name = soulmate.characterName;
            Log.LogInfo(String.Format("My soulmate is {0} (actor {1})", name, globalSoulmate));
            GUIManager.instance.SetHeroTitle("Soulmate: " + name, null);
        }
    }

    [HarmonyPatch(typeof(CharacterAfflictions))]
    public class SharedDamagePatch
    {
        internal static readonly HashSet<int> isReceivingSharedDamage = new();

        [HarmonyPostfix]
        [HarmonyPatch("AddStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
        public static void AddStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool __result)
        {
            try
            {
                if (fromRPC) return;
                if (!__instance.character.IsLocal) return;
                if (amount <= 0) return;
                if (!__result) return;
                if (__instance.character.data.dead || __instance.character.warping) return;
                if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;

                SendSharedDamageEvent(statusType, amount);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in AddStatusPostfix: {ex}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float))]
        public static void SetStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, out float __state)
        {
            __state = __instance.GetCurrentStatus(statusType);
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float))]
        public static void SetStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, float __state)
        {
            try
            {
                if (!__instance.character.IsLocal) return;
                if (__instance.character.data.dead || __instance.character.warping) return;
                if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;

                float increase = amount - __state;
                if (increase > 0)
                {
                    SendSharedDamageEvent(statusType, increase);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in SetStatusPostfix: {ex}");
            }
        }

        private static void SendSharedDamageEvent(CharacterAfflictions.STATUSTYPE statusType, float amount)
        {
            object[] content = [(int)statusType, amount];
            RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
        }

    }

    [HarmonyPatch(typeof(Character))]
    public class RecalculateSoulmatesPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("StartPassedOutOnTheBeach")]
        public static void StartPassedOutOnTheBeachPostfix(Character __instance)
        {
            Plugin.Log.LogInfo("Passed out on the beach function");
            var new_mates = Plugin.RecalculateSoulmate();
            if (new_mates != null)
            {
                SendRecalculateSoulmateEvent(new_mates);
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch("MoraleBoost", typeof(float), typeof(int))]
        public static void MoraleBoostPostfix(Character __instance, float staminaAdd, int scoutCount)
        {
            Plugin.Log.LogInfo("Morale boost function");
            var new_mates = Plugin.RecalculateSoulmate();
            if (new_mates != null)
            {
                SendRecalculateSoulmateEvent(new_mates);
            }
        }
        private static void SendRecalculateSoulmateEvent(List<int> actors)
        {
            Plugin.Log.LogInfo("Sending recalculate soulmate event...");
            object[] content = actors.Select(x => (object)x).ToArray();
            RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(Plugin.RECALCULATE_SOULMATES_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
        }
    }
}