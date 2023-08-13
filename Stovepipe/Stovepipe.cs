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
    public class Stovepipe
    {
        private const float timeUntilCanPhysicsSlideUnStovepipe = 0.1f;

        
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

            if (slideData.ejectedRound is null) return false;

            var bulletDataHolder = slideData.ejectedRound.gameObject.AddComponent<BulletStovepipeData>();
            bulletDataHolder.slideData = slideData;
            
            slideData.bulletCollider = slideData.ejectedRound.GetComponent<CapsuleCollider>();

            if (slideData.bulletCollider is null) return false;

            slideData.ejectedRoundWidth = slideData.bulletCollider.radius;
            slideData.ejectedRoundHeight = slideData.bulletCollider.height;
            
            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipeDiceroll(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

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
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

            if (slideData is null) return;
            
            if (!slideData.hasCollectedDefaultFrontPosition)
            {
                slideData.defaultFrontPosition = ___m_slideZ_forward;
                slideData.hasCollectedDefaultFrontPosition = true;
            }

            if (!slideData.IsStovepiping)
            {
                ___m_slideZ_forward = slideData.defaultFrontPosition;
                return;
            }

            var forwardPositionLimit = slideData.defaultFrontPosition - slideData.ejectedRoundWidth * 3f;
            ___m_slideZ_forward = forwardPositionLimit;

            /* Stovepipe the round...
             */

            if (!slideData.hasBulletBeenStovepiped)
            {
                StartStovepipe(slideData);
                slideData.randomPosAndRot = GenerateRandomNoise();
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */

            var slideTransform = __instance.transform;

            if (slideData.ejectedRound is null) return;
            if (__instance.Handgun.Chamber.ProxyRound == null) return;


            var ejectionPortDir = GetVectorThatPointsOutOfEjectionPort(__instance);
            var dirPerpOfSlideAndEjectionPort = Vector3.Cross(ejectionPortDir, slideTransform.forward);

            slideData.ejectedRound.transform.position =
                __instance.Handgun.Chamber.ProxyRound.position
                - slideTransform.forward * 0.5f * slideData.ejectedRoundHeight
                - slideTransform.forward * 1f * slideData.ejectedRoundWidth
                + slideTransform.up * slideData.randomPosAndRot[0];

            slideData.ejectedRound.transform.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            slideData.ejectedRound.transform.Rotate(slideData.ejectedRound.transform.right, slideData.randomPosAndRot[2], Space.World);

            if (slideData.ejectsToTheLeft)
            {
                slideData.ejectedRound.transform.Rotate(slideTransform.forward, -slideData.randomPosAndRot[1], Space.World);
                slideData.ejectedRound.transform.position -= 2 * slideData.ejectedRoundWidth * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
            }
            else
            {
                slideData.ejectedRound.transform.Rotate(slideTransform.forward, slideData.randomPosAndRot[1], Space.World);
                slideData.ejectedRound.transform.position += 2 * slideData.ejectedRoundWidth * slideTransform.right * Mathf.Abs(Mathf.Sin(slideData.randomPosAndRot[1]));
            }
            
            slideData.timeSinceStovepiping += Time.deltaTime;
        }

        private static float[] GenerateRandomNoise()
        {
            // Returns a 3-array of floats, first being randomness in the up/down pos, 
            // Next being random angle about the forward slide direction
            // Final being random angle about the perpendicular slide direction (left / right)
            // The rotation about the forward axis is randomised more to the right, as most handguns eject from the right

            return new[] { Random.Range(0.003f, 0.012f), -35f + Random.Range(-15f, 20f), Random.Range(0, 15f) };
        }

        /*
        [HarmonyPatch(typeof(FVRFireArmRound), "BeginAnimationFrom")]
        [HarmonyPrefix]
        private static bool CancelAnimationPatch(FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent(typeof(BulletStovepipeData)) 
                as BulletStovepipeData;

            if (bulletData is null) return true;
            
            return !bulletData.slideData.IsStovepiping;
        }
        */
        
        private static void StartStovepipe(SlideStovepipeData slideData)
        {
            slideData.roundDefaultLayer = slideData.ejectedRound.gameObject.layer;
            
            slideData.ejectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            slideData.ejectedRound.RootRigidbody.velocity = Vector3.zero;
            slideData.ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            slideData.ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            slideData.ejectedRound.RootRigidbody.useGravity = false;
            slideData.ejectedRound.RootRigidbody.detectCollisions = false;
            slideData.hasBulletBeenStovepiped = true;
            slideData.timeSinceStovepiping = 0f;

            if (slideData.transform.parent != null)
                slideData.ejectedRound.SetParentage(slideData.transform);
            else slideData.ejectedRound.SetParentage(slideData.transform.parent);

            // DEBUG CUUUUUUUUUBE
            /*var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "beep";
            cube.transform.localScale = Vector3.one * 0.01f;
            cube.transform.position = slideData.gameObject.GetComponent<HandgunSlide>().Handgun.RoundPos_Ejection.position;
            if (slideData.transform.parent != null)
                cube.transform.parent = slideData.transform.parent;
            else cube.transform.parent = slideData.transform;*/
        }

        private static Vector3 GetVectorThatPointsOutOfEjectionPort(HandgunSlide slide)
        {
            var ejectionDir = slide.Handgun.RoundPos_Ejection.position - slide.transform.position;
            var componentAlongSlide = Vector3.Dot(slide.transform.forward, ejectionDir);
            ejectionDir -= componentAlongSlide * slide.transform.forward;

            return ejectionDir.normalized;
        }

        private static void UnStovepipe(SlideStovepipeData slideData, bool breakParentage)
        {
            slideData.ejectedRound.RootRigidbody.useGravity = true;
            slideData.hasBulletBeenStovepiped = false;
            slideData.IsStovepiping = false;
            slideData.ejectedRound.gameObject.layer = slideData.roundDefaultLayer;
            slideData.ejectedRound.RootRigidbody.maxAngularVelocity = 1000f;
            slideData.ejectedRound.RootRigidbody.detectCollisions = true;
            slideData.timeSinceStovepiping = 0f;
            if (breakParentage) slideData.ejectedRound.SetParentage(null);
            
            /*Object.Destroy(GameObject.Find("beep"));*/
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void BulletGrabUnStovepipes(FVRFireArmRound __instance)
        {
            if (!__instance.IsHeld) return;
            
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            if (!bulletData.slideData.IsStovepiping) return;

            UnStovepipe(bulletData.slideData, false);
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void PhysicsSlideStovepipeCancelPatch(HandgunSlide __instance, 
            float ___m_slideZ_current, float ___m_slideZ_forward)
        {
            var slideData = __instance.gameObject.GetComponent<SlideStovepipeData>();

            if (slideData == null)
            {
                Debug.Log("Something has gone terribly wrong with stovepiping");
                return;
            }

            if (slideData.IsStovepiping == false) return;
            if (slideData.timeSinceStovepiping < timeUntilCanPhysicsSlideUnStovepipe) return;
            if (___m_slideZ_current < ___m_slideZ_forward - 0.01f) UnStovepipe(slideData, true);
        }
        
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            
            if (!bulletData.slideData.IsStovepiping) return;
            
            ___m_killAfter = 5f;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

            if (!slideData.IsStovepiping) return true;
            
            __instance.Handgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForwardHeld, 1f);
            return false;

        }

        [HarmonyPatch(typeof(HandgunSlide), "BeginInteraction")]
        [HarmonyPostfix]
        private static void SlideInteractionUnStovepipes(HandgunSlide __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(SlideStovepipeData)) 
                as SlideStovepipeData;

            if (slideData == null) return;
            if (!slideData.IsStovepiping) return;
            
            UnStovepipe(slideData, true);
        }


        public static bool FindIfGunEjectsToTheLeft(HandgunSlide slide)
        {
            // returns true if left, false if not.

            var dirOutOfEjectionPort = GetVectorThatPointsOutOfEjectionPort(slide);
            var componentToTheRight = Vector3.Dot(dirOutOfEjectionPort, slide.transform.right);

            return componentToTheRight < -0.005f;
        }

    }
}