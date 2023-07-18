using BepInEx;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe
{
    public class FailureScriptManager : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(EjectionFailure), null);
            Harmony.CreateAndPatchAll(typeof(FailureScriptManager), null);

        }

        private void Start()
        {
            foreach (var handgun in (GameObject[])FindObjectsOfType(typeof(Handgun)))
            {
                if (handgun is null)
                {
                    Debug.Log("Handgun found is null");
                    continue;
                }

                handgun.gameObject.AddComponent<SlideStovepipeData>();
            }
        }


        [HarmonyPatch(typeof(GameObject), "Instantiate")]
        [HarmonyPostfix]
        private static void AddScriptToHandgunsPatch(Object __result)
        {
            if (__result is Handgun)
            {
                Debug.Log("Handgun instantiated");
                var weapon = (GameObject) __result;
                weapon.AddComponent<SlideStovepipeData>();
            }
        }
    }
}