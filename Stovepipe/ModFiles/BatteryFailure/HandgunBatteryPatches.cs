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
            if (data.IsStovepiping) return;
            if (data.isWeaponBatteryFailing) return;

            var doubleFeedData = __instance.Handgun.GetComponent<DoubleFeedData>();
            if (doubleFeedData != null && doubleFeedData.isDoubleFeeding) return;

            data.isWeaponBatteryFailing = UnityEngine.Random.Range(0f, 1f) < UserConfig.BatteryFailureProb.Value;
            data.pointOfBatteryFail = UnityEngine.Random.Range(0.05f, 0.5f);
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlidePositionPatch(HandgunSlide __instance, ref float ___m_slideZ_forward)
        {
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;
            if (data.IsStovepiping) return;

            var doubleFeedData = __instance.Handgun.GetComponent<DoubleFeedData>();
            if (doubleFeedData != null && doubleFeedData.isDoubleFeeding) return;

            if (data.isWeaponBatteryFailing)
                ___m_slideZ_forward = Mathf.Lerp(__instance.Point_Slide_Forward.localPosition.z,
                    __instance.Point_Slide_Rear.localPosition.z,
                    data.pointOfBatteryFail);
            else
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void AllowSlideForwardWhenUserForcesForward(HandgunSlide __instance, 
            ref float ___m_slideZ_forward, float ___m_slideZ_heldTarget)
        {
            if (!__instance.IsHeld) return;
            
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;
            if (!data.isWeaponBatteryFailing) return;

            // i.e. if the users hand is infront of the weapon (they have put a lot of force in moving it forward)

            if (___m_slideZ_heldTarget >= __instance.Point_Slide_Forward.localPosition.z)
            {
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
                __instance.Handgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward);
                data.isWeaponBatteryFailing = false;
            }
        }

        [HarmonyPatch(typeof(Handgun), "Fire")]
        [HarmonyPrefix]
        private static bool StopFromFiringIfBatteryFailure(Handgun __instance)
        {
            var data = __instance.Slide.GetComponent<StovepipeData>();
            if (data == null) return true;

            return !data.isWeaponBatteryFailing;
        }

        [HarmonyPatch(typeof(Handgun), "DropHammer")]
        [HarmonyPatch(typeof(Handgun), "DeCockHammer")]
        [HarmonyPrefix]
        private static bool StopHammerFallingIfNotInBattery(Handgun __instance)
        {
            var data = __instance.Slide.GetComponent<StovepipeData>();
            if (data == null) return true;
            
            return !data.isWeaponBatteryFailing;
        }

        [HarmonyPatch(typeof(FVRInteractiveObject), "ForceBreakInteraction")]
        [HarmonyPrefix]
        private static bool StopHandFallingOffSlideWhileBatteryFailure(FVRInteractiveObject __instance)
        {
            if (!(__instance is HandgunSlide slide)) return true;
            
            var data = slide.GetComponent<StovepipeData>();
            if (data == null) return true;

            return !data.isWeaponBatteryFailing;
        }
    }
}