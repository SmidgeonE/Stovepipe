using FistVR;
using HarmonyLib;
using UnityEngine;
using UnityEngineInternal;

namespace Stovepipe.DoubleFeedPatches
{
    public class ClosedBoltDoubleFeedPatches
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

            var stoveData = __instance.Weapon.GetComponent<StovepipeData>();

            if (stoveData != null && stoveData.IsStovepiping) return;

            var data = __instance.Weapon.gameObject.GetComponent<DoubleFeedData>() ?? __instance.Weapon.gameObject.AddComponent<DoubleFeedData>();

            data.SetProbability(true);

            if (data.IsDoubleFeeding) return;
            
            data.hasUpperBulletBeenRemoved = false;
            data.hasLowerBulletBeenRemoved = false;

            data.IsDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.DoubleFeedChance;

            if (!data.IsDoubleFeeding) return;

            // Double Feed

            data.hasFinishedEjectingDoubleFeedRounds = false;

            __instance.Weapon.ChamberRound();
            data.upperBullet = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position,
                Vector3.zero, Vector3.zero, false);

            __instance.Weapon.BeginChamberingRound();
            __instance.Weapon.ChamberRound();
            
            data.lowerBullet = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position - Vector3.down * 0.1f,
                Vector3.zero, Vector3.zero, false);
            
            data.hasFinishedEjectingDoubleFeedRounds = true;

            data.upperBulletCol = data.upperBullet.gameObject.GetComponent<CapsuleCollider>();

            var firstRoundData = data.upperBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.upperBullet.gameObject.AddComponent<BulletDoubleFeedData>();
            var secondRoundData = data.lowerBullet.GetComponent<BulletDoubleFeedData>() ??
                                 data.lowerBullet.gameObject.AddComponent<BulletDoubleFeedData>();

            firstRoundData.gunData = data;
            secondRoundData.gunData = data;

            SetBulletToNonInteracting(data.upperBullet, data, true, __instance.Weapon.transform);
            SetBulletToNonInteracting(data.lowerBullet, data, true, __instance.Weapon.transform);

            // Generating Probs
            
            GenerateUnJammingProbs(data);
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void SetBoltForwardsLocation(ClosedBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding) return;
            
            var bulletOffset = data.upperBulletCol.height / 2f;

            var newFrontPos = data.upperBullet.transform.localPosition.z - bulletOffset;
            ___m_boltZ_forward = newFrontPos;
        }
        
        [HarmonyPatch(typeof(FVRFireArmMagazine), "UpdateBulletDisplay")]
        [HarmonyPostfix]
        private static void HideTopBulletIfDoubleFeeding(FVRFireArmMagazine __instance)
        {
            if (__instance.FireArm is null) return;
            
            var data = __instance.FireArm.GetComponent<DoubleFeedData>();
            if (data is null || !data.IsDoubleFeeding) return;
            
            __instance.DisplayRenderers[0].enabled = false;
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlideAndJiggling(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (__instance.CurPos != ClosedBolt.BoltPos.LockedToRear && __instance.CurPos != ClosedBolt.BoltPos.Rear) return;
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

        [HarmonyPatch(typeof(FVRFireArmRound), "BeginInteraction")]
        [HarmonyPrefix]
        private static bool UnDoubleFeedIfHeld(FVRFireArmRound __instance)
        {
            var bulletData = __instance.GetComponent<BulletDoubleFeedData>();

            if (bulletData is null || !bulletData.gunData.IsDoubleFeeding) return true;
            
            SetBulletToInteracting(__instance, bulletData.gunData, true, 
                bulletData.gunData.gameObject.GetComponent<Rigidbody>());
            return false;
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
            
            if (__instance.Weapon.Magazine != null)
            {
                data.lowerBullet.gameObject.layer = uninteractableLayer;
                data.upperBullet.gameObject.layer = uninteractableLayer;
            }
            else
            {
                data.lowerBullet.gameObject.layer = normalLayer;
                if (data.hasLowerBulletBeenRemoved) data.upperBullet.gameObject.layer = normalLayer;
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

        private static bool IsGunShaking(Rigidbody gunRb)
        {
            return (gunRb.velocity - GM.CurrentPlayerBody.Hitboxes[1].GetComponent<Rigidbody>().velocity).magnitude >
                   2f;
        }
        
        private static void SetBulletToNonInteracting(FVRFireArmRound round, DoubleFeedData data, bool setParentToWeapon = false, Transform weaponTransform = null)
        {
            round.RootRigidbody.velocity = Vector3.zero;
            round.RootRigidbody.angularVelocity = Vector3.zero;
            round.RootRigidbody.maxAngularVelocity = 0;
            round.RootRigidbody.useGravity = false;
            round.RootRigidbody.detectCollisions = false;
            data.upperBulletCol.isTrigger = false;
            
            round.StoreAndDestroyRigidbody();

            if (!setParentToWeapon) return;
            
            round.SetParentage(weaponTransform);
        }

        private static void SetBulletToInteracting(FVRFireArmRound round, DoubleFeedData data, bool breakParentage, Rigidbody weaponRb)
        {
            round.RecoverRigidbody();
            round.RootRigidbody.useGravity = true;
            round.RootRigidbody.maxAngularVelocity = 1000f;
            round.RootRigidbody.detectCollisions = true;
            data.upperBulletCol.isTrigger = true;

            if (round == data.upperBullet) data.hasUpperBulletBeenRemoved = true;
            if (round == data.lowerBullet) data.hasLowerBulletBeenRemoved = true;

            if (data.hasUpperBulletBeenRemoved && data.hasLowerBulletBeenRemoved) data.IsDoubleFeeding = false;
            
            if (breakParentage) round.SetParentage(null);
            if (weaponRb == null) return;
            
            round.RootRigidbody.velocity = weaponRb.velocity;
            round.RootRigidbody.angularVelocity = weaponRb.angularVelocity;
        }

        private static void GenerateUnJammingProbs(DoubleFeedData data)
        {
            // Obviously, if you cant use one of the methods to unjam the lower bullet, then you necessarily cant use it
            // for the upper bullet
            
            data.slideRackUnjamsLowerBullet = Random.Range(0f, 1f) < FailureScriptManager.lowerBulletDropoutProb.Value;

            if (data.slideRackUnjamsLowerBullet)
                data.slideRackUnjamsUpperBullet =
                    Random.Range(0f, 1f) < FailureScriptManager.upperBulletDropoutProb.Value;
            else
                data.slideRackUnjamsUpperBullet = false;
            
            data.slideRackAndJiggleUnjamsLowerBullet = Random.Range(0f, 1f) < FailureScriptManager.lowerBulletShakeyProb.Value;

            if (data.slideRackAndJiggleUnjamsLowerBullet)
                data.slideRackAndJiggleUnjamsUpperBullet =
                    Random.Range(0f, 1f) < FailureScriptManager.upperBulletShakeyProb.Value;
            else 
                data.slideRackAndJiggleUnjamsUpperBullet = false;
            
            
            // Special case probability where the lower bullet falls out, but the upper bullet needs cajoling
            data.slideRackUnjamsLowerButRackAndJiggleUnjamsUpper =
                Random.Range(0f, 1f) < FailureScriptManager.upperBulletShakeyProb.Value;
        }
    }
}