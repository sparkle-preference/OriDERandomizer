using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using OriDeModLoader.Util;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class TransparencyAnimatorExt
    {
        // private static Lazy<AccessTools.FieldRef<TransparencyAnimator, List<TransparencyAnimator>>>
        //     m_childTransparencyAnimators =
        //         new Lazy<AccessTools.FieldRef<TransparencyAnimator, List<TransparencyAnimator>>>(() =>
        //             AccessTools.FieldRefAccess<List<TransparencyAnimator>>(typeof(TransparencyAnimator),
        //                 "m_childTransparencyAnimators"));
        //
        // private static Lazy<AccessTools.FieldRef<TransparencyAnimator, List<CleverMenuItem>>>
        //     m_cleverMenuItems =
        //         new Lazy<AccessTools.FieldRef<TransparencyAnimator, List<CleverMenuItem>>>(() =>
        //             AccessTools.FieldRefAccess<List<CleverMenuItem>>(typeof(TransparencyAnimator),
        //                 "m_cleverMenuItems")); 
        //
        // private static Lazy<AccessTools.FieldRef<TransparencyAnimator, List<Renderer>>>
        //     m_renderers =
        //         new Lazy<AccessTools.FieldRef<TransparencyAnimator, List<Renderer>>>(() =>
        //             AccessTools.FieldRefAccess<List<Renderer>>(typeof(TransparencyAnimator),
        //                 "m_renderers")); 
        //
        // private static Lazy<AccessTools.FieldRef<TransparencyAnimator, IList>> 
        //     m_rendererData =
        //         new Lazy<AccessTools.FieldRef<TransparencyAnimator, IList>>(() =>
        //             AccessTools.FieldRefAccess<IList>(typeof(TransparencyAnimator),
        //                 "m_rendererData"));

        public static void Reset(this TransparencyAnimator _this)
        {
            // m_childTransparencyAnimators.Value(_this)?.Clear();
            // m_cleverMenuItems.Value(_this)?.Clear();
            // m_renderers.Value(_this)?.Clear();
            // m_rendererData.Value(_this)?.Clear();
        }
    }
}