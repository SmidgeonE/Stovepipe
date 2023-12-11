using System.Data.Common;
using FistVR;
using HarmonyLib;
using Stovepipe.Debug;
using UnityEngine;
using UnityEngineInternal;

namespace Stovepipe.DoubleFeedPatches
{
    public class ClosedBoltDoubleFeedPatches : DoubleFeedBase
    {
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_ExtractRoundFromMag")]
        [HarmonyPostfix]
        private static void DoubleFeedDiceroll(ClosedBolt __instance, ref float ___m_boltZ_forward)
        {
            if (__instance.IsHeld) return;
            if (__instance.Weapon.Handle != null && __instance.Weapon.Handle.IsHeld) return;
            if (__instance.Weapon.Magazine == null) return;
            if (__instance.Weapon.Magazine.m_numRounds < 2) return;
            if (__instance.Weapon.Chamber.GetRound() != null) return;

            var stoveData = __instance.Weapon.Bolt.GetComponent<StovepipeData>();
            if (stoveData != null &&
                (stoveData.IsStovepiping || 
                 stoveData.numOfRoundsSinceLastJam < UserConfig.MinRoundBeforeNextJam.Value || 
                 stoveData.isWeaponBatteryFailing)) return;
            
            
            var data = __instance.Weapon.gameObject.GetComponent<DoubleFeedData>() ?? __instance.Weapon.gameObject.AddComponent<DoubleFeedData>();
            if (!data.hasSetDefaultChance) data.SetProbability(true);
            data.thisWeaponsStovepipeData = stoveData;
            
            if (data.isDoubleFeeding) return;
            
            data.hasUpperBulletBeenRemoved = false;
            data.hasLowerBulletBeenRemoved = false;
            
            // Check the weapon isnt caseless, nor is the ammo
            
            var exampleRound = AM.GetRoundSelfPrefab(__instance.Weapon.Chamber.RoundType,
                AM.GetDefaultRoundClass(__instance.Weapon.Chamber.RoundType)).GetGameObject();
            if (exampleRound.GetComponent<FVRFireArmRound>().IsCaseless) return;

            
            data.isDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.doubleFeedChance;
            if (!data.isDoubleFeeding) return;

            // Double Feed

            data.BulletRandomness = GenerateRandomOffsets();
            GenerateUnJammingProbs(data, true);
            data.hasFinishedEjectingDoubleFeedRounds = false;
            
            data.Adjustments = DebugIO.ReadDoubleFeedAdjustment(__instance.Weapon.name);
            if (data.Adjustments != null) data.hasFoundAdjustments = true;

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

            var upperBulletCol = data.upperBullet.gameObject.GetComponent<CapsuleCollider>();
            data.bulletHeight = upperBulletCol.height;
            data.bulletRadius = upperBulletCol.radius;
            
            var upperBulletData = data.upperBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.upperBullet.gameObject.AddComponent<BulletDoubleFeedData>();
            var lowerBulletData = data.lowerBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.lowerBullet.gameObject.AddComponent<BulletDoubleFeedData>();

            upperBulletData.gunData = data;
            lowerBulletData.gunData = data;

            SetBulletToNonInteracting(data.upperBullet, data, true, __instance.Weapon.transform);
            SetBulletToNonInteracting(data.lowerBullet, data, true, __instance.Weapon.transform);
            
            var upperBulletTransform = data.upperBullet.transform;
            var lowerBulletTransform = data.lowerBullet.transform;

            if (data.hasFoundAdjustments)
            {
                upperBulletTransform.localPosition = data.Adjustments.UpperBulletLocalPos;
                upperBulletTransform.localRotation = data.Adjustments.UpperBulletDir;
                
                lowerBulletTransform.localPosition = data.Adjustments.LowerBulletLocalPos;
                lowerBulletTransform.localRotation = data.Adjustments.LowerBulletDir;

                ___m_boltZ_forward = data.Adjustments.BoltZ;
                return;
            }

            // Applying procedural positioning, assuming no adjustment has been found

            var chamberProxyRoundPos = __instance.Weapon.Chamber.ProxyRound.position;

            upperBulletTransform.position = chamberProxyRoundPos 
                                            + __instance.Weapon.transform.up * data.bulletRadius
                                            - upperBulletTransform.forward * data.bulletHeight * 1.05f
                                            + data.BulletRandomness[0, 0] * upperBulletTransform.right;
            
            upperBulletTransform.Rotate(upperBulletTransform.up, data.BulletRandomness[0, 1], Space.Self);
            upperBulletTransform.Rotate(upperBulletTransform.right, data.BulletRandomness[0, 2], Space.Self);
            

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
            ref float ___m_boltZ_forward, float ___m_boltZ_rear)
        {
            var stovepipeData = __instance.Weapon.Bolt.GetComponent<StovepipeData>();
            if (stovepipeData != null && (stovepipeData.IsStovepiping || stovepipeData.isWeaponBatteryFailing)) return;

            var data = __instance.Weapon.GetComponent<DoubleFeedData>();
            if (data == null) return;

            if (data.hasFoundAdjustments && data.isDoubleFeeding)
            {
                ___m_boltZ_forward = data.Adjustments.BoltZ;
                return;
            }
            
            if (!data.isDoubleFeeding)
                ___m_boltZ_forward = __instance.Weapon.Bolt.Point_Bolt_Forward.localPosition.z;
            else
            {
                var newFrontPos = __instance.Point_Bolt_Forward.localPosition.z - data.bulletHeight * 1.2f;
                if (newFrontPos < ___m_boltZ_rear) newFrontPos = ___m_boltZ_rear;
                
                ___m_boltZ_forward = newFrontPos;
            }
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "Fire")]
        [HarmonyPostfix]
        private static void BeepUpdate(ClosedBoltWeapon __instance)
        {
            /*var data = __instance.GetComponent<DoubleFeedData>();
            if (__instance.GetComponent<DoubleFeedData>() == null) return;
            
            UnityEngine.Debug.Log("Double Feed Prob : " + data.doubleFeedChance + " " + data.doubleFeedMaxChance);
            
            if (data.thisWeaponsStovepipeData != null)
                UnityEngine.Debug.Log("stovepipe : " + data.thisWeaponsStovepipeData.stovepipeProb + 
                                      " " + data.thisWeaponsStovepipeData.stovepipeMaxProb);*/
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlideAndJiggling(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();
            if (__instance.CurPos != ClosedBolt.BoltPos.LockedToRear
                && __instance.CurPos != ClosedBolt.BoltPos.Rear
                && __instance.CurPos != ClosedBolt.BoltPos.Locked) return;
            if (data is null || !data.isDoubleFeeding) return;
            if (__instance.Weapon.Magazine != null) return;
            if (!IsGunShaking(__instance.Weapon.RootRigidbody)) return;

            if (data.slideRackAndJiggleUnjamsLowerBullet && !data.hasLowerBulletBeenRemoved)
                SetBulletToInteracting(data.lowerBullet, data, true, __instance.Weapon.RootRigidbody);
            
            if (data.slideRackAndJiggleUnjamsUpperBullet && !data.hasUpperBulletBeenRemoved)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Weapon.RootRigidbody);
            
            // Special Case: lower bullet falls out just by opening bolt, but the top one needs cajoling.
            
            if (data.hasLowerBulletBeenRemoved && !data.hasUpperBulletBeenRemoved
                                               && data.slideRackUnjamsLowerButRackAndJiggleUnjamsUpper)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Weapon.RootRigidbody);
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_SmackRear")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlide(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (data is null || !data.isDoubleFeeding || __instance.Weapon.Magazine != null) return;

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
            if (!data.isDoubleFeeding) return;

