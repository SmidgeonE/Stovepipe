using FistVR;
using HarmonyLib;
using Stovepipe.Debug;
using Stovepipe.ModFiles;
using UnityEngine;

namespace Stovepipe.StovepipePatches
{
    public class TubeFedStovepipePatches : StovepipeBase
    {
        [HarmonyPatch(typeof(TubeFedShotgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(TubeFedShotgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var data = __instance.Bolt.GetComponent(typeof(StovepipeData)) as StovepipeData;

            if (data is null)
            {
                data = __instance.Bolt.gameObject.AddComponent(typeof(StovepipeData)) as StovepipeData;
                data.SetWeaponType(WeaponType.TubeFedShotgun);
            }

            var transform = __instance.transform;
            var right = transform.right;
            var up = transform.up;
            var forward = transform.forward;
            data.ejectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                right * __instance.RoundEjectionSpeed.x + up * __instance.RoundEjectionSpeed.y + forward * __instance.RoundEjectionSpeed.z, 
                right * __instance.RoundEjectionSpeed.x + up * __instance.RoundEjectionSpeed.y + forward * __instance.RoundEjectionSpeed.z,
                __instance.RoundPos_Ejection.position, 
                __instance.RoundPos_Ejection.rotation, 
                false);
            

            if (data.ejectedRound is null) return false;
            
            data.numOfRoundsSinceLastJam++;
            data.CheckAndIncreaseProbability();
            var bulletDataHolder = data.ejectedRound.gameObject.AddComponent<BulletStovepipeData>();
            bulletDataHolder.data = data;

            data.ejectedRoundCollider = data.ejectedRound.GetComponent<CapsuleCollider>();

            if (data.ejectedRoundCollider is null) return false;

            data.ejectedRoundRadius = data.ejectedRoundCollider.radius;
            data.ejectedRoundHeight = data.ejectedRoundCollider.height;
            
            return false;
        }

        [HarmonyPatch(typeof(TubeFedShotgunBolt), "BoltEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipeDiceroll(TubeFedShotgunBolt __instance)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data == null) return;
            if (__instance.IsHeld) return;
            
            var weapon = __instance.Shotgun;

            if (data.numOfRoundsSinceLastJam < UserConfig.MinRoundBeforeNextJam.Value) return;
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


        [HarmonyPatch(typeof(TubeFedShotgunBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltAndBulletUpdate(TubeFedShotgunBolt __instance, ref float ___m_boltZ_forward)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data is null) return;

            if (!data.hasCollectedWeaponCharacteristics)
            {
                data.defaultFrontPosition = ___m_boltZ_forward;
                data.hasCollectedWeaponCharacteristics = true;
            }
            
            if (!data.IsStovepiping)
            {
                if (!DebugMode.IsDebuggingWeapon) ___m_boltZ_forward = data.defaultFrontPosition;
                return;
            }
            
            /* Stovepipe the round...
             */

            if (!data.hasBulletBeenStovepiped)
            {
                StartStovepipe(data);
                data.randomPosAndRot = GenerateRandomRifleNoise();
                data.Adjustments = DebugMode.ReadAdjustment(__instance.Shotgun.name);
                if (data.Adjustments != null) data.hasFoundAdjustments = true;
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */

            var slideTransform = __instance.transform;

            if (data.ejectedRound is null) return;
            if (__instance.Shotgun.Chamber.ProxyRound == null) return;

            var weapon = __instance.Shotgun;
            var bulletTransform = data.ejectedRound.transform;

            if (data.hasFoundAdjustments)
            {
                // ReSharper disable once PossibleNullReferenceException
                bulletTransform.localPosition = data.Adjustments.BulletLocalPos;
                bulletTransform.localRotation = data.Adjustments.BulletDir;
                ___m_boltZ_forward = data.Adjustments.BoltZ;
                data.timeSinceStovepiping += Time.deltaTime;
                return;
            }
            
            // If we couldn't find an adjustment set by the user, we just use a procedural positioning:

            var gunTransform = __instance.Shotgun.transform;
            var velDirec = (gunTransform.right * weapon.RoundEjectionSpeed.x +
                            gunTransform.up * weapon.RoundEjectionSpeed.y +
                            gunTransform.forward * weapon.RoundEjectionSpeed.z).normalized;
            var gunTransformForward = gunTransform.forward;

            if (!data.hasFoundIfItEjectsUpwards)
            {
                data.ejectsUpwards = IsRifleThatEjectsUpwards(weapon.RoundPos_Ejection, __instance.transform, data.ejectedRound);
                data.hasFoundIfItEjectsUpwards = true;
            }
            
            if (data.ejectsUpwards)
            {
                bulletTransform.rotation = Quaternion.LookRotation(slideTransform.up, -slideTransform.forward);
                bulletTransform.position += gunTransformForward * data.ejectedRoundRadius * 4;
            }
            else
            {
                bulletTransform.rotation = Quaternion.LookRotation(velDirec, -slideTransform.forward);
            }

            bulletTransform.Rotate(slideTransform.forward, data.randomPosAndRot[1], Space.World);
            bulletTransform.Rotate(bulletTransform.right, data.randomPosAndRot[2], Space.World);

            bulletTransform.position = weapon.Chamber.ProxyRound.position
                                       - gunTransformForward * data.ejectedRoundRadius * 4f
                                       + bulletTransform.forward * data.ejectedRoundHeight * 0.3f
                                       + bulletTransform.forward * data.randomPosAndRot[0];
            
            
            /* These are the weird cases where the default positioning doesnt work well */

            // NOT IMPLEMENTED
            
            /* Now setting the slide to the end of the bullet */

            var dx = weapon.Chamber.transform.localPosition.z - bulletTransform.localPosition.z - data.ejectedRoundHeight/2;
            var forwardPositionLimit = data.defaultFrontPosition - dx - 1.2f * data.ejectedRoundRadius;
            
            ___m_boltZ_forward = forwardPositionLimit;

            data.timeSinceStovepiping += Time.deltaTime;
        }

        /*
        [HarmonyPatch(typeof(TubeFedShotgunBolt), "BoltEvent_ExtractRoundFromMag")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(TubeFedShotgunBolt __instance)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data is null) return true;
            if (!data.IsStovepiping) return true;
            
            UnityEngine.Debug.Log("stopping from extracting from mag");
            __instance.Shotgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForwardHeld, 1f);
            return false;
        }
        */

        [HarmonyPatch(typeof(TubeFedShotgun), "TransferShellToUpperTrack")]
        [HarmonyPrefix]
        private static bool StopShellFromEnteringTrackWhenStovepiping(TubeFedShotgun __instance)
        {
            var data = __instance.Bolt.GetComponent<StovepipeData>();

            return data == null || !data.IsStovepiping;
        }

        [HarmonyPatch(typeof(TubeFedShotgunBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void UnStovepipeIfBoltIsLockedOrBack(TubeFedShotgunBolt __instance,
            bool ___m_isBoltLocked, float ___m_boltZ_current, float ___m_boltZ_forward)
        {
            var data = __instance.Shotgun.Bolt.GetComponent<StovepipeData>();
            
            if (data == null) return;
            if (!data.IsStovepiping) return;
            if (data.ejectedRound is null) return;
            if (data.ejectedRound.GetComponent<BulletStovepipeData>().timeSinceStovepiped < 0.5f) return;
            if (!___m_isBoltLocked && (___m_boltZ_current - ___m_boltZ_forward) > -0.01f) return;
            if (!DoesBulletAimAtFloor(data.ejectedRound)
                && !CouldBulletFallOutGunHorizontally(__instance.Shotgun.RootRigidbody, data.ejectedRound.transform.forward))
                return;

            UnStovepipe(data, true, __instance.Shotgun.RootRigidbody);
        }
    }
}