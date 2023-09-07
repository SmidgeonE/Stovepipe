﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using Newtonsoft.Json;
using Stovepipe.DoubleFeedPatches;
using Stovepipe.StovepipePatches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stovepipe
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "2.2.2")]
    [BepInProcess("h3vr.exe")]
    public class FailureScriptManager : BaseUnityPlugin
    {
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<bool> isWriteToDefault;

        public static ConfigEntry<bool> isStovepipeEnabled;
        public static ConfigEntry<bool> isDoubleFeedEnabled;
        
        public static ConfigEntry<float> stovepipeHandgunProb;
        public static ConfigEntry<float> stovepipeRifleProb;
        
        public static ConfigEntry<float> doubleFeedHandgunProb;
        public static ConfigEntry<float> doubleFeedRifleProb;
        public static ConfigEntry<float> lowerBulletDropoutProb;
        public static ConfigEntry<float> upperBulletDropoutProb;
        public static ConfigEntry<float> lowerBulletShakeyProb;
        public static ConfigEntry<float> upperBulletShakeyProb;

        public static Dictionary<string, StovepipeAdjustment> Defaults;
        public static Dictionary<string, StovepipeAdjustment> UserDefs;

        public static string defaultsDir;
        public static string userDefsDir;

        // This is the value if they are upgrading from 1.x.x
        private static float _previousUserProbability;
        private static bool _configIsFirstType;

        private void Awake()
        {
            GrabPreviousUserValue();
            GenerateConfigBinds();
            
            if (_configIsFirstType)
            {
                stovepipeRifleProb.Value = _previousUserProbability;
                stovepipeHandgunProb.Value = _previousUserProbability;  
            }

            ApplyPatches();

            var userDefsRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/StovepipeData/";

            userDefsDir = userDefsRoot + "userdefinitions.json";
            defaultsDir = Paths.PluginPath + "/Smidgeon-Stovepipe/defaults.json";

            if (!File.Exists(userDefsDir))
            {
                Directory.CreateDirectory(userDefsRoot);
                File.Create(userDefsDir).Dispose();
            }
            if (!File.Exists(defaultsDir))
            {
                File.Create(defaultsDir).Dispose();
            }

            Defaults =
                JsonConvert.DeserializeObject<Dictionary<string, StovepipeAdjustment>>(File.ReadAllText(defaultsDir));
            UserDefs =
                JsonConvert.DeserializeObject<Dictionary<string, StovepipeAdjustment>>(File.ReadAllText(userDefsDir));

            if (Defaults is null)
                Defaults = new Dictionary<string, StovepipeAdjustment>();
            if (UserDefs is null)
                UserDefs = new Dictionary<string, StovepipeAdjustment>();
        }

        private static void ApplyPatches()
        {
            if (isStovepipeEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(HandgunPatches));
                Harmony.CreateAndPatchAll(typeof(StovepipeBase));
                Harmony.CreateAndPatchAll(typeof(ClosedBoltPatches));
                Harmony.CreateAndPatchAll(typeof(FailureScriptManager));
            }

            if (isDoubleFeedEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(ClosedBoltDoubleFeedPatches));
                Harmony.CreateAndPatchAll(typeof(DoubleFeedBase));
            }

            if (isDebug.Value) 
                Harmony.CreateAndPatchAll(typeof(DebugMode));
        }
        
        public static StovepipeAdjustment ReadAdjustment(string rawNameOfGun)
        {
            var cleanedName = rawNameOfGun.Remove(rawNameOfGun.Length - 7);

            if (UserDefs.TryGetValue(cleanedName, out var adjustment)) return adjustment;

            Defaults.TryGetValue(cleanedName, out adjustment);

            return adjustment;
        }

        private void GenerateConfigBinds()
        {
            stovepipeHandgunProb = Config.Bind("Probability - Stovepipe", "Handgun Probability", 0.012f, "");
            stovepipeRifleProb = Config.Bind("Probability - Stovepipe", "Rifle Probability", 0.01f, "");
            doubleFeedHandgunProb = Config.Bind("Probability - Double Feed", "Handgun Probability", 0.012f, "");
            doubleFeedRifleProb = Config.Bind("Probability - Double Feed", "Rifle Probability", 0.8f, "");
            lowerBulletDropoutProb = Config.Bind("Probability - Double Feed", "lowerBulletDropoutProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out on its own accord and doesn't need the user to shake / remove the bullet manually.");
            upperBulletDropoutProb = Config.Bind("Probability - Double Feed", "upperBulletDropoutProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out on its own accord and doesn't need the user to shake / remove the bullet manually. " +
                "Note this is the probability after the lower bullet has fallen out, so the true probability of this happening is (this probability x the other probability)");
            lowerBulletShakeyProb = Config.Bind("Probability - Double Feed", "lowerBulletShakeyProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out while the user is shaking the gun.");
            upperBulletShakeyProb = Config.Bind("Probability - Double Feed", "upperBulletShakeyProbabilityy", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out while the user is shaking the gun. " +
                "Note this is the probability after the lower bullet has fallen out, so the true probability of this happening is (this probability x the other probability)");

            
            
            isDebug = Config.Bind("Debug Mode", "isActive", false, "This debug mode allows, " +
                                                                   "once both triggers are pressed upwards, " +
                                                                   "spawns a debug object that allows for manually changing the position / rotation of the bullet when its stovepiped. " +
                                                                   "Once you leave the debug mode, it will save this position and rotation so you can use it again in future.");
            isWriteToDefault = Config.Bind("Debug Mode", "writeToDefault", false,
                "Do not use this, this is for developing. " +
                "If you do, it will overwrite the defaults, which will just be overwritten when the mod is updated.");
            

            isStovepipeEnabled = Config.Bind("Activation","enableStovepipe", true,
                "Keep this to true if you want stovepipes to be simulated");
            isDoubleFeedEnabled = Config.Bind("Activation","enableDoubleFeed", true,
                "Keep this to true if you want double feeds to be simulated");
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
        }
    }
}