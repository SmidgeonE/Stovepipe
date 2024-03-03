using System;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe.StovepipePatches
{
    public class StovepipeHandGrabPatches
    {
        [HarmonyPatch(typeof(FVRViveHand), "TestCollider")]
        [HarmonyPostfix]
        private static void UnStovepipeBulletIfHitWithHand(FVRViveHand __instance, Collider collider, bool isEnter)
        {
            if (!isEnter) return;
            
            var bulletStoveData = collider.GetComponent<BulletStovepipeData>();

            if (bulletStoveData == null) return;
            if (!bulletStoveData.data.IsStovepiping) return;

            var handRb = __instance.GetComponent<Rigidbody>();
            var data = bulletStoveData.data;
            var bullet = data.ejectedRound;
            var weaponRb = bullet.transform.parent.GetComponent<Rigidbody>();
            var relativeVelocityOfHand = handRb.velocity - weaponRb.velocity;

            if (__instance.CurrentInteractable != null) return;
            if (!__instance.Input.GripPressed ) return;
            
            if (relativeVelocityOfHand.magnitude < 0.03f)
            {
                SM.PlayHandlingGrabSound(bullet.HandlingGrabSound, bullet.transform.position, false);
            }
            else
            {
                SM.PlayHandlingGrabSound(bullet.HandlingGrabSound, bullet.transform.position, true);
                bullet.transform.position += bullet.transform.up * 0.1f;
                StovepipeBase.UnStovepipe(data, true, weaponRb);
                var bulletRb = bullet.GetComponent<Rigidbody>();
                bullet.SetAllCollidersToLayer(true, "Water");
                bulletRb.AddForce(relativeVelocityOfHand * 2f, ForceMode.Impulse);
            }
        }

        private static Transform FindRoundEjectionPositionGeneric(FVRFireArm fireArm)
        {
            switch (fireArm)
            {
                case ClosedBoltWeapon closedBoltWeapon:
                    return closedBoltWeapon.RoundPos_Ejection;
                case Handgun handgun:
                    return handgun.RoundPos_Ejection;
                case OpenBoltReceiver openBoltReceiver:
                    return openBoltReceiver.RoundPos_Ejection;
                case TubeFedShotgun tubeFedShotgun:
                    return tubeFedShotgun.RoundPos_Ejection;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fireArm));
            }
        }
    }
}