            var uninteractableLayer = LayerMask.NameToLayer("Water");
            var normalLayer = LayerMask.NameToLayer("Interactable");
            var lowerBulletExists = !data.hasLowerBulletBeenRemoved;
            var upperBulletExists = !data.hasUpperBulletBeenRemoved;

            if (__instance.Weapon.Magazine != null)
            {
                if (__instance.Weapon.Magazine.IsIntegrated || __instance.Weapon.Magazine.IsEnBloc)
                {
                    if (lowerBulletExists) data.lowerBullet.gameObject.layer = normalLayer;
                    if (upperBulletExists) data.upperBullet.gameObject.layer = normalLayer;
                    return;
                }
                
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = uninteractableLayer;
                if (upperBulletExists) data.upperBullet.gameObject.layer = uninteractableLayer;
            }
            else
            {
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = normalLayer;
                if (upperBulletExists && lowerBulletExists) data.upperBullet.gameObject.layer = uninteractableLayer;
                if (upperBulletExists && !lowerBulletExists) data.upperBullet.gameObject.layer = normalLayer;
            }
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "BeginChamberingRound")]
        [HarmonyPrefix]
        private static bool DontChamberRoundIfDoubleFeeding(ClosedBoltWeapon __instance)
        {
            var data = __instance.GetComponent<DoubleFeedData>();

            if (data is null) return true;

            return !data.isDoubleFeeding || !data.hasFinishedEjectingDoubleFeedRounds;
        }
    }
}