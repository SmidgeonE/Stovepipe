using System;
using System.Data.Common;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using FistVR;
using Random = UnityEngine.Random;


namespace Stovepipe
{
    public class EjectionFailure
    {

        
        [HarmonyPatch(typeof(Handgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(Handgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var handgunTransform = __instance.transform;
            var slideData = __instance.Slide.GetComponent(typeof(SlideStovepipeData)) as SlideStovepipeData;

            if (slideData is null)
            {
                Debug.Log("Something is horribly wrong, the handgun does not have a data holder");
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

            var bulletDataHolder = slideData.ejectedRound.gameObject.AddComponent<BulletStovepipeData>();
            bulletDataHolder.slideData = slideData;

            if (slideData.ejectedRound == null)
            {
                Debug.Log("Ejected round is null");
                return false;
            }
            
            slideData.bulletCollider = slideData.ejectedRound.GetComponent<CapsuleCollider>();

            if (slideData.bulletCollider is null)
            {
                Debug.Log("bullet has no collider mesh");
                return false;
            }

            slideData.ejectedRoundWidth = slideData.bulletCollider.bounds.size.y;
            slideData.ejectedRoundHeight = slideData.bulletCollider.bounds.size.z;
            
            /*
            Debug.Log("Bullets dimensions in x y z :");
            Debug.Log(slideData.bulletCollider.bounds.size.x);
            Debug.Log(slideData.bulletCollider.bounds.size.y);
            Debug.Log(slideData.bulletCollider.bounds.size.z);
            Debug.Log("");
            
            Debug.Log("eject round has width:" + slideData.ejectedRoundWidth);
            */

            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipePatch(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;
            
            if (__instance.IsHeld) return;
            if (!__instance.Handgun.Chamber.IsFull) return;
            if (!__instance.Handgun.Chamber.IsSpent) return;
            if (__instance.HasLastRoundSlideHoldOpen)
            {
                if (__instance.Handgun.Magazine == null) return;
                if (__instance.Handgun.Magazine.m_numRounds == 0) return;
            }
            
            
            slideData.IsStovepiping = Random.Range(0f, 1f) < slideData.stovepipeProb;
            slideData.hasBulletBeenSetNonColliding = false;
        }


        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlidePatch(HandgunSlide __instance, ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

            if (slideData is null) return;
            
            if (!slideData.hasCollectedDefaultFrontPosition)
            {
                Debug.Log("collecting default position");
                slideData.defaultFrontPosition = ___m_slideZ_forward;
                slideData.hasCollectedDefaultFrontPosition = true;
            }

            if (!slideData.IsStovepiping)
            {
                ___m_slideZ_forward = slideData.defaultFrontPosition;
                return;
            }


            var forwardPositionLimit = slideData.defaultFrontPosition - slideData.ejectedRoundWidth * 1.5f;
            ___m_slideZ_forward = forwardPositionLimit;
            
            if (!slideData.hasBulletBeenSetNonColliding)
            {
                /*Debug.Log("forward float:" + ___m_slideZ_forward);
                Debug.Log("Ejected round width float: " + slideData.ejectedRoundWidth);
                Debug.Log("forward position limit " + forwardPositionLimit);*/
            }
            
            /* Stovepipe the round...
             */
            
            if (!slideData.hasBulletBeenSetNonColliding) SetBulletToStovepiping(slideData);
            
            /* Now setting the position and rotation while the bullet is stovepiping */

            var slideTransform = __instance.transform;

            if (slideData.ejectedRound is null) return;

            if (__instance.Handgun is null) Debug.Log("handgun is null");
            if (__instance.Handgun.Chamber == null) Debug.Log("chamber is null");

            if (__instance.Handgun.Chamber.ProxyRound == null)
            {
                Debug.Log("Proxy round transform is null");
                return;
            }

            slideData.ejectedRound.RootRigidbody.position =
                __instance.Handgun.Chamber.ProxyRound.position
                + slideTransform.up.normalized * 0.1f
                - slideTransform.forward.normalized * 0.5f * slideData.ejectedRoundHeight;

            slideData.ejectedRound.RootRigidbody.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            
            
            
            
        }


        [HarmonyPatch(typeof(FVRFireArmRound), "BeginAnimationFrom")]
        [HarmonyPrefix]
        private static bool CancelAnimationPatch(FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent(typeof(BulletStovepipeData)) 
                as BulletStovepipeData;

            if (bulletData is null) return true;
            
            return !bulletData.slideData.IsStovepiping;
        }
        
        private static void SetBulletToStovepiping(SlideStovepipeData slideData)
        {
            Debug.Log("setting bullet to non colliding");

            slideData.roundDefaultLayer = slideData.ejectedRound.gameObject.layer;
            
            slideData.ejectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            slideData.ejectedRound.RootRigidbody.velocity = Vector3.zero;
            slideData.ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            slideData.ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            slideData.ejectedRound.RootRigidbody.useGravity = false;

            slideData.hasBulletBeenSetNonColliding = true;
            slideData.bulletCollider.isTrigger = true;
        }

        private static void SetBulletBackToNormal(SlideStovepipeData slideData)
        {
            Debug.Log("Setting bullet back to normal.");
            slideData.ejectedRound.RootRigidbody.useGravity = true;
            slideData.hasBulletBeenSetNonColliding = false;
            slideData.IsStovepiping = false;
            slideData.ejectedRound.gameObject.layer = slideData.roundDefaultLayer;
            slideData.bulletCollider.isTrigger = false;
            slideData.ejectedRound.RootRigidbody.maxAngularVelocity = 1000f;
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void BulletInteractionPatch(FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null)
            {
                return;
            }

            if (!bulletData.slideData.IsStovepiping)
            {
                return;
            }
            Debug.Log("This bullet is stovepiping");

            if (!__instance.IsHeld)
            {
                return;
            }
            
            Debug.Log("setting bullet back to normal via being held");
            SetBulletBackToNormal(bulletData.slideData);
        }
        
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            
            if (!bulletData.slideData.IsStovepiping) return;

            Debug.Log("resetting lifetime");
            ___m_killAfter = 5f;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;
            
            return !slideData.IsStovepiping;
        }

        [HarmonyPatch(typeof(HandgunSlide), "BeginInteraction")]
        [HarmonyPostfix]
        private static void SlideInteractionCancelsStovepiping(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

            if (slideData == null) return;
            
            if (!slideData.IsStovepiping) return;
            
            SetBulletBackToNormal(slideData);
        }

    }
}