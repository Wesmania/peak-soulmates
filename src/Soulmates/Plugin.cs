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
using System.Runtime.Serialization;
using System.Diagnostics;
using Sirenix.Utilities;
using UnityEngine.Rendering;
using AsmResolver.Patching;
using Newtonsoft.Json;

namespace Soulmates;

public static class Extensions
{
    public static bool isAbsolute(this CharacterAfflictions.STATUSTYPE t)
    {
        return t == CharacterAfflictions.STATUSTYPE.Weight || t == CharacterAfflictions.STATUSTYPE.Thorns;
    }
    public static bool isShared(this CharacterAfflictions.STATUSTYPE t)
    {
        return t != CharacterAfflictions.STATUSTYPE.Curse;
    }
    public static bool isLiv(this Character c)
    {
        return !c.data.dead && !c.warping;
    }
}

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> EnablePoison { get; private set; } = null!;

    internal const byte SHARED_DAMAGE_EVENT_CODE = 198;
    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");

        EnablePoison = Config.Bind("Shared Status Effects", "EnablePoison", true, "Share Poison damage");

        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;

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
            PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }
    }
    [Serializable]
    public struct RecalculateSoulmatesEvent
    {
        public List<int> soulmates;
        public Dictionary<int, Dictionary<CharacterAfflictions.STATUSTYPE, float>> playerStatus;
        public bool firstTime;

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
        public static RecalculateSoulmatesEvent Deserialize(string s)
        {
            return JsonConvert.DeserializeObject<RecalculateSoulmatesEvent>(s);
        }
    }

    [Serializable]
    public enum SharedDamageKind
    {
        ADD,
        SUBTRACT,
        SET
    }

    [Serializable]
    public struct SharedDamage
    {
        public CharacterAfflictions.STATUSTYPE type;
        public float value;
        public SharedDamageKind kind;
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
        public static SharedDamage Deserialize(string s)
        {
            return JsonConvert.DeserializeObject<SharedDamage>(s);
        }
    }

    [Serializable]
    public struct UpdateWeight
    {
        public float weight = 0.0f;
        public float thorns = 0.0f;

        public UpdateWeight() { }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
        public static UpdateWeight Deserialize(string s)
        {
            return JsonConvert.DeserializeObject<UpdateWeight>(s);
        }
    }
    
    [Serializable]
    public struct ConnectToSoulmate
    {
        public int from;
        public int to;
        public Dictionary<CharacterAfflictions.STATUSTYPE, float> status;
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
        public static ConnectToSoulmate Deserialize(string s)
        {
            return JsonConvert.DeserializeObject<ConnectToSoulmate>(s);
        }
    }
    enum SoulmateEventType
    {
        RECALCULATE = 0,
        DAMAGE = 1,
        UPDATE_WEIGHT = 2,
        CONNECT_TO_SOULMATE = 3
    }

    public static bool localCharIsReady()
    {
        Character localChar = Character.localCharacter;
        if (localChar == null || !localChar.isLiv())
        {
            return false;
        }
        return true;
    }

    private static int globalSoulmate = -1;
    private static bool globalConnectedToSoulmate = false;
    private static Dictionary<int, UpdateWeight> playerWeights = new Dictionary<int, UpdateWeight>();
    private static RecalculateSoulmatesEvent? previousSoulmates;
    private static ConnectToSoulmate? connectToSoulmateMe;
    private static ConnectToSoulmate? connectToSoulmateThem;

    private void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != SHARED_DAMAGE_EVENT_CODE)
        {
            return;
        }

        object[] data = (object[])photonEvent.CustomData;
        int eventType = (int)data[0];
        switch (eventType)
        {
            case (int)SoulmateEventType.RECALCULATE:
                OnRecalculateSoulmateEvent(photonEvent);
                break;
            case (int)SoulmateEventType.DAMAGE:
                OnSharedDamageEvent(photonEvent);
                break;
            case (int)SoulmateEventType.UPDATE_WEIGHT:
                OnUpdateWeightEvent(photonEvent);
                break;
            case (int)SoulmateEventType.CONNECT_TO_SOULMATE:
                OnConnectToSoulmate(photonEvent);
                break;
            default:
                return;
        }
    }
    private void OnSharedDamageEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        var damage = SharedDamage.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        if (!localCharIsReady())
        {
            Log.LogInfo("Character is missing or dead, not applying shared damage");
            return;
        }
        Character localChar = Character.localCharacter;
        // Sanity check
        //if (localChar.photonView.Owner.ActorNumber == senderActorNumber) return;
        if (senderActorNumber != globalSoulmate)
        {
            Log.LogInfo(String.Format("Sender {0} not matching soulmate {1}", senderActorNumber, globalSoulmate));
            return;
        }

        if (!damage.type.isShared())
        {
            Log.LogInfo($"Received update for non-shared damage type {damage.type}");
            return;
        }
        if (damage.type.isAbsolute())
        {
            Log.LogInfo($"Received an update request for an absolute damage type {damage.type}");
            return;
        }

        SharedDamagePatch.isReceivingSharedDamage.Add(localChar.photonView.ViewID);
        var affs = localChar.refs.afflictions;
        try
        {
            switch (damage.kind)
            {
                case SharedDamageKind.ADD:
                    affs.AddStatus(damage.type, damage.value);
                    break;
                case SharedDamageKind.SUBTRACT:
                    affs.SubtractStatus(damage.type, damage.value);
                    break;
                case SharedDamageKind.SET:
                    // It's a diff
                    float existing = affs.GetCurrentStatus(damage.type);
                    affs.SetStatus(damage.type, existing + damage.value);
                    break;
            }
        }
        finally
        {
            SharedDamagePatch.isReceivingSharedDamage.Remove(localChar.photonView.ViewID);
        }
    }

    private static int findSoulmate(List<int> soulmates)
    {
        var my_actor = PhotonNetwork.LocalPlayer.ActorNumber;
        var pos = soulmates.FindIndex(x => x == my_actor);
        if (pos == -1)
        {
            Log.LogInfo($"Did not find myself ({my_actor}) on soulmate list!");
            return -1;
        }
        Log.LogInfo($"Found my index: {pos}");
        var soulmate_index = pos % 2 == 0 ? pos + 1 : pos - 1;
        if (soulmate_index >= soulmates.Count)
        {
            Log.LogInfo(String.Format("I am last player on the list and have no soulmate"));
            return -1;
        }
        else
        {
            return soulmates[soulmate_index];
        }
    }

    private static Character? GetSoulmate(int actor)
    {
        try
        {
            return Character.AllCharacters.Find(c => c.photonView.Owner.ActorNumber == actor);
        } catch(Exception) {
            return null;
        }
    }
    private static void ConnectToNewSoulmate(RecalculateSoulmatesEvent e)
    {
        if (!localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        var my_number = localChar.photonView.Owner.ActorNumber;

        if (!e.playerStatus.ContainsKey(my_number))
        {
            UpdateWeightSafe();
            return;
        }
        var stat = e.playerStatus[my_number];

        foreach (var s in stat.Keys)
        {
            if (s.isAbsolute() || !s.isShared())
            {
                continue;
            }
            localChar.refs.afflictions.SetStatus(s, stat[s]);
        }

        UpdateWeightSafe();
    }
    private static void OnRecalculateSoulmateEvent(EventData photonEvent)
    {
        Log.LogInfo("Received recalculate soulmate event");
        object[] data = (object[])photonEvent.CustomData;
        var soulmates = RecalculateSoulmatesEvent.Deserialize((string)data[1]);

        previousSoulmates = soulmates;

        var oldSoulmateIndex = globalSoulmate;
        globalSoulmate = findSoulmate(soulmates.soulmates);

        if (globalSoulmate == -1)
        {
            Log.LogInfo("No soulmate");
        }
        else
        {
            Log.LogInfo($"New soulmate: {globalSoulmate}");
        }

        globalConnectedToSoulmate = ConnectedToSoulmateStatus();
        if (soulmates.firstTime)
        {
            // Starting game. Clear data, do nothing else.
            playerWeights.Clear();
            connectToSoulmateMe = null;
            connectToSoulmateThem = null;
        }
        else
        {
            ConnectToNewSoulmate(soulmates);
        }

        var soulmate = PhotonNetwork.PlayerList.ToList().Find(c => c.ActorNumber == globalSoulmate);
        if (soulmate == null)
        {
            Log.LogError(String.Format("Uh-oh, did not find soulmate with actor ID {0}!", globalSoulmate));
            return;
        }
        var name = soulmate.NickName;
        Log.LogInfo(String.Format("My soulmate is {0} (actor {1})", name, globalSoulmate));
        SoulmateTextPatch.SetSoulmateText("Soulmate: " + name, 10);
    }

    private static void OnUpdateWeightEvent(EventData photonEvent)
    {
        Log.LogInfo("Received recalculate weight event");
        object[] data = (object[])photonEvent.CustomData;
        var weight = UpdateWeight.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;

        var oldWeights = playerWeights.GetValueOrDefault(senderActorNumber, new UpdateWeight());
        playerWeights[senderActorNumber] = weight;

        if (senderActorNumber == globalSoulmate)
        {
            if (Plugin.localCharIsReady())
            {
                // Will recalculate shared weight
                Character.localCharacter.refs.afflictions.UpdateWeight();
            }
        }
    }

    // Called after UpdateWeight.
    public static void RecalculateSharedWeight()
    {
        if (!localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        var affs = localChar.refs.afflictions;

        float thorns = affs.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Thorns);
        float weight = affs.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Thorns);

        var soulmate = GetSoulmate(globalSoulmate);
        if (soulmate == null)
        {
            return;
        }
        if (!soulmate.isLiv())
        {
            return; // Sanity check: don't share status of dead people
        }
        if (!playerWeights.ContainsKey(globalSoulmate))
        {
            return;
        }
        var soulmateWeights = playerWeights[globalSoulmate];
        float finalWeight = (weight + soulmateWeights.weight) / 2;
        float finalThorns = (thorns + soulmateWeights.thorns) / 2;
    }
    public static RecalculateSoulmatesEvent? RecalculateSoulmate(bool firstTime)
    {
        Log.LogInfo("Recalculating soulmate");

        Log.LogInfo(String.Format("Character count: {0}", PhotonNetwork.PlayerList.Count()));
        var actors = PhotonNetwork.PlayerList.Select(x => x.ActorNumber).ToList();
        actors.Sort();
        Log.LogInfo(String.Format("Characters: {0}", PhotonNetwork.PlayerList));

        // Lowest actor number is responsible for recalculating soulmates.
        if (actors.Count == 0 || actors[0] != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return null;
        }

        Log.LogInfo("I am the lowest numbered player, preparing new soulmate list");

        actors.Shuffle();
        RecalculateSoulmatesEvent soulmates;

        soulmates.soulmates = actors;
        soulmates.firstTime = firstTime;
        soulmates.playerStatus = new Dictionary<int, Dictionary<CharacterAfflictions.STATUSTYPE, float>>();

        if (firstTime)
        {
            previousSoulmates = null;
        }

        // Fill in base values first.
        foreach (var c in Character.AllCharacters)
        {
            var d = new Dictionary<CharacterAfflictions.STATUSTYPE, float>();
            soulmates.playerStatus[c.photonView.Owner.ActorNumber] = d;
            foreach (CharacterAfflictions.STATUSTYPE s in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
            {
                if (s.isAbsolute() || !s.isShared())
                {
                    continue;
                }
                d[s] = c.refs.afflictions.GetCurrentStatus(s);
            }
        }

        if (previousSoulmates.HasValue)
        {
            // Average the values between soulmates and split their share.
            var s = previousSoulmates.Value;
            for (int i = 0; i + 1 < s.soulmates.Count; i += 2)
            {
                var s1 = s.soulmates[i];
                var s2 = s.soulmates[i + 1];
                var ps = soulmates.playerStatus;
                if (!ps.ContainsKey(s1) || !ps.ContainsKey(s2))
                {
                    continue;
                }
                var s1d = ps[s1];
                var s2d = ps[s2];
                foreach (var st in s1d.Keys)
                {
                    var avg = (s1d[st] + s2d[st]) / 2;
                    var share = avg / 2;
                    s1d[st] = share;
                    s2d[st] = share;
                }
            }
        }


        // Combine the burdens of new soulmates.
        for (int i = 0; i + 1 < soulmates.soulmates.Count; i += 2)
        {
            var s1 = soulmates.soulmates[i];
            var s2 = soulmates.soulmates[i + 1];
            var ps = soulmates.playerStatus;
            if (!ps.ContainsKey(s1) || !ps.ContainsKey(s2))
            {
                continue;
            }
            var s1d = ps[s1];
            var s2d = ps[s2];
            foreach (var st in s1d.Keys)
            {
                var sum = s1d[st] + s2d[st];
                s1d[st] = sum;
                s2d[st] = sum;
            }
        }

        // FIXME: make sure to ignore dead soulmates...
        return soulmates;
    }
    public static void SendRecalculateSoulmateEvent(RecalculateSoulmatesEvent e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        object[] content = [(int)SoulmateEventType.RECALCULATE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendSharedDamageEvent(SharedDamage e)
    {
        if (!e.type.isShared() || e.type.isAbsolute())
        {
            Log.LogInfo("$Tried to send a non-shared or absolute status type {statusType}");
            return;
        }
        Log.LogInfo($"Sending shared damage: {e.value} {e.type} {e.kind}");
        object[] content = [(int) SoulmateEventType.DAMAGE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static void SendUpdateWeightEvent(UpdateWeight e)
    {
        Log.LogInfo($"Sending weight update: weight {e.weight}, thorns {e.thorns}");
        object[] content = [(int)SoulmateEventType.UPDATE_WEIGHT, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
    public static bool ConnectedToSoulmateStatus()
    {
        if (!localCharIsReady())
        {
            return false;
        }
        var soulmate = GetSoulmate(globalSoulmate);
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
        if (!localCharIsReady())
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

    // Update weight, including shared weight, without sending a weight message.
    public static void UpdateWeightSafe()
    {
        if (!localCharIsReady())
        {
            return;
        }
        Character localChar = Character.localCharacter;
        SharedDamagePatch.isReceivingSharedDamage.Add(localChar.photonView.ViewID);
        try
        {
            localChar.refs.afflictions.UpdateWeight();
        }
        finally
        {
            SharedDamagePatch.isReceivingSharedDamage.Remove(localChar.photonView.ViewID);
        }
    }
    private static void DisconnectFromSoulmate()
    {
        Log.LogInfo("Disconnecting from soulmate.");
        if (!localCharIsReady())
        {
            return;
        }

        Character localChar = Character.localCharacter;
        // Soulmate is dead or disconnected. Keep his burden.
        // Only set our weight and thorns to local values.
        UpdateWeightSafe();
    }
    private static void DoConnectToSoulmate()
    {
        Log.LogInfo("Trying to connect to soulmate.");
        if (!localCharIsReady())
        {
            return;
        }

        ConnectToSoulmate e;
        e.from = Character.localCharacter.photonView.Owner.ActorNumber;
        e.to = globalSoulmate;
        e.status = new Dictionary<CharacterAfflictions.STATUSTYPE, float>();

        foreach (CharacterAfflictions.STATUSTYPE s in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
        {
            if (s.isAbsolute() || !s.isShared())
            {
                continue;
            }
            e.status[s] = Character.localCharacter.refs.afflictions.GetCurrentStatus(s);
        }
        SendConnectToSoulmateEvent(e);
        connectToSoulmateMe = e;
    }
    private static void OnConnectToSoulmate(EventData photonEvent)
    {
        if (Character.localCharacter == null)
        {
            return;
        }

        object[] data = (object[])photonEvent.CustomData;
        var c = ConnectToSoulmate.Deserialize((string)data[1]);
        int senderActorNumber = photonEvent.Sender;
        if (senderActorNumber != globalSoulmate)
        {
            return;
        }
        if (c.from != globalSoulmate)
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
        if (!localCharIsReady())
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
        var them_id = globalSoulmate;
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

    public static void SendConnectToSoulmateEvent(ConnectToSoulmate e)
    {
        Plugin.Log.LogInfo("Sending recalculate soulmate event...");
        object[] content = [(int)SoulmateEventType.CONNECT_TO_SOULMATE, e.Serialize()];
        RaiseEventOptions raiseEventOptions = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(Plugin.SHARED_DAMAGE_EVENT_CODE, content, raiseEventOptions, SendOptions.SendReliable);
    }
}

[HarmonyPatch(typeof(CharacterAfflictions))]
public class SharedDamagePatch
{
    internal static readonly HashSet<int> isReceivingSharedDamage = new();
    private static HashSet<float> SoulmateValues = new();
    public static void StatusPostfix(CharacterAfflictions __instance, Plugin.SharedDamage _e)
    {
        try
        {
            if (!__instance.character.IsLocal) return;
            if (__instance.character.data.dead || __instance.character.warping) return;
            if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;
            var e = _e;
            if (e.type.isAbsolute() || !e.type.isShared()) return;
            Plugin.SendSharedDamageEvent(e);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"Error in SetStatusPostfix: {ex}");
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
        float current = __instance.GetCurrentStatus(statusType);
        float diff = current - __state;
        if (diff == 0.0f)
        {
            return;
        }
        var st = statusType;
        if (st.isAbsolute())
        {
            return;
        }

        Plugin.SharedDamage e;
        e.type = statusType;
        e.value = diff;
        e.kind = Plugin.SharedDamageKind.SET;
        StatusPostfix(__instance, e);        
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
    public static void AddStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC)
    {
        Plugin.SharedDamage e;
        e.type = statusType;
        e.value = amount;
        e.kind = Plugin.SharedDamageKind.ADD;
        StatusPostfix(__instance, e);
    }

    [HarmonyPostfix]
    [HarmonyPatch("SubtractStatus", typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool))]
    public static void SubtractStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC)
    {
        Plugin.SharedDamage e;
        e.type = statusType;
        e.value = amount;
        e.kind = Plugin.SharedDamageKind.SUBTRACT;
        StatusPostfix(__instance, e);
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateWeight")]
    public static void UpdateWeightPostfix(CharacterAfflictions __instance)
    {
        // After updating weight, adjust for shared weight. Send weight update if we're not called by the mod.
        Plugin.UpdateWeight w;

        if (!Plugin.localCharIsReady()) return;
        if (isReceivingSharedDamage.Contains(__instance.character.photonView.ViewID)) return;

        var aff = Character.localCharacter.refs.afflictions;
        w.weight = aff.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Weight);
        w.thorns = aff.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Thorns);
        Plugin.SendUpdateWeightEvent(w);
        Plugin.RecalculateSharedWeight();
        return;
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
        if (!__instance.IsLocal)
        {
            return;
        }
        var new_mates = Plugin.RecalculateSoulmate(true);
        if (new_mates.HasValue)
        {
            Plugin.SendRecalculateSoulmateEvent(new_mates.Value);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    public static void UpdatePostfix(Character __instance)
    {
        if (!__instance.IsLocal)
        {
            return;
        }
        Plugin.UpdateSoulmateStatus();
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
        var new_mates = Plugin.RecalculateSoulmate(false);
        if (new_mates.HasValue)
        {
            Plugin.SendRecalculateSoulmateEvent(new_mates.Value);
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