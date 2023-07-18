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
        private static FVRFireArmRound _ejectedRound;
        private static Rigidbody _ejectedRoundRb;
        private static Transform _ejectedRoundTransform;
        private static int _roundDefaultLayer;
        private static float _ejectedRoundWidth;
        private static float _defaultFrontPosition;
        
        private const float StovepipeProb = 0.5f;
        private static bool _isStovepiping;
        private static bool _hasBulletBeenSetNonColliding;

        private static bool _hasCollectedDefaultFrontPosition;
        private static CapsuleCollider _bulletCollider;

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(EjectionFailure), null);
        }

        [HarmonyPatch(typeof(Handgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(Handgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var handgunTransform = __instance.transform;
            
            _ejectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                handgunTransform.right * __instance.RoundEjectionSpeed.x 
                + handgunTransform.up * __instance.RoundEjectionSpeed.y 
                + handgunTransform.forward * __instance.RoundEjectionSpeed.z, 
                handgunTransform.right * __instance.RoundEjectionSpin.x 
                + handgunTransform.up * __instance.RoundEjectionSpin.y 
                + handgunTransform.forward * __instance.RoundEjectionSpin.z, 
                __instance.RoundPos_Ejection.position, __instance.RoundPos_Ejection.rotation, 
                false);

            if (_ejectedRound == null)
            {
                Debug.Log("Ejected round is null");
                return false;
            }
            
            _ejectedRoundRb = _ejectedRound.GetComponent<Rigidbody>();
            _ejectedRoundTransform = _ejectedRound.transform;
            _bulletCollider = _ejectedRound.GetComponent<CapsuleCollider>();

            if (_bulletCollider is null)
            {
                Debug.Log("bullet has no collider mesh");
                return false;
            }

            if (!_ejectedRound.IsSpent)
            {
                _isStovepiping = false;
            }

            _ejectedRoundWidth = _bulletCollider.bounds.size.y;
            
            Debug.Log("eject round has width:" + _ejectedRoundWidth);

            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipePatch(HandgunSlide __instance)
        {
            if (__instance.IsHeld) return;
            if (!__instance.Handgun.Chamber.IsFull) return;
            _isStovepiping = Random.Range(0f, 1f) < StovepipeProb;
            _hasBulletBeenSetNonColliding = false;
        }


        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlidePatch(HandgunSlide __instance, ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            if (!_hasCollectedDefaultFrontPosition)
            {
                Debug.Log("collecting default position");
                _defaultFrontPosition = ___m_slideZ_forward;
                _hasCollectedDefaultFrontPosition = true;
            }

            if (!_isStovepiping)
            {
                ___m_slideZ_forward = _defaultFrontPosition;
                return;
            }
            
            if (!_hasBulletBeenSetNonColliding)
            {
                Debug.Log("forward float:" + ___m_slideZ_forward);
                Debug.Log("Ejected round width float: " + _ejectedRoundWidth);
                Debug.Log("forward position limit " + (_defaultFrontPosition - _ejectedRoundWidth));
            }
            
            var forwardPositionLimit = _defaultFrontPosition - _ejectedRoundWidth;
            ___m_slideZ_forward = forwardPositionLimit;
            
            if (__instance.IsHeld)
            {
                _isStovepiping = false;
                return;
            }

            /* Stovepipe the round...
             */
            
            if (!_hasBulletBeenSetNonColliding) SetBulletToStovepiping(__instance);

            var slideTransform = __instance.transform;
            
            _ejectedRound.RootRigidbody.position = new Vector3(slideTransform.position.x, slideTransform.position.y, __instance.Handgun.RoundPos_Ejection.position.z);
            _ejectedRound.RootRigidbody.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            
        }


        [HarmonyPatch(typeof(FVRFireArmRound), "BeginAnimationFrom")]
        [HarmonyPrefix]
        private static bool CancelAnimationPatch(bool ___m_canAnimate)
        {
            return !_isStovepiping;
        }
        
        private static void SetBulletToStovepiping(HandgunSlide slide)
        {
            Debug.Log("setting bullet to non colliding");

            _roundDefaultLayer = _ejectedRound.gameObject.layer;
            
            _ejectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            _ejectedRound.RootRigidbody.velocity = Vector3.zero;
            _ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            _ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            _ejectedRound.RootRigidbody.useGravity = false;

            _ejectedRoundTransform.position = slide.Point_Slide_Forward.position;
            _hasBulletBeenSetNonColliding = true;
            _bulletCollider.isTrigger = true;
        }

        private static void SetBulletBackToNormal()
        {
            Debug.Log("Setting bullet back to normal.");
            _ejectedRound.RootRigidbody.useGravity = true;
            _hasBulletBeenSetNonColliding = false;
            _isStovepiping = false;
            _ejectedRound.gameObject.layer = _roundDefaultLayer;
            _bulletCollider.isTrigger = false;
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void BulletInteractionPatch(FVRFireArmRound __instance)
        {
            if (!_isStovepiping) return;
            if (!__instance.IsHeld) return;
            
            SetBulletBackToNormal();
        }
        
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter)
        {
            if (!_isStovepiping) return;

            ___m_killAfter = 5f;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping()
        {
            return !_isStovepiping;
        }

    }
}