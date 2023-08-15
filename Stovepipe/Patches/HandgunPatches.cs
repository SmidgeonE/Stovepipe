using System;
using System.Data.Common;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using FistVR;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;


namespace Stovepipe
{
    public class HandgunPatches : StovepipeBase
    {

        [HarmonyPatch(typeof(Handgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(Handgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var handgunTransform = __instance.transform;
            var slideData = __instance.Slide.GetComponent(typeof(StovepipeData)) as StovepipeData;

            if (slideData is null)
            {
                __instance.Slide.gameObject.AddComponent(typeof(StovepipeData));
                return true;
            }

            slideData.ejectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                handgunTransform.right * __instance.RoundEjectionSpeed.x 
                + handgunTransform.up * __instance.RoundEjectionSpeed.y 
                + handgunTransform.forward * __instance.RoundEjectionSpeed.z, 
                handgunTransform.right * __instance.RoundEjectionSpin.x 
                + handgunTransform.up * __instance.RoundEjectionSpin.y 
                + handgunTransform.forward * __instance.RoundEjectionSpin.z, 
                __instance.RoundPos_Ejection.position, __instance.RoundPos_Ejection.rotation, 
                false);

            if (slideData.ejectedRound is null) return false;

            var bulletDataHolder = slideData.ejectedRound.gameObject.AddComponent<BulletStovepipeData>();
            bulletDataHolder.data = slideData;
            
            slideData.bulletCollider = slideData.ejectedRound.GetComponent<CapsuleCollider>();

            if (slideData.bulletCollider is null) return false;

            slideData.ejectedRoundRadius = slideData.bulletCollider.radius;
            slideData.ejectedRoundHeight = slideData.bulletCollider.height;
            
            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipeDiceroll(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (slideData is null) return;
            if (__instance.IsHeld) return;
            
            var handgun = __instance.Handgun;

            if (!handgun.Chamber.IsFull) return;
            if (!handgun.Chamber.IsSpent) return;
            if (handgun.Chamber.GetRound().IsCaseless) return;
            if (__instance.HasLastRoundSlideHoldOpen)
            {
                if (handgun.Magazine == null) return;
                if (handgun.Magazine.m_numRounds == 0) return;
            }

            slideData.IsStovepiping = Random.Range(0f, 1f) < slideData.stovepipeProb;
            slideData.hasBulletBeenStovepiped = false;
        }


        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlideAndBulletUpdate(HandgunSlide __instance, ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (slideData is null) return;

            if (!slideData.hasCollectedWeaponCharacteristics)
            {
                slideData.defaultFrontPosition = ___m_slideZ_forward;
                slideData.hasCollectedWeaponCharacteristics = true;
            }

            if (!slideData.IsStovepiping)
            {
                ___m_slideZ_forward = slideData.defaultFrontPosition;
                return;
            }

            var forwardPositionLimit = slideData.defaultFrontPosition - slideData.ejectedRoundRadius * 3f;
            ___m_slideZ_forward = forwardPositionLimit;

            /* Stovepipe the round...
             */

            if (!slideData.hasBulletBeenStovepiped)
            {
                StartStovepipe(slideData);
                slideData.randomPosAndRot = GenerateRandomHandgunNoise();
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */
            var slideTransform = __instance.transform;

            if (slideData.ejectedRound is null) return;
            if (__instance.Handgun.Chamber.ProxyRound == null) return;
            
            slideData.ejectedRound.transform.position =
                __instance.Handgun.Chamber.ProxyRound.position
                - slideTransform.forward * 0.5f * slideData.ejectedRoundHeight
                - slideTransform.forward * 1f * slideData.ejectedRoundRadius
                + slideTransform.up * slideData.randomPosAndRot[0];
            
            slideData.ejectedRound.transform.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            slideData.ejectedRound.transform.Rotate(slideData.ejectedRound.transform.right, slideData.randomPosAndRot[2], Space.World);
            
            if (slideData.ejectsToTheLeft)
            {
                slideData.ejectedRound.transform.Rotate(slideTransform.forward, -slideData.randomPosAndRot[1], Space.World);
                slideData.ejectedRound.transform.position -= 2 * slideData.ejectedRoundRadius * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
            }
            else
            {
                slideData.ejectedRound.transform.Rotate(slideTransform.forward, slideData.randomPosAndRot[1], Space.World);
                slideData.ejectedRound.transform.position += 2 * slideData.ejectedRoundRadius * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
            }
            
            slideData.timeSinceStovepiping += Time.deltaTime;
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void PhysicsSlideStovepipeCancelPatch(HandgunSlide __instance, 
            float ___m_slideZ_current, float ___m_slideZ_forward)
        {
            var slideData = __instance.gameObject.GetComponent<StovepipeData>();

            if (slideData == null)
                return;

            if (slideData.IsStovepiping == false) return;
            if (slideData.timeSinceStovepiping < TimeUntilCanPhysicsSlideUnStovepipe) return;
            if (___m_slideZ_current < ___m_slideZ_forward - 0.01f) UnStovepipe(slideData, true);
        }
        
        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (slideData == null) return true;
            if (!slideData.IsStovepiping) return true;
            
            __instance.Handgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForwardHeld, 1f);
            return false;

        }

        [HarmonyPatch(typeof(HandgunSlide), "BeginInteraction")]
        [HarmonyPostfix]
        private static void SlideInteractionUnStovepipes(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (slideData == null) return;
            if (!slideData.IsStovepiping) return;
            
            UnStovepipe(slideData, true);
        }
    }
}