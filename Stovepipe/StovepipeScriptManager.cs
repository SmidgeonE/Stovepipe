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
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "2.2.0")]
    [BepInProcess("h3vr.exe")]
    public class StovepipeScriptManager : BaseUnityPlugin
    {
        public static ConfigEntry<float> stovepipeHandgunProb;
        public static ConfigEntry<float> stovepipeRifleProb;
        public static ConfigEntry<bool> isDebug;
        
        // This is the value if they are upgrading from 1.x.x
        private static float _previousUserProbability;
        private static bool _configIsFirstType;
        
        // These are the values read if they are upgrading from 2.1.x
        private static float _previousUserHandgunProb;
        private static float _previousUserRifleProb;
        private static bool _configIsSecondType;

        private void Awake()
        {
            GrabPreviousUserValue();

            stovepipeHandgunProb = Config.Bind("Probability - Stovepipe", "Handgun Probability", 0.008f, "");
            stovepipeRifleProb = Config.Bind("Probability - Stovepipe", "Rifle Probability", 0.004f, "");
            isDebug = Config.Bind("Debug Mode", "isActive", false, "This debug mode allows, " +
                                                                   "once both triggers are pressed upwards, " +
                                                                   "spawns a debug object that allows for manually changing the position / rotation of the bullet when its stovepiped. " +
                                                                   "Once you leave the debug mode, it will save this position and rotation so you can use it again in future.");

            if (_configIsFirstType)
            {
                stovepipeRifleProb.Value = _previousUserProbability;
                stovepipeHandgunProb.Value = _previousUserProbability;  
            }
            else if (_configIsSecondType)
            {
                stovepipeRifleProb.Value = _previousUserRifleProb;
                stovepipeHandgunProb.Value = _previousUserHandgunProb;
            }

            Harmony.CreateAndPatchAll(typeof(HandgunPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeBase));
            Harmony.CreateAndPatchAll(typeof(ClosedBoltPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeScriptManager));
            
            if (isDebug.Value == true) Harmony.CreateAndPatchAll(typeof(DebugMode));
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
            
            if (data[0][data[0].Length - 5] == '1')
            {
                _previousUserProbability = float.Parse(data[7].Substring(13));
                _configIsFirstType = true;
            }
            else if (data[0].Substring(57).StartsWith("2.1"))
            {

                var failedHandgun = float.TryParse(data[7].Substring(22), out _previousUserHandgunProb);
                var failedRifle = float.TryParse(data[11].Substring(20), out _previousUserRifleProb);

                if (failedHandgun || failedRifle)
                {
                    Debug.Log("Something went wrong with reading previous config file for stovepipe.");
                    Debug.Log("Giving default values");
                }
                else
                {
                    _configIsSecondType = true;
                }
            }
        }
    }
}