using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Stovepipe.Debug;
using Stovepipe.DoubleFeedPatches;
using Stovepipe.StovepipePatches;

namespace Stovepipe.ModFiles
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "3.0.0")]
    [BepInProcess("h3vr.exe")]
    public class FailureScriptManager : BaseUnityPlugin
    {
        public static string DefaultsDir;
        public static string UserDefsDir;

        // This is the value if they are upgrading from 1.x.x
        private static float _previousUserProbability;
        
        private static bool _configIsFirstType;

        private void Awake()
        {
            GrabPreviousUserValue();
            GenerateConfigBinds();
            RemoveOldProbabilityFromConfig();
            
            if (_configIsFirstType)
            {
                UserConfig.StovepipeRifleProb.Value = _previousUserProbability;
                UserConfig.StovepipeHandgunProb.Value = _previousUserProbability;  
            }

            ApplyPatches();
            GrabStovepipeAdjustments();
        }

        private static void GrabStovepipeAdjustments()
        {
            var userDefsRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/StovepipeData/";

            UserDefsDir = userDefsRoot + "userdefinitions.json";
            DefaultsDir = Paths.PluginPath + "/Smidgeon-Stovepipe/defaults.json";

            if (!File.Exists(UserDefsDir))
            {
                Directory.CreateDirectory(userDefsRoot);
                File.Create(UserDefsDir).Dispose();
            }
            if (!File.Exists(DefaultsDir))
            {
                File.Create(DefaultsDir).Dispose();
            }

            UserConfig.Defaults =
                JsonConvert.DeserializeObject<Dictionary<string, StovepipeAdjustment>>(File.ReadAllText(DefaultsDir));
            UserConfig.UserDefs =
                JsonConvert.DeserializeObject<Dictionary<string, StovepipeAdjustment>>(File.ReadAllText(UserDefsDir));

            if (UserConfig.Defaults is null)
                UserConfig.Defaults = new Dictionary<string, StovepipeAdjustment>();
            if (UserConfig.UserDefs is null)
                UserConfig.UserDefs = new Dictionary<string, StovepipeAdjustment>();
        }

        private static void ApplyPatches()
        {

            if (UserConfig.IsStovepipeEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(HandgunStovepipePatches));
                Harmony.CreateAndPatchAll(typeof(StovepipeBase));
                Harmony.CreateAndPatchAll(typeof(ClosedBoltStovepipePatches));
                Harmony.CreateAndPatchAll(typeof(TubeFedStovepipePatches));
                Harmony.CreateAndPatchAll(typeof(OpenBoltStovepipePatches));
            }

            if (UserConfig.IsDoubleFeedEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(ClosedBoltDoubleFeedPatches));
                Harmony.CreateAndPatchAll(typeof(DoubleFeedBase));
                Harmony.CreateAndPatchAll(typeof(HandgunDoubleFeedPatches));
            }

            if (UserConfig.IsDebug.Value)
            {
                Harmony.CreateAndPatchAll(typeof(DebugMode));
                Harmony.CreateAndPatchAll(typeof(ClosedBoltDebug));
                Harmony.CreateAndPatchAll(typeof(HandgunDebug));
                Harmony.CreateAndPatchAll(typeof(TubeFedShotgunDebug));
                Harmony.CreateAndPatchAll(typeof(OpenBoltDebug));
            }
        }
        
        public static StovepipeAdjustment ReadAdjustment(string rawNameOfGun)
        {
            var cleanedName = rawNameOfGun.Remove(rawNameOfGun.Length - 7);

            if (UserConfig.UserDefs.TryGetValue(cleanedName, out var adjustment)) return adjustment;

            UserConfig.Defaults.TryGetValue(cleanedName, out adjustment);

            return adjustment;
        }

        private void GenerateConfigBinds()
        {
            UserConfig.StovepipeHandgunProb = Config.Bind("Probability - Stovepipe", "Handgun Probability", 0.012f, "");
            UserConfig.StovepipeRifleProb = Config.Bind("Probability - Stovepipe", "Rifle Probability", 0.01f, "");
            UserConfig.StovepipeTubeFedProb = Config.Bind("Probability - Stovepipe", "Tube Fed Shotgun Probability", 0.01f, "");
            UserConfig.StovepipeOpenBoltProb = Config.Bind("Probability - Stovepipe", "Open Bolt Probability", 0.01f, "");
            
            UserConfig.DoubleFeedHandgunProb = Config.Bind("Probability - Double Feed", "Handgun Probability", 0.008f, "");
            UserConfig.DoubleFeedRifleProb = Config.Bind("Probability - Double Feed", "Rifle Probability", 0.003f, "");
            
            UserConfig.LowerBulletDropoutProb = Config.Bind("Probability - Double Feed", "lowerBulletDropoutProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out on its own accord and doesn't need the user to shake / remove the bullet manually.");
            UserConfig.UpperBulletDropoutProb = Config.Bind("Probability - Double Feed", "upperBulletDropoutProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out on its own accord and doesn't need the user to shake / remove the bullet manually. " +
                "Note this is the probability after the lower bullet has fallen out, so the true probability of this happening is (this probability x the other probability)");
            UserConfig.LowerBulletShakeyProb = Config.Bind("Probability - Double Feed", "lowerBulletShakeyProbability", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out while the user is shaking the gun.");
            UserConfig.UpperBulletShakeyProb = Config.Bind("Probability - Double Feed", "upperBulletShakeyProbabilityy", 0.5f, "This is the probability that, when the bolt is held back, the bullet falls out while the user is shaking the gun. " +
                "Note this is the probability after the lower bullet has fallen out, so the true probability of this happening is (this probability x the other probability)");

            
            
            UserConfig.IsDebug = Config.Bind("Debug Mode", "isActive", false, "This debug mode allows, " +
                                                                              "once both triggers are pressed upwards, " +
                                                                              "spawns a debug object that allows for manually changing the position / rotation of the bullet when its stovepiped. " +
                                                                              "Once you leave the debug mode, it will save this position and rotation so you can use it again in future.");
            UserConfig.IsWriteToDefault = Config.Bind("Debug Mode", "writeToDefault", false,
                "Do not use this, this is for developing. " +
                "If you do, it will overwrite the defaults, which will just be overwritten when the mod is updated.");
            

            UserConfig.IsStovepipeEnabled = Config.Bind("Activation","enableStovepipe", true,
                "Keep this to true if you want stovepipes to be simulated");
            UserConfig.IsDoubleFeedEnabled = Config.Bind("Activation","enableDoubleFeed", true,
                "Keep this to true if you want double feeds to be simulated");
            
            UserConfig.UseProbabilityCreep = Config.Bind("Quality Of Life","Use Probability Creep", true,
                "Sometimes, it feels like you get too many stovepipes at one time. This, set to true, should remedy this feeling.");
            UserConfig.MinRoundBeforeNextJam = Config.Bind("Quality Of Life","Minimum Rounds Before Next Jam", 5,
                "If you find the bullets seem to jam to frequently together, this can be tuned to reduce frustration.");
            UserConfig.ProbabilityCreepNumRounds = Config.Bind("Quality Of Life","Number Of Rounds Before Probability Is ˜Fully Charged˜", 30,
                "This number changes how many bullets before the probability of a jam returns back to normal. " +
                "Note it will slowly increase throughout this range. This should make it much more unlikely that you will get a jam right after another jam. " +
                "Requires 'Use Probability Creep' to be set to true.");
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

        private void RemoveOldProbabilityFromConfig()
        {
            var dir = Config.ConfigFilePath;

            if (!File.Exists(dir)) return;

            var data = File.ReadAllLines(dir);

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].Length < 14) continue;

                if (data[i].Substring(0, 14) == "Probability = ")
                {
                    data[i] = "";
                    break;
                }
            }
            
            File.WriteAllLines(dir, data);
        }
    }

    public enum WeaponType
    {
        Handgun,
        Rifle,
        TubeFedShotgun,
        OpenBolt
    }
}