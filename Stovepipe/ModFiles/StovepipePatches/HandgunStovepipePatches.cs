using FistVR;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Stovepipe.StovepipePatches
{
    public class HandgunStovepipePatches : StovepipeBase
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
                slideData = __instance.Slide.gameObject.AddComponent(typeof(StovepipeData)) as StovepipeData;
                slideData.SetWeaponType(WeaponType.Handgun);
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
            
            slideData.ejectedRoundCollider = slideData.ejectedRound.GetComponent<CapsuleCollider>();

            if (slideData.ejectedRoundCollider is null) return false;

            slideData.ejectedRoundRadius = slideData.ejectedRoundCollider.radius;
            slideData.ejectedRoundHeight = slideData.ejectedRoundCollider.height;
            
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
                slideData.Adjustments = FailureScriptManager.ReadAdjustment(__instance.Handgun.name);
                slideData.timeSinceStovepiping += Time.deltaTime;
                
                if (slideData.Adjustments != null) slideData.hasFoundAdjustments = true;
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */
            var slideTransform = __instance.transform;

            if (slideData.ejectedRound is null) return;
            if (__instance.Handgun.Chamber.ProxyRound == null) return;

            var bulletTransform = slideData.ejectedRound.transform;
            
            
            // If we found a user made adjustment, we apply it 
            
            if (slideData.hasFoundAdjustments)
            {
                // ReSharper disable once PossibleNullReferenceException
                bulletTransform.localPosition = slideData.Adjustments.BulletLocalPos;
                bulletTransform.localRotation = slideData.Adjustments.BulletDir;
                ___m_slideZ_forward = slideData.Adjustments.BoltZ;
                return;
            }

            
            bulletTransform.position =
                __instance.Handgun.Chamber.ProxyRound.position
                - slideTransform.forward * 0.5f * slideData.ejectedRoundHeight
                - slideTransform.forward * 1f * slideData.ejectedRoundRadius
                + slideTransform.up * slideData.randomPosAndRot[0];
            
            bulletTransform.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            bulletTransform.Rotate(bulletTransform.right, slideData.randomPosAndRot[2], Space.World);
            
            if (slideData.ejectsToTheLeft)
            {
                bulletTransform.Rotate(slideTransform.forward, -slideData.randomPosAndRot[1], Space.World);
                bulletTransform.position -= 2 * slideData.ejectedRoundRadius * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
            }
            else
            {
                bulletTransform.Rotate(slideTransform.forward, slideData.randomPosAndRot[1], Space.World);
                bulletTransform.position += 2 * slideData.ejectedRoundRadius * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
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
            if (___m_slideZ_current < ___m_slideZ_forward - 0.01f) UnStovepipe(slideData, true, __instance.Handgun.RootRigidbody);
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
            
            UnStovepipe(slideData, true, __instance.Handgun.RootRigidbody);
        }
    }
}