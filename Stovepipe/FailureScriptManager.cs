﻿using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "1.1.5")]
    [BepInProcess("h3vr.exe")]
    public class FailureScriptManager : BaseUnityPlugin
    {
        public static ConfigEntry<float> stovepipeProb;
        
        private void Awake()
        {
            stovepipeProb = Config.Bind("Probability - Stovepipe", "Probability", 0.016f, "");
            
            Harmony.CreateAndPatchAll(typeof(Stovepipe), null);
            Harmony.CreateAndPatchAll(typeof(FailureScriptManager), null);
        }

        private void Start()
        {
            foreach (var o in FindObjectsOfType(typeof(Handgun)))
            {
                var handgun = (Handgun)o;
                if (handgun is null) continue;

                handgun.Slide.gameObject.AddComponent<SlideStovepipeData>().stovepipeProb = stovepipeProb.Value;
            }
        }


        [HarmonyPatch(typeof(Object), "Instantiate", 
            typeof(Object) )]
        [HarmonyPatch(typeof(Object), "Instantiate", 
            typeof(Object), typeof(Vector3), typeof(Quaternion))]
        [HarmonyPostfix]
        private static void AddScriptToHandgunsPatch(Object __result)
        {
            var handgun = ((GameObject)__result).GetComponent<Handgun>();
            if (handgun != null)
                handgun.Slide.gameObject.AddComponent<SlideStovepipeData>().stovepipeProb = stovepipeProb.Value;
            
        }
    }
}