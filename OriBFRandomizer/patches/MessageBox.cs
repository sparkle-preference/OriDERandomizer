using System;
using System.Collections.Generic;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(MessageBox))]
    public static class MessageBoxPatches
    {
        [HarmonyPatch("RefreshText")]
        [HarmonyPrefix]
        static bool MessageBoxAlignmentPatch(MessageBox __instance, MessageDescriptor ___m_currentMessage)
        {
            if (__instance.MessageProvider && __instance.TextBox && ___m_currentMessage.Message != null)
            {
                string text = ___m_currentMessage.Message;
                if (text.StartsWith("ALIGNLEFT"))
                {
                    __instance.TextBox.alignment = CatlikeCoding.TextBox.AlignmentMode .Left;
                    text = text.Substring(9);
                }
                else if (text.StartsWith("ALIGNRIGHT"))
                {
                    __instance.TextBox.alignment = CatlikeCoding.TextBox.AlignmentMode.Right;
                    text = text.Substring(10);
                }

                if (text.StartsWith("ANCHORTOP"))
                {
                    __instance.TextBox.verticalAnchor = CatlikeCoding.TextBox.VerticalAnchorMode.Top;
                    text = text.Substring(9);
                }
                else if (text.StartsWith("ANCHORBOT"))
                {
                    __instance.TextBox.verticalAnchor = CatlikeCoding.TextBox.VerticalAnchorMode.Bottom;
                    text = text.Substring(9);
                }

                if (text.StartsWith("ANCHORLEFT"))
                {
                    __instance.TextBox.horizontalAnchor = CatlikeCoding.TextBox.HorizontalAnchorMode.Left;
                    text = text.Substring(10);
                }
                else if (text.StartsWith("ANCHORRIGHT"))
                {
                    __instance.TextBox.horizontalAnchor = CatlikeCoding.TextBox.HorizontalAnchorMode.Right;
                    text = text.Substring(11);
                }

                if (text.StartsWith("PADDING"))
                {
                    Queue<string> queue = new Queue<string>(text.Split(new char[]
                    {
                        '_'
                    }));
                    queue.Dequeue();
                    __instance.TextBox.paddingBottom = float.Parse(queue.Dequeue());
                    __instance.TextBox.paddingLeft = float.Parse(queue.Dequeue());
                    __instance.TextBox.paddingRight = float.Parse(queue.Dequeue());
                    __instance.TextBox.paddingTop = float.Parse(queue.Dequeue());
                    text = string.Join("_", queue.ToArray());
                }

                if (text.StartsWith("PARAMS"))
                {
                    Queue<string> queue2 = new Queue<string>(text.Split(new char[]
                    {
                        '_'
                    }));
                    queue2.Dequeue();
                    __instance.TextBox.maxHeight = float.Parse(queue2.Dequeue());
                    __instance.TextBox.width = float.Parse(queue2.Dequeue());
                    __instance.TextBox.TabSize = float.Parse(queue2.Dequeue());
                    text = string.Join("_", queue2.ToArray());
                }

                if (text.StartsWith("SHOWINFO"))
                {
                    text = string.Concat(new string[]
                    {
                        text.Substring(8),
                        "\nHeight: ",
                        __instance.TextBox.maxHeight.ToString(),
                        " width: ",
                        __instance.TextBox.width.ToString(),
                        "TabSize ",
                        __instance.TextBox.size.ToString(),
                        "\n Anchors ",
                        __instance.TextBox.horizontalAnchor.ToString(),
                        " ",
                        __instance.TextBox.verticalAnchor.ToString(),
                        "\nPadding: ",
                        __instance.TextBox.paddingBottom.ToString(),
                        "/",
                        __instance.TextBox.paddingLeft.ToString(),
                        "/",
                        __instance.TextBox.paddingRight.ToString(),
                        "/",
                        __instance.TextBox.paddingTop.ToString()
                    });
                }
            }

            return true;
        }
    }
}