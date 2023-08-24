using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using Newtonsoft.Json;
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
        public static ConfigEntry<bool> isWriteToDefault;

        public static Dictionary<string, StovepipeAdjustment> Defaults;
        public static Dictionary<string, StovepipeAdjustment> UserDefs;

        private static string defaultsDir;
        private static string userDefsDir;

        private static JsonSerializerSettings ignoreSelfReference = new JsonSerializerSettings
            { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        
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

            Harmony.CreateAndPatchAll(typeof(HandgunPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeBase));
            Harmony.CreateAndPatchAll(typeof(ClosedBoltPatches));
            Harmony.CreateAndPatchAll(typeof(StovepipeScriptManager));
            
            if (isDebug.Value == true) Harmony.CreateAndPatchAll(typeof(DebugMode));

            var userDefsRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/StovepipeData/";

            userDefsDir = userDefsRoot + "userdefinitions.json";
            defaultsDir = Paths.PluginPath + "/Smidgeon-Stovepipe/plugins/defaults.json";

            if (!File.Exists(userDefsDir))
            {
                Directory.CreateDirectory(userDefsRoot);
                File.Create(userDefsDir).Dispose();
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

        public static void WriteNewAdjustment(string nameOfGun, StovepipeAdjustment adjustments)
        {
            if (isWriteToDefault.Value)
                WriteOrReplaceInDict(nameOfGun, adjustments, Defaults, defaultsDir);
            else
                WriteOrReplaceInDict(nameOfGun, adjustments, UserDefs, userDefsDir);
        }

        private static void WriteOrReplaceInDict(string nameOfGun, StovepipeAdjustment adjustment,
            IDictionary<string, StovepipeAdjustment> dict, string dictDir)
        {
            if (dict.TryGetValue(nameOfGun, out _)) dict.Remove(nameOfGun);
            dict.Add(nameOfGun, adjustment);
            File.WriteAllText(dictDir, JsonConvert.SerializeObject(dict, Formatting.Indented, ignoreSelfReference));
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
            stovepipeHandgunProb = Config.Bind("Probability - Stovepipe", "Handgun Probability", 0.008f, "");
            stovepipeRifleProb = Config.Bind("Probability - Stovepipe", "Rifle Probability", 0.004f, "");
            isDebug = Config.Bind("Debug Mode", "isActive", false, "This debug mode allows, " +
                                                                   "once both triggers are pressed upwards, " +
                                                                   "spawns a debug object that allows for manually changing the position / rotation of the bullet when its stovepiped. " +
                                                                   "Once you leave the debug mode, it will save this position and rotation so you can use it again in future.");
            isWriteToDefault = Config.Bind("Debug Mode", "writeToDefault", false,
                "Do not use this, this is for developing. " +
                "If you do, it will overwrite the defaults, which will just be overwritten when the mod is updated.");
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