using FistVR;
using HarmonyLib;
using UnityEngine;
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

        protected static void StartStovepipe(StovepipeData data, bool setParentToWeapon = false)
        {
            data.ejectedRound.RootRigidbody.velocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.angularVelocity = Vector3.zero;
            data.ejectedRound.RootRigidbody.maxAngularVelocity = 0;
            data.ejectedRound.RootRigidbody.useGravity = false;
            data.ejectedRound.RootRigidbody.detectCollisions = false;
            data.hasBulletBeenStovepiped = true;
            data.timeSinceStovepiping = 0f;
            
            data.ejectedRound.StoreAndDestroyRigidbody();
            data.ejectedRoundCollider.isTrigger = false;

            if (setParentToWeapon)
            {
                data.ejectedRound.SetParentage(data.GetComponent<ClosedBolt>().Weapon.transform);
                return;
            }
            
            if (data.transform.parent != null)
                data.ejectedRound.SetParentage(data.transform);
            else data.ejectedRound.SetParentage(data.transform.parent);

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
            
            if (!bulletData.data.IsStovepiping) return;
            
            ___m_killAfter = 5f;
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
 
            // Playing sound after bullet is unstovepiped.
            
            var handgun = data.GetComponent<Handgun>();
            if (handgun != null)
                handgun.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
            else
                data.GetComponent<ClosedBoltWeapon>().PlayAudioEvent(FirearmAudioEventType.BoltSlideForward, 1f);
        }

        protected static bool DoesBulletAimAtFloor(FVRFireArmRound round)
        {
            return Vector3.Dot(round.transform.forward, Vector3.down) > 0;
        }

        protected static bool IsRifleThatEjectsUpwards(Transform ejectionPos, ClosedBolt bolt, FVRFireArmRound round)
        {
            return Vector3.Dot(ejectionPos.position - bolt.transform.position, bolt.transform.up) > 0.017f
                   && round.RoundType != FireArmRoundType.a9_19_Parabellum && round.RoundType != FireArmRoundType.a10mmAuto;
        }

        protected static bool CouldBulletFallOutGunHorizontally(Rigidbody weaponRb,
            Vector3 bulletDirection)
        {
            return Vector3.Dot(weaponRb.velocity - GM.CurrentPlayerBody.Hitboxes[1].GetComponent<Rigidbody>().velocity, bulletDirection) > 0.5f;
        }
    }
}