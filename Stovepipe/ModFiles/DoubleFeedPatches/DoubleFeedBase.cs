using System;
using System.Data.Common;
using System.Runtime.Remoting.Messaging;
using FistVR;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Stovepipe.DoubleFeedPatches
{
    public class DoubleFeedBase
    {
        protected static float[,] GenerateRandomOffsets()
        {
            // Will return a 2x3 array, each holding these:
            // 1. horizontal displacement 2. rotation about the upwards axis 3. rotation about the left/right axis.
            // The first array will be for the upper bullet, the second will be for the lower bullet;

            return new[,]
            {
                { Random.Range(-0.005f, 0.005f), Random.Range(-15f, 15f), Random.Range(-5f, 5f) },
                { Random.Range(-0.005f, 0.005f), Random.Range(-15f, 15f), Random.Range(-5f, 5f) }
            };
        }

        [HarmonyPatch(typeof(FVRFireArmMagazine), "FVRUpdate")]
        [HarmonyPostfix]
        private static void HideTopBulletIfDoubleFeeding(FVRFireArmMagazine __instance)
        {
            if (__instance.FireArm is null) return;

            var data = __instance.FireArm.GetComponent<DoubleFeedData>();
            if (data is null || !data.isDoubleFeeding) return;
            
            __instance.DisplayRenderers[0].enabled = false;

            if (__instance.m_numRounds == 1) __instance.DisplayRenderers[1].enabled = true;
        }

        [HarmonyPatch(typeof(FVRFireArmMagazine), "Release")]
        [HarmonyPrefix]
        private static void RevealTopBulletIfRemovedFromDoubleFeedingWeapon(FVRFireArmMagazine __instance)
        {
            if (__instance.FireArm is null) return;
            
            var data = __instance.FireArm.GetComponent<DoubleFeedData>();
            if (data is null || !data.isDoubleFeeding) return;
            
            __instance.DisplayRenderers[0].enabled = true;

            if (__instance.m_numRounds == 1) __instance.DisplayRenderers[1].enabled = false;
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "Chamber")]
        [HarmonyPrefix]
        private static bool StopBulletFromBeingChamberedIfDoubleFeeding(FVRFireArmRound __instance)
        {
            var data = __instance.GetComponent<BulletDoubleFeedData>();

            if (data == null) return true;
            if (__instance != data.gunData.upperBullet && __instance != data.gunData.lowerBullet) return true;

            return false;
        }


        [HarmonyPatch(typeof(FVRFireArmRound), "BeginInteraction")]
        [HarmonyPrefix]
        private static void UnDoubleFeedIfHeld(FVRFireArmRound __instance)
        {
            if (__instance.IsSpent) return;
            
            var bulletData = __instance.GetComponent<BulletDoubleFeedData>();

            if (bulletData is null || !bulletData.gunData.isDoubleFeeding) return;
            
            SetBulletToInteracting(__instance, bulletData.gunData, false, null);
        }

        protected static bool IsGunShaking(Rigidbody gunRb)
        {
            return (gunRb.velocity - GM.CurrentPlayerBody.Hitboxes[1].GetComponent<Rigidbody>().velocity).magnitude >
                   2f;
        }

        protected static void SetBulletToNonInteracting(FVRFireArmRound round, DoubleFeedData data, bool setParentToWeapon = false, Transform weaponTransform = null)
        {
            round.RootRigidbody.velocity = Vector3.zero;
            round.RootRigidbody.angularVelocity = Vector3.zero;
            round.RootRigidbody.maxAngularVelocity = 0;
            round.RootRigidbody.useGravity = false;
            round.RootRigidbody.detectCollisions = false;
            
            round.isMagazineLoadable = false;
            round.isManuallyChamberable = false;
            if (data.thisWeaponsStovepipeData != null)
            {
                data.thisWeaponsStovepipeData.numOfRoundsSinceLastJam = 0;
                data.thisWeaponsStovepipeData.SetStoveProbToMin();
            }
            
            data.SetDoubleFeedProbToMin();
            round.StoreAndDestroyRigidbody();
            round.GetComponent<BulletDoubleFeedData>().isThisBulletDoubleFeeding = true;

            if (!setParentToWeapon) return;
            
            round.SetParentage(weaponTransform);
        }

        protected static void SetBulletToInteracting(FVRFireArmRound round, DoubleFeedData data, bool breakParentage, Rigidbody weaponRb)
        {
            round.RecoverRigidbody();
            round.RootRigidbody.useGravity = true;
            round.RootRigidbody.maxAngularVelocity = 1000f;
            round.RootRigidbody.detectCollisions = true;
            round.isMagazineLoadable = true;

            round.GetComponent<BulletDoubleFeedData>().isThisBulletDoubleFeeding = false;

            if (round == data.upperBullet)
            {
                data.hasUpperBulletBeenRemoved = true;
                data.upperBullet = null;
            }
            else if (round == data.lowerBullet)
            {
                data.hasLowerBulletBeenRemoved = true;
                data.lowerBullet = null;
            }

            if (data.hasUpperBulletBeenRemoved && data.hasLowerBulletBeenRemoved)
                data.isDoubleFeeding = false;

            if (breakParentage) round.SetParentage(null);
            if (weaponRb == null) return;

            round.RootRigidbody.velocity = weaponRb.velocity;
            round.RootRigidbody.angularVelocity = weaponRb.angularVelocity;
        }

        protected static void GenerateUnJammingProbs(DoubleFeedData data, bool isRifle)
        {
            // Obviously, if you cant use one of the methods to unjam the lower bullet, then you necessarily cant use it
            // for the upper bullet

            if (!isRifle)
            {
                GenerateUnJammingProbsForPistols(data);
                return;
            }
            
            data.slideRackUnjamsLowerBullet = Random.Range(0f, 1f) < UserConfig.LowerBulletDropoutProb.Value;

            if (data.slideRackUnjamsLowerBullet)
                data.slideRackUnjamsUpperBullet =
                    Random.Range(0f, 1f) < UserConfig.UpperBulletDropoutProb.Value;
            else
                data.slideRackUnjamsUpperBullet = false;
            
            data.slideRackAndJiggleUnjamsLowerBullet = Random.Range(0f, 1f) < UserConfig.LowerBulletShakeyProb.Value;

            if (data.slideRackAndJiggleUnjamsLowerBullet)
                data.slideRackAndJiggleUnjamsUpperBullet =
                    Random.Range(0f, 1f) < UserConfig.UpperBulletShakeyProb.Value;
            else 
                data.slideRackAndJiggleUnjamsUpperBullet = false;
            
            
            // Special case probability where the lower bullet falls out, but the upper bullet needs cajoling
            data.slideRackUnjamsLowerButRackAndJiggleUnjamsUpper =
                Random.Range(0f, 1f) < UserConfig.UpperBulletShakeyProb.Value;
        }

        private static void GenerateUnJammingProbsForPistols(DoubleFeedData data)
        {
            data.slideRackUnjamsLowerBullet = Random.Range(0f, 1f) < UserConfig.LowerBulletDropoutProb.Value;
            data.slideRackUnjamsUpperBullet = true;
            data.slideRackAndJiggleUnjamsLowerBullet = Random.Range(0f, 1f) < UserConfig.LowerBulletShakeyProb.Value;
        }
    }
}