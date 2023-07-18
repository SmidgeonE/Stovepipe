using System;
using System.Data.Common;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using FistVR;
using Random = UnityEngine.Random;


namespace Stovepipe
{

    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public class EjectionFailure : BaseUnityPlugin
    {
        private static FVRFireArmRound EjectedRound;
        private static Rigidbody EjectedRoundRb;
        private static Transform EjectedRoundTransform;
        private static int RoundDefaultLayer;
        private static float EjectedRoundWidth;
        private static float DefaultFrontPosition;
        
        
        private const float stovepipeProb = 0.5f;
        private static bool isStovepiping;
        private static bool isClippingThroughbullet;
        private static bool hasBulletBeenSetNonColliding;
        private static bool hasBeenRotatedUpwards;

        private static bool hasCollectedDefaultFrontPosition;

        private void Awake()
        {
            Debug.Log("Failure to eject");
            Harmony.CreateAndPatchAll(typeof(EjectionFailure), null);
        }

        [HarmonyPatch(typeof(Handgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(Handgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var handgunTransform = __instance.transform;
            
            EjectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                handgunTransform.right * __instance.RoundEjectionSpeed.x 
                + handgunTransform.up * __instance.RoundEjectionSpeed.y 
                + handgunTransform.forward * __instance.RoundEjectionSpeed.z, 
                handgunTransform.right * __instance.RoundEjectionSpin.x 
                + handgunTransform.up * __instance.RoundEjectionSpin.y 
                + handgunTransform.forward * __instance.RoundEjectionSpin.z, 
                __instance.RoundPos_Ejection.position, __instance.RoundPos_Ejection.rotation, 
                false);

            if (EjectedRound == null)
            {
                Debug.Log("Ejected round is null");
                return false;
            }
            
            EjectedRoundRb = EjectedRound.GetComponent<Rigidbody>();
            EjectedRoundTransform = EjectedRound.transform;
            
            
            var bulletCollider = EjectedRound.GetComponent<CapsuleCollider>();

            if (bulletCollider is null)
            {
                Debug.Log("bullet has no collider mesh");
                return false;
            }

            if (!EjectedRound.IsSpent)
            {
                Debug.Log("No longer stovepipping");
                isStovepiping = false;
            }

            EjectedRoundWidth = bulletCollider.bounds.size.y;
            
            Debug.Log("eject round has width:" + EjectedRoundWidth);

            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipePatch(HandgunSlide __instance)
        {
            if (__instance.IsHeld) return;
            if (!__instance.Handgun.Chamber.IsFull) return;
            isStovepiping = Random.Range(0f, 1f) < stovepipeProb;
            hasBulletBeenSetNonColliding = false;

            if (isStovepiping) Debug.Log("Stovepiping...");
        }


        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlidePatch(HandgunSlide __instance, ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            if (!hasCollectedDefaultFrontPosition)
            {
                Debug.Log("collecting default position");
                DefaultFrontPosition = ___m_slideZ_forward;
                hasCollectedDefaultFrontPosition = true;
            }

            if (!isStovepiping)
            {
                ___m_slideZ_forward = DefaultFrontPosition;
                return;
            }
            

            if (!hasBulletBeenSetNonColliding)
            {
                Debug.Log("forward float:" + ___m_slideZ_forward);
                Debug.Log("Ejected round width float: " + EjectedRoundWidth);
                Debug.Log("forward position limit " + (DefaultFrontPosition - EjectedRoundWidth));
            }
            
            var forwardPositionLimit = DefaultFrontPosition - EjectedRoundWidth;
            ___m_slideZ_forward = forwardPositionLimit;

            /*
            if (___m_slideZ_current < forwardPositionLimit) ___m_slideZ_current = forwardPositionLimit;
            */

            if (__instance.IsHeld)
            {
                Debug.Log("it is held, stopping stovepipe");
                isStovepiping = false;
                return;
            }

            /* Stovepipe the round...
             */
            
            if (!hasBulletBeenSetNonColliding) SetBulletToNonColliding(__instance);

            EjectedRound.RootRigidbody.position = __instance.transform.position + 0.1f * __instance.transform.up.normalized;

            EjectedRound.RootRigidbody.rotation = EjectedRound.RootRigidbody.rotation;

        }

        /*
        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void ForceSlideToFrontPatch(float __state)
        {
            if (!isStovepiping) return;
            
            Debug.Log("Forcing the slide to the front with state value, " + __state);
            
            
        }
        */


        [HarmonyPatch(typeof(FVRFireArmRound), "BeginAnimationFrom")]
        [HarmonyPrefix]
        private static bool CancelAnimationPatch(bool ___m_canAnimate)
        {
            if(___m_canAnimate) Debug.Log("Can animate");
            else Debug.Log("cannot animate");

            return !isStovepiping;
        }



        private static void SetBulletToNonColliding(HandgunSlide slide)
        {
            Debug.Log("setting bullet to non colliding");

            RoundDefaultLayer = EjectedRound.gameObject.layer;
            
            EjectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            EjectedRound.RootRigidbody.velocity = Vector3.zero;
            EjectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            EjectedRound.RootRigidbody.maxAngularVelocity = 0;
            EjectedRound.RootRigidbody.useGravity = false;
            EjectedRound.IsDestroyedAfterCounter = false;


            EjectedRoundTransform.position = slide.Point_Slide_Forward.position;
            hasBulletBeenSetNonColliding = true;
        }

        private static void SetBulletBackToNormal()
        {
            Debug.Log("Setting bullet back to normal.");
            EjectedRound.RootRigidbody.useGravity = true;
            hasBulletBeenSetNonColliding = false;
            isStovepiping = false;
            EjectedRound.gameObject.layer = RoundDefaultLayer;
            EjectedRound.IsDestroyedAfterCounter = false;
            hasBeenRotatedUpwards = false;
        }
        
        
        /*
        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void SlidePostfix(ref float ___m_slideZ_forward,
            ref float ___m_slideZ_current)
        {
            if (isStovepiping && isClippingThroughbullet)
            {
                Debug.Log("Is clipping through the bullet, returning to end of bullet");
                ___m_slideZ_current = (___m_slideZ_forward - EjectedRoundWidth) * 0.9f;
                isClippingThroughbullet = false;
            }
        }*/

        [HarmonyPatch(typeof(FVRFireArmRound), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void BulletInteractionPatch(FVRFireArmRound __instance)
        {
            if (!isStovepiping) return;
            if (!__instance.IsHeld) return;
            
            SetBulletBackToNormal();
        }
        
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter)
        {
            if (!isStovepiping) return;

            ___m_killAfter = 5f;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping()
        {
            return !isStovepiping;
        }

    }
}