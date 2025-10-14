using System.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Soulmates;

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