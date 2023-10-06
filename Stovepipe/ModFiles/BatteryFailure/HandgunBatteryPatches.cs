using System;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe.ModFiles.BatteryFailure
{
    public class HandgunBatteryPatches
    {
        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_SmackRear")]
        [HarmonyPostfix]
        private static void BatteryFailureDiceRoll(HandgunSlide __instance)
        {
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;

            data.isWeaponBatteryFailing = UnityEngine.Random.Range(0f, 1f) < UserConfig.BatteryFailureProb.Value;
            data.pointOfBatteryFail = UnityEngine.Random.Range(0f, 0.5f);
            UnityEngine.Debug.Log("weapon failed to enter battery");
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void SlidePositionPatch(HandgunSlide __instance, ref float ___m_slideZ_forward)
        {
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;

            if (data.isWeaponBatteryFailing)
                ___m_slideZ_forward = Mathf.Lerp(__instance.Point_Slide_Forward.localPosition.z,
                    __instance.Point_Slide_Rear.localPosition.z,
                    data.pointOfBatteryFail);
            else
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void AllowSlideForwardWhenUserForcesForward(HandgunSlide __instance, ref float ___m_slideZ_forward)
        {
            if (!__instance.IsHeld) return;
            
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;
            if (!data.isWeaponBatteryFailing) return;

            // i.e. if the users hand is infront of the weapon (they have put a lot of force in moving it forward)
            if (Vector3.Dot((__instance.m_hand.transform.position - __instance.Point_Slide_Forward.position),
                    __instance.transform.forward) > 0.1f)
            {
                UnityEngine.Debug.Log("stopping failure to battery");
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
            }
        }
    }
}