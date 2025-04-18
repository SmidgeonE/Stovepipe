﻿using System;
using FistVR;
using HarmonyLib;
using Stovepipe.Debug;
using Stovepipe.ModFiles;
using UnityEngine;
using Valve.VR.InteractionSystem;
using Random = UnityEngine.Random;

namespace Stovepipe.StovepipePatches
{
    public class StovepipeBase
    {
        protected const float TimeUntilCanPhysicsSlideUnStovepipe = 0.1f;

        protected static float[] GenerateRandomHandgunNoise()
        {
            // Returns a 3-array of floats, first being randomness in the up/down pos, 
            // Next being random angle about the forward slide direction
            // Final being random angle about the perpendicular slide direction (left / right)
            // The rotation about the forward axis is randomised more to the right, as most handguns eject from the right

            return new[] { Random.Range(0.003f, 0.012f), -35f + Random.Range(-15f, 20f), Random.Range(0, 15f) };
        }
        
        protected static float[] GenerateRandomRifleNoise()
        {
            return new[] { Random.Range(0.005f, 0.011f), Random.Range(0, -20f), Random.Range(0, 15f) };
        }

        protected static void StartStovepipe(StovepipeData data)
        {
            DebugMode.DebugLog("Starting Stovepipe");
            if (data is null) return;
            if (data.ejectedRound == null) return;
            if (data.thisDoubleFeedData != null && data.thisDoubleFeedData.isDoubleFeeding) return;
            if (data.ejectedRound.RootRigidbody is null) return;
           
            DebugMode.DebugLog("Checkpoint 1");
            data.ejectedRound.RootRigidbody.velocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            data.ejectedRound.RootRigidbody.useGravity = false;
            data.ejectedRound.RootRigidbody.detectCollisions = false;

            DebugMode.DebugLog("Checkpoint 1");
            data.hasBulletBeenStovepiped = true;
            data.timeSinceStovepiping = 0f;
            data.numOfRoundsSinceLastJam = 0;
            data.SetStoveProbToMin();
            
            DebugMode.DebugLog("Checkpoint 3");
            if (data.thisDoubleFeedData != null) data.thisDoubleFeedData.SetDoubleFeedProbToMin();

            DebugMode.DebugLog("CheckPoint 3");
            data.ejectedRound.StoreAndDestroyRigidbody();
            data.ejectedRoundCollider.isTrigger = false;

            DebugMode.DebugLog("CheckPoint 4");
            switch (data.weaponType)
            {
                case WeaponType.Handgun:
                    data.ejectedRound.SetParentage(data.GetComponent<HandgunSlide>().Handgun.transform);
                    break;
                case WeaponType.Rifle:
                    data.ejectedRound.SetParentage(data.GetComponent<ClosedBolt>().Weapon.transform);
                    DebugMode.DebugLog("Checkpoint 5");
                    break;
                case WeaponType.TubeFedShotgun:
                    data.ejectedRound.SetParentage(data.GetComponent<TubeFedShotgunBolt>().Shotgun.transform);
                    break;
                case WeaponType.OpenBolt:
                    data.ejectedRound.SetParentage(data.GetComponent<OpenBoltReceiverBolt>().Receiver.transform);
                    break;
            }


            /*
            if (data.transform.parent != null)
                data.ejectedRound.SetParentage(data.transform);
            else data.ejectedRound.SetParentage(data.transform.parent);
            */

            // DEBUG CUUUUUUUUUBE
            /*var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "beep";
            cube.transform.localScale = Vector3.one * 0.01f;
            cube.transform.position = data.gameObject.GetComponent<ClosedBolt>().Weapon.RoundPos_Ejection.position;
            if (data.transform.parent != null)
                cube.transform.parent = data.transform.parent;
            else cube.transform.parent = data.transform;*/
        }

        public static void UnStovepipe(StovepipeData data, bool breakParentage, Rigidbody weaponRb)
        {
            data.ejectedRound.RecoverRigidbody();
            data.ejectedRoundCollider.isTrigger = true;
            data.ejectedRound.RootRigidbody.useGravity = true;
            data.hasBulletBeenStovepiped = false;
            data.IsStovepiping = false;
            data.ejectedRound.RootRigidbody.maxAngularVelocity = 1000f;
            data.ejectedRound.RootRigidbody.detectCollisions = true;
            data.timeSinceStovepiping = 0f;
            data.hasBulletsPositionBeenSet = false;

            if (breakParentage) data.ejectedRound.SetParentage(null);
            if (weaponRb == null) return;
            
            data.ejectedRound.RootRigidbody.velocity = weaponRb.velocity;
            data.ejectedRound.RootRigidbody.angularVelocity = weaponRb.angularVelocity;

            /*Object.Destroy(GameObject.Find("beep"));*/
        }

        protected static Vector3 GetVectorThatPointsOutOfEjectionPort(HandgunSlide slide)
        {
            var ejectionDir = slide.Handgun.RoundPos_Ejection.position - slide.transform.position;
            var componentAlongSlide = Vector3.Dot(slide.transform.forward, ejectionDir);
            ejectionDir -= componentAlongSlide * slide.transform.forward;

            return ejectionDir.normalized;
        }

