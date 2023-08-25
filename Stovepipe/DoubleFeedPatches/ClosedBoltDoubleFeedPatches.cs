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
            if (__instance.Weapon.Chamber.GetRound() != null)
            {
                Debug.Log("chamber round is null");
                return;
            }

            var stoveData = __instance.Weapon.GetComponent<StovepipeData>();

            if (stoveData != null && stoveData.IsStovepiping) return;

            var data = __instance.Weapon.gameObject.AddComponent<DoubleFeedData>();
            data.SetProbability(true);
            
            data.hasFirstBulletBeenRemoved = false;
            data.hasSecondBulletBeenRemoved = false;

            /*if (!data.DoesProxyExist)
            {
                Debug.Log("proxy doesn't exist, returning");
                return;
            }*/


            Debug.Log(data.DoubleFeedChance);
            
            data.IsDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.DoubleFeedChance;

            if (!data.IsDoubleFeeding)
            {
                Debug.Log("not double feeding, returnign");
                return;
            }
            
            // Double Feed

            __instance.Weapon.ChamberRound();
            data.firstRound = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position,
                Vector3.zero, Vector3.zero, false);
            Debug.Log("Grabbing first round");
            
            __instance.Weapon.BeginChamberingRound();
            __instance.Weapon.ChamberRound();
            
            data.secondRound = __instance.Weapon.Chamber.EjectRound(__instance.Weapon.RoundPos_Ejection.position - Vector3.down * 0.1f,
                Vector3.zero, Vector3.zero, false);
            Debug.Log("Grabbing second round");

            data.mainBulletCol = data.firstRound.gameObject.GetComponent<CapsuleCollider>();
            Debug.Log("Grabbing coolider");


            data.firstRound.gameObject.AddComponent<BulletDoubleFeedData>().gunData = data;
            data.secondRound.gameObject.AddComponent<BulletDoubleFeedData>().gunData = data;

            SetBulletToNonInteracting(data.firstRound, data, true, __instance.Weapon.transform);
            SetBulletToNonInteracting(data.secondRound, data, true, __instance.Weapon.transform);
            Debug.Log("Set Bullets to non interacting");
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "UpdateComponents")]
        [HarmonyPostfix]
        private static void GetIfProxyExists(ClosedBolt __instance, ref FVRFirearmMovingProxyRound ___m_proxy)
        {
            var data = __instance.GetComponent<DoubleFeedData>();
            if (data is null) return;

            data.DoesProxyExist = ___m_proxy != null;
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void SetBoltForwardsLocation(ClosedBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();
            
            if (data is null || !data.IsDoubleFeeding) return;

            var newFrontPos = data.firstRound.transform.localPosition.z - data.mainBulletCol.height / 1.8f;

            ___m_boltZ_current = newFrontPos;
        }
        
        [HarmonyPatch(typeof(FVRFireArmMagazine), "UpdateBulletDisplay")]
        [HarmonyPostfix]
        private static void HideTopBulletIfDoubleFeeding(FVRFireArmMagazine __instance)
        {
            if (__instance.FireArm is null)
            {
                Debug.Log("firearm is null, retruning");
                return;
            }

            var data = __instance.FireArm.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding) return;

            Debug.Log("making top mag proxy invisibile");
            __instance.DisplayRenderers[0].enabled = false;
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlideAndShaking(ClosedBoltWeapon __instance)
        {
            var data = __instance.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding) return;

            if (!IsGunShaking(__instance.RootRigidbody))
            {
                return;
            }
            
            Debug.Log("gun is shaking ");

            if (Random.Range(0f, 1f) < FailureScriptManager.lowerBulletShakeyProb.Value)
            {
                SetBulletToInteracting(data.secondRound, data, true, __instance.RootRigidbody);
                Debug.Log("dejamming second round ");
            }
            else return;

            if (Random.Range(0f, 1f) < FailureScriptManager.upperBulletShakeyProb.Value)
            {
                SetBulletToInteracting(data.firstRound, data, true, __instance.RootRigidbody);
                Debug.Log("dejamming first round ");
            }
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_SmackRear")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlide(ClosedBolt __instance)
        {
            var data = __instance.Weapon.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding) return;

            if (Random.Range(0f, 1f) < FailureScriptManager.lowerBulletDropoutProb.Value)
            {
                SetBulletToInteracting(data.secondRound, data, true, __instance.Weapon.RootRigidbody);
                Debug.Log("dejamming second round ");
            }
            else return;

            if (Random.Range(0f, 1f) < FailureScriptManager.upperBulletDropoutProb.Value)
            {
                SetBulletToInteracting(data.firstRound, data, true, __instance.Weapon.RootRigidbody);
                Debug.Log("dejamming first round ");
            }
        }

        [HarmonyPatch(typeof(FVRFireArmRound), "BeginInteraction")]
        [HarmonyPrefix]
        private static bool UnDoubleFeedIfHeld(FVRFireArmRound __instance)
        {
            var bulletData = __instance.GetComponent<BulletDoubleFeedData>();

            if (bulletData is null || !bulletData.gunData.IsDoubleFeeding) return true;
            if (__instance == bulletData.gunData.firstRound && bulletData.gunData.hasFirstBulletBeenRemoved) return true;
            if (__instance == bulletData.gunData.secondRound && bulletData.gunData.hasSecondBulletBeenRemoved) return true;
            
            Debug.Log("interaction with double fed bullet");
            
            SetBulletToInteracting(__instance, bulletData.gunData, true, 
                bulletData.gunData.gameObject.GetComponent<Rigidbody>());
            return false;
        }

        private static bool IsGunShaking(Rigidbody gunRb)
        {
            return (gunRb.velocity - GM.CurrentPlayerBody.Hitboxes[1].GetComponent<Rigidbody>().velocity).magnitude >
                   0.5f;
        }
        
        private static void SetBulletToNonInteracting(FVRFireArmRound round, DoubleFeedData data, bool setParentToWeapon = false, Transform weaponTransform = null)
        {
            round.RootRigidbody.velocity = Vector3.zero;
            round.RootRigidbody.angularVelocity = Vector3.zero;
            round.RootRigidbody.maxAngularVelocity = 0;
            round.RootRigidbody.useGravity = false;
            round.RootRigidbody.detectCollisions = false;
            data.mainBulletCol.isTrigger = false;
            
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
            data.mainBulletCol.isTrigger = true;

            if (round == data.firstRound) data.hasFirstBulletBeenRemoved = true;
            if (round == data.secondRound) data.hasSecondBulletBeenRemoved = true;

            if (data.hasFirstBulletBeenRemoved && data.hasSecondBulletBeenRemoved) data.IsDoubleFeeding = false;
            
            if (breakParentage) round.SetParentage(null);
            if (weaponRb == null) return;
            
            round.RootRigidbody.velocity = weaponRb.velocity;
            round.RootRigidbody.angularVelocity = weaponRb.angularVelocity;
        }
    }
}