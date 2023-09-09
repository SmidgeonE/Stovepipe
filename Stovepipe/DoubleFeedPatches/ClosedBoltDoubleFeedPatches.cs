using System.Data.Common;
using FistVR;
using HarmonyLib;
using UnityEngine;
using UnityEngineInternal;

namespace Stovepipe.DoubleFeedPatches
{
    public class ClosedBoltDoubleFeedPatches : DoubleFeedBase
    {
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_ExtractRoundFromMag")]
        [HarmonyPostfix]
        private static void DoubleFeedDiceroll(ClosedBolt __instance)
        {
            if (__instance.IsHeld) return;
            if (__instance.Weapon.Handle != null && __instance.Weapon.Handle.IsHeld) return;
            if (__instance.Weapon.Magazine == null) return;
            if (__instance.Weapon.Magazine.m_numRounds < 2) return;
            if (__instance.Weapon.Chamber.GetRound() != null) return;

            var stoveData = __instance.Weapon.Bolt.GetComponent<StovepipeData>();
            if (stoveData != null && stoveData.IsStovepiping) return;
            
            var data = __instance.Weapon.gameObject.GetComponent<DoubleFeedData>() ?? __instance.Weapon.gameObject.AddComponent<DoubleFeedData>();
            data.SetProbability(true);
            
            if (data.IsDoubleFeeding) return;
            
            data.hasUpperBulletBeenRemoved = false;
            data.hasLowerBulletBeenRemoved = false;
            
            data.IsDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.DoubleFeedChance;
            if (!data.IsDoubleFeeding) return;

            // Double Feed

            data.BulletRandomness = GenerateRandomOffsets();
            GenerateUnJammingProbs(data);
            data.hasFinishedEjectingDoubleFeedRounds = false;

            var managedToChamber = __instance.Weapon.ChamberRound();
            if (!managedToChamber)
            {
                __instance.Weapon.BeginChamberingRound();
                __instance.Weapon.ChamberRound();
            }

            data.upperBullet = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position,
                Vector3.zero, Vector3.zero, false);

            __instance.Weapon.BeginChamberingRound();
            __instance.Weapon.ChamberRound();
            data.lowerBullet = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position + Vector3.down * 0.3f,
                Vector3.zero, Vector3.zero, false);
            
            data.hasFinishedEjectingDoubleFeedRounds = true;

            data.upperBulletCol = data.upperBullet.gameObject.GetComponent<CapsuleCollider>();
            data.bulletHeight = data.upperBulletCol.height;
            data.bulletRadius = data.upperBulletCol.radius;
            
            var upperBulletData = data.upperBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.upperBullet.gameObject.AddComponent<BulletDoubleFeedData>();
            var lowerBulletData = data.lowerBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.lowerBullet.gameObject.AddComponent<BulletDoubleFeedData>();

            upperBulletData.gunData = data;
            lowerBulletData.gunData = data;

            SetBulletToNonInteracting(data.upperBullet, data, true, __instance.Weapon.transform);
            SetBulletToNonInteracting(data.lowerBullet, data, true, __instance.Weapon.transform);

            // Applying procedural positioning

            var upperBulletTransform = data.upperBullet.transform;
            var chamberProxyRoundPos = __instance.Weapon.Chamber.ProxyRound.position;

            upperBulletTransform.position = chamberProxyRoundPos 
                                            + __instance.Weapon.transform.up * data.bulletRadius
                                            - upperBulletTransform.forward * data.bulletHeight * 1.05f
                                            + data.BulletRandomness[0, 0] * upperBulletTransform.right;
            
            upperBulletTransform.Rotate(upperBulletTransform.up, data.BulletRandomness[0, 1], Space.Self);
            upperBulletTransform.Rotate(upperBulletTransform.right, data.BulletRandomness[0, 2], Space.Self);


            var lowerBulletTransform = data.lowerBullet.transform;

