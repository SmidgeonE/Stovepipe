using FistVR;
using HarmonyLib;
using Stovepipe.Debug;
using Stovepipe.ModFiles;
using UnityEngine;

namespace Stovepipe.StovepipePatches
{
    public class OpenBoltStovepipePatches : StovepipeBase
    {
        [HarmonyPatch(typeof(OpenBoltReceiver), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(OpenBoltReceiver __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var data = __instance.Bolt.GetComponent(typeof(StovepipeData)) as StovepipeData;

            if (data is null)
            {
                data = __instance.Bolt.gameObject.AddComponent(typeof(StovepipeData)) as StovepipeData;
                data.SetWeaponType(WeaponType.OpenBolt);
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

        [HarmonyPatch(typeof(OpenBoltReceiverBolt), "BoltEvent_EjectRound")]
        [HarmonyPrefix]
        private static void StovepipeDiceroll(OpenBoltReceiverBolt __instance)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data == null) return;
            if (__instance.IsHeld) return;
            
            var weapon = __instance.Receiver;

            // Weapons it doenst work with
            if (weapon.name.Contains("MX")) return;
            
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


        [HarmonyPatch(typeof(OpenBoltReceiverBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltAndBulletUpdate(OpenBoltReceiverBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
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
                data.Adjustments = DebugMode.ReadAdjustment(__instance.Receiver.name);
                if (data.Adjustments != null) data.hasFoundAdjustments = true;
            }
            
            /* Now setting the position and rotation while the bullet is stovepiping */

            var slideTransform = __instance.transform;

            if (data.ejectedRound is null) return;
            if (__instance.Receiver.Chamber.ProxyRound == null) return;

            var weapon = __instance.Receiver;
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

            var gunTransform = __instance.Receiver.transform;
            var velDirec = (gunTransform.right * weapon.EjectionSpeed.x +
                            gunTransform.up * weapon.EjectionSpeed.y +
                            gunTransform.forward * weapon.EjectionSpeed.z).normalized;
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
                                       - gunTransformForward * data.ejectedRoundRadius * 3.5f
                                       + bulletTransform.forward * data.ejectedRoundHeight * 0.3f
                                       + bulletTransform.forward * data.randomPosAndRot[0];


            /* These are the weird cases where the default positioning doesnt work well */

            // NOT IMPLEMENTED 
            
            /* Now setting the slide to the end of the bullet */

            var dx = weapon.Chamber.transform.localPosition.z - bulletTransform.localPosition.z - data.ejectedRoundHeight/2;
            var forwardPositionLimit = data.defaultFrontPosition - dx - 1f * data.ejectedRoundRadius;
            
            ___m_boltZ_forward = forwardPositionLimit;

            data.timeSinceStovepiping += Time.deltaTime;
        }

        [HarmonyPatch(typeof(OpenBoltReceiver), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool AbortExtractingMagIfStovepiping(OpenBoltReceiver __instance)
        {
            var data = __instance.gameObject.GetComponent(typeof(StovepipeData)) 
                as StovepipeData;

            if (data is null) return true;
            if (!data.IsStovepiping) return true;
            
            __instance.PlayAudioEvent(FirearmAudioEventType.BoltSlideForwardHeld, 1f);
            return false;
        }

        [HarmonyPatch(typeof(OpenBoltReceiverBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void UnStovepipeIfBoltNotJammedAgainstBullet(OpenBoltReceiverBolt __instance, 
            float ___m_boltZ_forward, float ___m_boltZ_current)
        {
            var data = __instance.Receiver.Bolt.GetComponent<StovepipeData>();
            
            if (data == null) return;
            if (!data.IsStovepiping) return;
            if (data.ejectedRound is null) return;
            if (!__instance.Receiver.IsBoltCatchEngaged()) return;
            if (!DoesBulletAimAtFloor(data.ejectedRound) && 
                !CouldBulletFallOutGunHorizontally(__instance.Receiver.RootRigidbody, data.ejectedRound.transform.forward)) return;
            if ((___m_boltZ_current - ___m_boltZ_forward) > -0.005f) return;
            if (data.ejectedRound.GetComponent<BulletStovepipeData>().timeSinceStovepiped < 0.2f) return;

            UnStovepipe(data, true, __instance.Receiver.RootRigidbody);
        }
        
        [HarmonyPatch(typeof(OpenBoltReceiver), "BeginChamberingRound")]
        [HarmonyPrefix]
        private static bool StopFromChamberingRoundIfStovepiping(OpenBoltReceiver __instance)
        {
            var data = __instance.Bolt.GetComponent<StovepipeData>();
            
            if (data == null) return true;
            if (!data.IsStovepiping) return true;
            if (data.ejectedRound is null) return true;

            return false;
        }
    }
}