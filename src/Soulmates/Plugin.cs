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
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace Soulmates;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> EnablePoison { get; private set; } = null!;

    internal const byte SHARED_DAMAGE_EVENT_CODE = 198;
    internal const byte RECALCULATE_SOULMATES_EVENT_CODE = 197;

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
            if (localChar == null || localChar.data.dead || localChar.warping)
            {
                Log.LogInfo("Character is missing or dead, not applying shared damage");
                return;
            }
            // Sanity check
            //if (localChar.photonView.Owner.ActorNumber == senderActorNumber) return;
            if (senderActorNumber != globalSoulmate)
            {
                Log.LogInfo(String.Format("Sender {0} not matching soulmate {1}", senderActorNumber, globalSoulmate));
                return;
            }

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
    public static List<int>? RecalculateSoulmate()
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
                Log.LogInfo(String.Format("I am last player on the list and have no soulmate"));
                SoulmateTextPatch.SetSoulmateText("Soulmate: None", 10);
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
            SoulmateTextPatch.SetSoulmateText("Soulmate: " + name, 10);
        }
    }
    public static void SendRecalculateSoulmateEvent(List<int> actors)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        object[] content = actors.Select(x => (object)x).ToArray();
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.RECALCULATE_SOULMATES_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
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
                SendSharedDamageEvent(statusType, increase);
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
    public static class RecalculateSoulmatesPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("StartPassedOutOnTheBeach")]
        public static void StartPassedOutOnTheBeachPostfix(Character __instance)
        {
            Plugin.Log.LogInfo("Passed out on the beach function");
            var new_mates = Plugin.RecalculateSoulmate();
            if (new_mates != null)
            {
                Plugin.SendRecalculateSoulmateEvent(new_mates);
            }
        }
    }

    [HarmonyPatch(typeof(StaminaBar))]
    public static class RecalculateSoulmatesPatch2
    {
        [HarmonyPostfix]
        [HarmonyPatch("PlayMoraleBoost", typeof(int))]
        public static void PlayMoraleBoostPostfix(StaminaBar __instance, int scoutCount)
        {
            Plugin.Log.LogInfo("Morale boost function");
            var new_mates = Plugin.RecalculateSoulmate();
            if (new_mates != null)
            {
                Plugin.SendRecalculateSoulmateEvent(new_mates);
            }
        }
    }

    public class TextSetter : MonoBehaviour
    {
        public void SetSoulmateText(string text, float delay)
        {
            Plugin.Log.LogInfo("In SetSoulmateText");
            StartCoroutine(TextCoroutine());
            IEnumerator TextCoroutine()
            {
                Plugin.Log.LogInfo("In SetSoulmateText coroutine");
                yield return new WaitForSeconds(delay);
                if (SoulmateTextPatch.text != null)
                {
                    Plugin.Log.LogInfo("In SetSoulmateText coroutine, set text");
                    SoulmateTextPatch.text.text = text;
                }
                yield return new WaitForSeconds(10f);
                if (SoulmateTextPatch.text != null)
                {
                    Plugin.Log.LogInfo("In SetSoulmateText coroutine, reset text");
                    SoulmateTextPatch.text.text = "";
                }
                Plugin.Log.LogInfo("In SetSoulmateText coroutine end");
            }
        }
    }

    [HarmonyPatch(typeof(GUIManager))]
    public static class SoulmateTextPatch
    {
        public static Canvas? SoulmatePrompt;
        public static TextMeshProUGUI? text;
        public static TMP_FontAsset? darumaDropOneFont;
        public static TextSetter? text_setter;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        public static void StartPostfix(GUIManager __instance)
        {
            var transform = __instance.transform;
            var textChatCanvasObj = new GameObject("SoulmatePrompt");
            textChatCanvasObj.transform.SetParent(transform, false);
            SoulmatePrompt = textChatCanvasObj.AddComponent<Canvas>();
            SoulmatePrompt.renderMode = RenderMode.ScreenSpaceCamera;

            var textChatCanvasScaler = SoulmatePrompt.gameObject.GetComponent<CanvasScaler>() ?? SoulmatePrompt.gameObject.AddComponent<CanvasScaler>();
            textChatCanvasScaler.referencePixelsPerUnit = 100;
            textChatCanvasScaler.matchWidthOrHeight = 1;
            textChatCanvasScaler.referenceResolution = new Vector2(1920, 1080);
            textChatCanvasScaler.scaleFactor = 1;
            textChatCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            textChatCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var textChatObj = new GameObject("TextChat");
            textChatObj.transform.SetParent(SoulmatePrompt.transform, false);
            text = textChatObj.AddComponent<TextMeshProUGUI>();
            text_setter = textChatObj.AddComponent<TextSetter>();
            try
            {
                darumaDropOneFont = GUIManager.instance?.itemPromptDrop?.font;
            }
            catch { }
            text.text = "";
            if (darumaDropOneFont != null)
            {
                text.font = darumaDropOneFont;
            }
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
        }

        public static void SetSoulmateText(string text, float delay)
        {
            if (text_setter != null)
            {
                text_setter.SetSoulmateText(text, delay);
            }
        }
    }
}