        protected static Vector3 GetVectorThatPointsOutOfEjectionPort(ClosedBolt bolt)
        {
            var ejectionDir = bolt.Weapon.RoundPos_Ejection.position - bolt.transform.position;
            var componentAlongSlide = Vector3.Dot(bolt.transform.forward, ejectionDir);
            ejectionDir -= componentAlongSlide * bolt.transform.forward;

            return ejectionDir.normalized;
        }
        
        protected static Vector3 GetVectorThatPointsOutOfEjectionPort(TubeFedShotgunBolt bolt)
        {
            var ejectionDir = bolt.Shotgun.RoundPos_Ejection.position - bolt.transform.position;
            var componentAlongSlide = Vector3.Dot(bolt.transform.forward, ejectionDir);
            ejectionDir -= componentAlongSlide * bolt.transform.forward;

            return ejectionDir.normalized;
        }
        
        protected static Vector3 GetVectorThatPointsOutOfEjectionPort(OpenBoltReceiverBolt bolt)
        {
            var ejectionDir = bolt.Receiver.RoundPos_Ejection.position - bolt.transform.position;
            var componentAlongSlide = Vector3.Dot(bolt.transform.forward, ejectionDir);
            ejectionDir -= componentAlongSlide * bolt.transform.forward;

            return ejectionDir.normalized;
        }


        public static bool FindIfGunEjectsToTheLeft(HandgunSlide slide)
        {
            // returns true if left, false if not.

            var dirOutOfEjectionPort = GetVectorThatPointsOutOfEjectionPort(slide);
            var componentToTheRight = Vector3.Dot(dirOutOfEjectionPort, slide.transform.right);

            return componentToTheRight < -0.005f;
        }

        public static bool FindIfGunEjectsToTheLeft(ClosedBolt bolt)
        {
            // returns true if left, false if not.

            var dirOutOfEjectionPort = GetVectorThatPointsOutOfEjectionPort(bolt);
            var componentToTheRight = Vector3.Dot(dirOutOfEjectionPort, bolt.transform.right);

            return componentToTheRight < -0.005f;
        }
        
        public static bool FindIfGunEjectsToTheLeft(TubeFedShotgunBolt bolt)
        {
            // returns true if left, false if not.

            var dirOutOfEjectionPort = GetVectorThatPointsOutOfEjectionPort(bolt);
            var componentToTheRight = Vector3.Dot(dirOutOfEjectionPort, bolt.transform.right);

            return componentToTheRight < -0.005f;
        }
        
        public static bool FindIfGunEjectsToTheLeft(OpenBoltReceiverBolt bolt)
        {
            // returns true if left, false if not.

            var dirOutOfEjectionPort = GetVectorThatPointsOutOfEjectionPort(bolt);
            var componentToTheRight = Vector3.Dot(dirOutOfEjectionPort, bolt.transform.right);

            return componentToTheRight < -0.005f;
        }
        
                
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            
            if (!bulletData.data.IsStovepiping) return;
            
            switch (GM.Options.SimulationOptions.ShellTime)
            {
                case SimulationOptions.SpentShellDespawnTime.Seconds_5:
                    ___m_killAfter = 5f;
                    break;
                case SimulationOptions.SpentShellDespawnTime.Seconds_10:
                    ___m_killAfter = 10f;
                    break;
                case SimulationOptions.SpentShellDespawnTime.Seconds_30:
                    ___m_killAfter = 30f;
                    break;
                case SimulationOptions.SpentShellDespawnTime.Infinite:
                    ___m_killAfter = 999999f;
                    break;
            }
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "BeginInteraction")]
        [HarmonyPrefix]
        private static void UnstovepipeWhenGrabbed(FVRFireArmRound __instance)
        {
            var bulletData = __instance.GetComponent<BulletStovepipeData>();

            if (bulletData is null) return;
            
            var data = bulletData.data;

            if (!data.IsStovepiping) return;
            
            UnStovepipe(data, true, null);

            switch (data.weaponType)
            {
                case WeaponType.Handgun:
                    data.GetComponent<HandgunSlide>().Handgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
                    break;
                case WeaponType.Rifle:
                    data.GetComponent<ClosedBolt>().Weapon.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
                    break;
                case WeaponType.TubeFedShotgun:
                    data.GetComponent<TubeFedShotgunBolt>().Shotgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
                    break;
                case WeaponType.OpenBolt:
                    data.GetComponent<OpenBoltReceiverBolt>().Receiver.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
                    break;
                default:
                    throw new Exception("Sound for bullet grab not found!");
            }
        }

        protected static bool DoesBulletAimAtFloor(FVRFireArmRound round)
        {
            return Vector3.Dot(round.transform.forward, Vector3.down) > 0;
        }

        protected static bool IsRifleThatEjectsUpwards(Transform ejectionPos, Transform boltTransform, FVRFireArmRound round)
        {
            return Vector3.Dot(ejectionPos.position - boltTransform.transform.position, boltTransform.transform.up) > 0.017f
                   && round.RoundType != FireArmRoundType.a9_19_Parabellum && round.RoundType != FireArmRoundType.a10mmAuto;
        }
        
        protected static bool CouldBulletFallOutGunHorizontally(Rigidbody weaponRb,
            Vector3 bulletDirection)
        {
            return Vector3.Dot(weaponRb.velocity - GM.CurrentPlayerBody.Hitboxes[1].GetComponent<Rigidbody>().velocity, bulletDirection) > 0.5f;
        }
    }
}