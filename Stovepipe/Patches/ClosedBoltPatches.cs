using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe
{
    public class ClosedBoltPatches : StovepipeBase
    {
        
        [HarmonyPatch(typeof(ClosedBoltWeapon), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(ClosedBoltWeapon __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var data = __instance.Bolt.GetComponent(typeof(StovepipeData)) as StovepipeData;

            if (data is null)
            {
                __instance.Bolt.gameObject.AddComponent(typeof(StovepipeData));
                return true;
            }

            var transform = __instance.transform;
            var right = transform.right;
            var up = transform.up;
            var forward = transform.forward;
            data.ejectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                right * __instance.EjectionSpeed.x + up * __instance.EjectionSpeed.y + forward * __instance.EjectionSpeed.z, 
                right * __instance.EjectionSpin.x + up * __instance.EjectionSpin.y + forward * __instance.EjectionSpin.z,
                __instance.RoundPos_Ejection.position, 
                __instance.RoundPos_Ejection.rotation, 
                false);
            
            if (data.ejectedRound is null) return false;

            var bulletDataHolder = data.ejectedRound.gameObject.AddComponent<BulletStovepipeData>();
            bulletDataHolder.data = data;
            
            data.bulletCollider = data.ejectedRound.GetComponent<CapsuleCollider>();

            if (data.bulletCollider is null) return false;

            data.ejectedRoundWidth = data.bulletCollider.radius;
            data.ejectedRoundHeight = data.bulletCollider.height;
            
            return false;
        }

        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipeDiceroll(ClosedBolt __instance)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data == null) return;
            if (__instance.IsHeld) return;
            
            var weapon = __instance.Weapon;

            if (!weapon.Chamber.IsFull) return;
            if (!weapon.Chamber.IsSpent) return;
            if (weapon.Chamber.GetRound().IsCaseless) return;
            if (__instance.HasLastRoundBoltHoldOpen)
            {
                if (weapon.Magazine == null) return;
                if (weapon.Magazine.m_numRounds == 0) return;
            }

            data.IsStovepiping = Random.Range(0f, 1f) < data.stovepipeProb;
            data.hasBulletBeenStovepiped = false;
        }


        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltAndBulletUpdate(ClosedBolt __instance, ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data is null) return;
            
            if (!data.hasCollectedWeaponCharacteristics)
            {
                data.defaultFrontPosition = ___m_boltZ_forward;
                data.boltOrSlideRadius = __instance.gameObject.GetComponent<CapsuleCollider>().radius;
                data.hasCollectedWeaponCharacteristics = true;
            }
            
            if (data.ejectedRound is null) Debug.Log("asd");
            if (__instance.transform is null) Debug.Log("What the fuck");
            
            if (!data.IsStovepiping && GetIfCasingIsStillInsideAction(__instance.transform, data.ejectedRound, data.boltOrSlideRadius))
            {
                ___m_boltZ_forward = data.defaultFrontPosition - data.ejectedRoundHeight * 1.3f;
                return;
            }
            
            if (!data.IsStovepiping)
            {
                ___m_boltZ_forward = data.defaultFrontPosition;
                return;
            }


            var forwardPositionLimit = data.defaultFrontPosition - data.ejectedRoundWidth * 3f;
            ___m_boltZ_forward = forwardPositionLimit;

            /* Stovepipe the round...
             */

            if (!data.hasBulletBeenStovepiped)
            {
                StartStovepipe(data);
                data.randomPosAndRot = GenerateRandomNoise();
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */

            var slideTransform = __instance.transform;

            if (data.ejectedRound is null) return;
            if (__instance.Weapon.Chamber.ProxyRound == null) return;


            var ejectionPortDir = GetVectorThatPointsOutOfEjectionPort(__instance);
            var dirPerpOfSlideAndEjectionPort = Vector3.Cross(ejectionPortDir, slideTransform.forward);

            data.ejectedRound.transform.position =
                __instance.Weapon.Chamber.ProxyRound.position
                - slideTransform.forward * 0.5f * data.ejectedRoundHeight
                - slideTransform.forward * 1f * data.ejectedRoundWidth
                + slideTransform.up * data.randomPosAndRot[0];

            data.ejectedRound.transform.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
            data.ejectedRound.transform.Rotate(data.ejectedRound.transform.right, data.randomPosAndRot[2], Space.World);

            if (data.ejectsToTheLeft)
            {
                data.ejectedRound.transform.Rotate(slideTransform.forward, -data.randomPosAndRot[1], Space.World);
                data.ejectedRound.transform.position -= 2 * data.ejectedRoundWidth * slideTransform.right * Mathf.Abs(Mathf.Sin(data.randomPosAndRot[1]));
            }
            else
            {
                data.ejectedRound.transform.Rotate(slideTransform.forward, data.randomPosAndRot[1], Space.World);
                data.ejectedRound.transform.position += 2 * data.ejectedRoundWidth * slideTransform.right * Mathf.Abs(Mathf.Sin(data.randomPosAndRot[1]));
            }
            
            data.timeSinceStovepiping += Time.deltaTime;
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void PhysicsBoltStovepipeCancelPatch(ClosedBolt __instance, 
            float ___m_boltZ_current, float ___m_boltZ_forward)
        {
            var slideData = __instance.gameObject.GetComponent<StovepipeData>();

            if (slideData == null) return;

            if (slideData.IsStovepiping == false) return;
            if (slideData.timeSinceStovepiping < TimeUntilCanPhysicsSlideUnStovepipe) return;
            if (___m_boltZ_current < ___m_boltZ_forward - 0.01f) UnStovepipe(slideData, true);
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(ClosedBolt __instance)
        {
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (slideData is null) return true;
            if (!slideData.IsStovepiping) return true;
            
            __instance.Weapon.PlayAudioEvent(FirearmAudioEventType.BoltSlideForwardHeld, 1f);
            return false;

        }

        [HarmonyPatch(typeof(FVRInteractiveObject), "BeginInteraction")]
        [HarmonyPostfix]
        private static void SlideInteractionUnStovepipes(FVRInteractiveObject __instance)
        {
            if (!(__instance is ClosedBolt)) return;

            __instance = (ClosedBolt)__instance;
            
            var slideData = __instance.gameObject.GetComponent(typeof(StovepipeData)) as StovepipeData;

            if (slideData == null) return;
            if (!slideData.IsStovepiping) return;
            
            UnStovepipe(slideData, true);
        }
    }
}