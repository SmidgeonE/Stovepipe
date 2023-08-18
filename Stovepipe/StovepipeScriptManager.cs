using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stovepipe
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "2.0.3")]
    [BepInProcess("h3vr.exe")]
    public class StovepipeScriptManager : BaseUnityPlugin
    {
        public static ConfigEntry<float> stovepipeHandgunProb;
        public static ConfigEntry<float> stovepipeRifleProb;
        private static float _userVal;

        private void Awake()
        {
            GrabPreviousUserValue();

            stovepipeHandgunProb = Config.Bind("Probability - Stovepipe", "Handgun Probability", 0.016f, "");
            stovepipeRifleProb = Config.Bind("Probability - Stovepipe", "Rifle Probability", 0.014f, "");

            if (_userVal != 0f)
            {
                stovepipeRifleProb.Value = _userVal;
                stovepipeHandgunProb.Value = _userVal;  
            }

            Harmony.CreateAndPatchAll(typeof(HandgunPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeBase));
            Harmony.CreateAndPatchAll(typeof(ClosedBoltPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeScriptManager));
        }
        
        
        private void Start()
        {
            foreach (var o in FindObjectsOfType(typeof(Handgun)))
            {
                var handgun = (Handgun)o;
                if (handgun is null) continue;

                handgun.Slide.gameObject.AddComponent<StovepipeData>();
            }
            foreach (var o in FindObjectsOfType(typeof(ClosedBoltWeapon)))
            {
                var cb = (ClosedBolt)o;
                if (cb is null) continue;

                cb.Weapon.gameObject.AddComponent<StovepipeData>();
            }
        }


        [HarmonyPatch(typeof(Object), "Instantiate", 
            typeof(Object) )]
        [HarmonyPatch(typeof(Object), "Instantiate", 
            typeof(Object), typeof(Vector3), typeof(Quaternion))]
        [HarmonyPostfix]
        private static void AddScriptToWeaponsPatch(Object __result)
        {
            var handgun = ((GameObject)__result).GetComponent<Handgun>();
            if (handgun != null)
                handgun.Slide.gameObject.AddComponent<StovepipeData>();
            
            var cb = ((GameObject)__result).GetComponent<ClosedBoltWeapon>();
            if (cb != null)
                cb.Bolt.gameObject.AddComponent<StovepipeData>();
        }


        private void GrabPreviousUserValue()
        {
            var dir = Config.ConfigFilePath;

            if (!File.Exists(dir)) return;
            
            var data = File.ReadAllLines(dir);
            if (data[0][data[0].Length - 5] == '1') _userVal = float.Parse(data[7].Substring(13));
        }
    }
}