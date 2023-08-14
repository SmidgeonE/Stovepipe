﻿using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe
{
    public class StovepipeBase
    {
        protected const float TimeUntilCanPhysicsSlideUnStovepipe = 0.1f;
        private const float BoltRadiusTolerance = 1.5f;
        
        protected static float[] GenerateRandomNoise()
        {
            // Returns a 3-array of floats, first being randomness in the up/down pos, 
            // Next being random angle about the forward slide direction
            // Final being random angle about the perpendicular slide direction (left / right)
            // The rotation about the forward axis is randomised more to the right, as most handguns eject from the right

            return new[] { Random.Range(0.003f, 0.012f), -35f + Random.Range(-15f, 20f), Random.Range(0, 15f) };
        }

        protected static void StartStovepipe(StovepipeData data)
        {
            data.roundDefaultLayer = data.ejectedRound.gameObject.layer;

            data.ejectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            data.ejectedRound.RootRigidbody.velocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            data.ejectedRound.RootRigidbody.useGravity = false;
            data.ejectedRound.RootRigidbody.detectCollisions = false;
            data.hasBulletBeenStovepiped = true;
            data.timeSinceStovepiping = 0f;

            if (data.transform.parent != null)
                data.ejectedRound.SetParentage(data.transform);
            else data.ejectedRound.SetParentage(data.transform.parent);

            // DEBUG CUUUUUUUUUBE
            /*var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "beep";
            cube.transform.localScale = Vector3.one * 0.01f;
            cube.transform.position = slideData.gameObject.GetComponent<HandgunSlide>().Handgun.RoundPos_Ejection.position;
            if (slideData.transform.parent != null)
                cube.transform.parent = slideData.transform.parent;
            else cube.transform.parent = slideData.transform;*/
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

        protected static void UnStovepipe(StovepipeData data, bool breakParentage)
        {
            data.ejectedRound.RootRigidbody.useGravity = true;
            data.hasBulletBeenStovepiped = false;
            data.IsStovepiping = false;
            data.ejectedRound.gameObject.layer = data.roundDefaultLayer;
            data.ejectedRound.RootRigidbody.maxAngularVelocity = 1000f;
            data.ejectedRound.RootRigidbody.detectCollisions = true;
            data.timeSinceStovepiping = 0f;
            if (breakParentage) data.ejectedRound.SetParentage(null);

            /*Object.Destroy(GameObject.Find("beep"));*/
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
        
                
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            
            if (!bulletData.data.IsStovepiping && !GetIfCasingIsStillInsideAction(bulletData.data)) return;
            
            ___m_killAfter = 5f;
        }
        
        [HarmonyPatch(typeof(FVRFireArmRound), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void BulletGrabUnStovepipes(FVRFireArmRound __instance)
        {
            if (!__instance.IsHeld) return;
            
            var bulletData = __instance.gameObject.GetComponent<BulletStovepipeData>();
            if (bulletData is null) return;
            if (!bulletData.data.IsStovepiping) return;

            UnStovepipe(bulletData.data, false);
        }
        
        protected static bool GetIfCasingIsStillInsideAction(Transform actionTransform, FVRFireArmRound round, float radius)
        {
            Debug.Log("a");
            if (round is null) return false;
            Debug.Log("b");
            if (actionTransform is null) Debug.Log("transform is null");
            Debug.Log("c");
            if (!round.IsSpent) return false;
            Debug.Log("d");

            if (actionTransform is null) Debug.Log("what the fuckasd");
            return (actionTransform.position - round.transform.position).magnitude < BoltRadiusTolerance * radius;
        }

        protected static bool GetIfCasingIsStillInsideAction(StovepipeData data)
        {
            Debug.Log("Beep");
            return GetIfCasingIsStillInsideAction(data.transform, data.ejectedRound, data.boltOrSlideRadius);
        }
    }
}