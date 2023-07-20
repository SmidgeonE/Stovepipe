using BepInEx;
using FistVR;
using HarmonyLib;
using UnityEngine;
using BepInEx.MonoMod.HookGenPatcher;

namespace Stovepipe
{
    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public class FailureScriptManager : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(EjectionFailure), null);
            Harmony.CreateAndPatchAll(typeof(FailureScriptManager), null);
        }

        private void Start()
        {
            foreach (var o in FindObjectsOfType(typeof(Handgun)))
            {
                var handgun = (Handgun)o;
                if (handgun is null)
                {
                    Debug.Log("Handgun found is null");
                    continue;
                }
                
                handgun.Slide.gameObject.AddComponent<SlideStovepipeData>();
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
            {
                Debug.Log("Handgun instantiated");
                handgun.Slide.gameObject.AddComponent<SlideStovepipeData>();
            }
            
            /*
            foreach (var item in ((GameObject)__result).GetComponents<Component>()) Debug.Log(" items " + item.GetType());
        */
        }
    }
}