            lowerBulletTransform.position = chamberProxyRoundPos 
                                            - __instance.Weapon.transform.up * data.bulletRadius
                                            - lowerBulletTransform.forward * data.bulletHeight * 1.05f
                                            + data.BulletRandomness[1, 0] * lowerBulletTransform.right;
            
            lowerBulletTransform.Rotate(lowerBulletTransform.up, data.BulletRandomness[1, 1], Space.Self);
            lowerBulletTransform.Rotate(lowerBulletTransform.right, data.BulletRandomness[1, 2], Space.Self);
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void SetBoltForwardsLocation(ClosedBolt __instance,
            ref float ___m_boltZ_forward)
        {
            var stovepipeData = __instance.Weapon.Bolt.GetComponent<StovepipeData>();
            if (stovepipeData != null && stovepipeData.IsStovepiping)
            {
                return;
            }
            
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding)
            {
                ___m_boltZ_forward = __instance.Weapon.Bolt.Point_Bolt_Forward.localPosition.z;
            }
            else
            {
                var newFrontPos = __instance.Point_Bolt_Forward.localPosition.z - data.upperBulletCol.height * 1.2f;
                ___m_boltZ_forward = newFrontPos;
            }
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlideAndJiggling(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();
            if (__instance.CurPos != ClosedBolt.BoltPos.LockedToRear
                && __instance.CurPos != ClosedBolt.BoltPos.Rear
                && __instance.CurPos != ClosedBolt.BoltPos.Locked) return;
            if (data is null || !data.IsDoubleFeeding) return;
            if (__instance.Weapon.Magazine != null) return;
            if (!IsGunShaking(__instance.Weapon.RootRigidbody)) return;

            if (data.slideRackAndJiggleUnjamsLowerBullet && !data.hasLowerBulletBeenRemoved)
                SetBulletToInteracting(data.lowerBullet, data, true, __instance.Weapon.RootRigidbody);
            
            if (data.slideRackAndJiggleUnjamsUpperBullet && !data.hasUpperBulletBeenRemoved)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Weapon.RootRigidbody);
            
            // Special Case: lower bullet falls out just by opening bolt, but the top one needs cajoling.
            
            if (data.hasLowerBulletBeenRemoved && data.slideRackUnjamsLowerButRackAndJiggleUnjamsUpper)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Weapon.RootRigidbody);
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_SmackRear")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlide(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding || __instance.Weapon.Magazine != null) return;

            if (data.slideRackUnjamsLowerBullet && !data.hasLowerBulletBeenRemoved)
                SetBulletToInteracting(data.lowerBullet, data, true, __instance.Weapon.RootRigidbody);
            
            if (data.slideRackUnjamsUpperBullet && !data.hasUpperBulletBeenRemoved)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Weapon.RootRigidbody);
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void ChangeBulletInteractability(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();
            if (data is null) return;
            if (!data.IsDoubleFeeding) return;

            var uninteractableLayer = LayerMask.NameToLayer("Water");
            var normalLayer = LayerMask.NameToLayer("Interactable");
            var lowerBulletExists = !data.hasLowerBulletBeenRemoved;
            var upperBulletExists = !data.hasUpperBulletBeenRemoved;

            if (__instance.Weapon.Magazine != null)
            {
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = uninteractableLayer;
                if (upperBulletExists) data.upperBullet.gameObject.layer = uninteractableLayer;
            }
            else
            {
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = normalLayer;
                
                if (upperBulletExists && lowerBulletExists)
                {
                    data.upperBullet.gameObject.layer = uninteractableLayer;
                }
                else if (upperBulletExists && !lowerBulletExists)
                {
                    data.upperBullet.gameObject.layer = normalLayer;
                }
            }
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "BeginChamberingRound")]
        [HarmonyPrefix]
        private static bool DontChamberRoundIfDoubleFeeding(ClosedBoltWeapon __instance)
        {
            var data = __instance.GetComponent<DoubleFeedData>();

            if (data is null) return true;

            return !data.IsDoubleFeeding || !data.hasFinishedEjectingDoubleFeedRounds;
        }
    }
}