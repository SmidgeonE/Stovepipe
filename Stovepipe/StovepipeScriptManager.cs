using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "2.0.0")]
    [BepInProcess("h3vr.exe")]
    public class StovepipeScriptManager : BaseUnityPlugin
    {
        public static ConfigEntry<float> stovepipeProb;
        
        private void Awake()
        {
            stovepipeProb = Config.Bind("Probability - Stovepipe", "Probability", 0.016f, "");
            
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
    }
}