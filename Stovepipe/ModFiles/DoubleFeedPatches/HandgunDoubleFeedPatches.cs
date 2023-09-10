using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe.DoubleFeedPatches
{
    public class HandgunDoubleFeedPatches : DoubleFeedBase
    {
        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_ExtractRoundFromMag")]
        [HarmonyPostfix]
        private static void DoubleFeedDiceroll(HandgunSlide __instance)
        {
            if (__instance.IsHeld) return;
            if (__instance.Handgun.Magazine == null) return;
            if (__instance.Handgun.Magazine.m_numRounds < 2) return;
            if (__instance.Handgun.Chamber.GetRound() != null) return;

            var stoveData = __instance.Handgun.Slide.GetComponent<StovepipeData>();
            if (stoveData != null && stoveData.IsStovepiping) return;
            
            var data = __instance.Handgun.gameObject.GetComponent<DoubleFeedData>() ?? __instance.Handgun.gameObject.AddComponent<DoubleFeedData>();
            data.SetProbability(false);
            
            if (data.IsDoubleFeeding) return;
            
            data.hasUpperBulletBeenRemoved = false;
            data.hasLowerBulletBeenRemoved = false;
            
            data.IsDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.DoubleFeedChance;
            if (!data.IsDoubleFeeding) return;

            // Double Feed

            data.BulletRandomness = GenerateRandomOffsets();
            GenerateUnJammingProbs(data, false);
            data.hasFinishedEjectingDoubleFeedRounds = false;

            var managedToChamber = __instance.Handgun.ChamberRound();
            if (!managedToChamber)
            {
                __instance.Handgun.ExtractRound();
                __instance.Handgun.ChamberRound();
            }

            data.upperBullet = __instance.Handgun.Chamber.EjectRound(__instance.Handgun.RoundPos_Ejection.position,
                Vector3.zero, Vector3.zero, false);

            __instance.Handgun.ExtractRound();
            __instance.Handgun.ChamberRound();
            data.lowerBullet = __instance.Handgun.Chamber.EjectRound(__instance.Handgun.RoundPos_Ejection.position + Vector3.down * 0.3f,
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

            SetBulletToNonInteracting(data.upperBullet, data, true, __instance.Handgun.transform);
            SetBulletToNonInteracting(data.lowerBullet, data, true, __instance.Handgun.transform);

            // Applying procedural positioning

            var upperBulletTransform = data.upperBullet.transform;
            var chamberProxyRoundPos = __instance.Handgun.Chamber.ProxyRound.position;

            upperBulletTransform.position = chamberProxyRoundPos 
                                            + __instance.Handgun.transform.up * data.bulletRadius
                                            - upperBulletTransform.forward * data.bulletHeight * 1.05f
                                            + data.BulletRandomness[0, 0] * upperBulletTransform.right;
            
            upperBulletTransform.Rotate(upperBulletTransform.up, data.BulletRandomness[0, 1], Space.Self);
            upperBulletTransform.Rotate(upperBulletTransform.right, data.BulletRandomness[0, 2], Space.Self);


            var lowerBulletTransform = data.lowerBullet.transform;

            lowerBulletTransform.position = chamberProxyRoundPos 
                                            - __instance.Handgun.transform.up * data.bulletRadius
                                            - lowerBulletTransform.forward * data.bulletHeight * 1.05f
                                            + data.BulletRandomness[1, 0] * lowerBulletTransform.right;
            
            lowerBulletTransform.Rotate(lowerBulletTransform.up, data.BulletRandomness[1, 1], Space.Self);
            lowerBulletTransform.Rotate(lowerBulletTransform.right, data.BulletRandomness[1, 2], Space.Self);
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SetBoltForwardsLocation(HandgunSlide __instance,
            ref float ___m_slideZ_forward)
        {
            var stovepipeData = __instance.GetComponent<StovepipeData>();
            if (stovepipeData != null && stovepipeData.IsStovepiping)
            {
                return;
            }
            
            var data = __instance.Handgun.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding)
            {
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
            }
            else
            {
                var newFrontPos = __instance.Point_Slide_Forward.localPosition.z - data.upperBulletCol.height * 1.2f;
                ___m_slideZ_forward = newFrontPos;
            }
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlideAndJiggling(HandgunSlide __instance)
        {
            var data = __instance.Handgun.GetComponent<DoubleFeedData>();
            if (__instance.CurPos != HandgunSlide.SlidePos.LockedToRear
                && __instance.CurPos != HandgunSlide.SlidePos.Rear
                && __instance.CurPos != HandgunSlide.SlidePos.Locked) return;
            if (data is null || !data.IsDoubleFeeding) return;
            if (__instance.Handgun.Magazine != null) return;
            if (!IsGunShaking(__instance.Handgun.RootRigidbody)) return;

            if (data.slideRackAndJiggleUnjamsLowerBullet && !data.hasLowerBulletBeenRemoved)
                SetBulletToInteracting(data.lowerBullet, data, true, __instance.Handgun.RootRigidbody);
            
            if (data.slideRackAndJiggleUnjamsUpperBullet && !data.hasUpperBulletBeenRemoved)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Handgun.RootRigidbody);
            
            // Special Case: lower bullet falls out just by opening bolt, but the top one needs cajoling.
            
            if (data.hasLowerBulletBeenRemoved && data.slideRackUnjamsLowerButRackAndJiggleUnjamsUpper)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Handgun.RootRigidbody);
        }
        
        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_SmackRear")]
        [HarmonyPostfix]
        private static void UnBlockBulletsSometimesIfRackingSlide(HandgunSlide __instance)
        {
            var data = __instance.Handgun.GetComponent<DoubleFeedData>();

            if (data is null || !data.IsDoubleFeeding || __instance.Handgun.Magazine != null) return;

            if (data.slideRackUnjamsLowerBullet && !data.hasLowerBulletBeenRemoved)
                SetBulletToInteracting(data.lowerBullet, data, true, __instance.Handgun.RootRigidbody);
            
            if (data.slideRackUnjamsUpperBullet && !data.hasUpperBulletBeenRemoved)
                SetBulletToInteracting(data.upperBullet, data, true, __instance.Handgun.RootRigidbody);
        }

        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void ChangeBulletInteractability(HandgunSlide __instance)
        {
            var data = __instance.Handgun.GetComponent<DoubleFeedData>();
            if (data is null) return;
            if (!data.IsDoubleFeeding) return;

            var uninteractableLayer = LayerMask.NameToLayer("Water");
            var normalLayer = LayerMask.NameToLayer("Interactable");
            var lowerBulletExists = !data.hasLowerBulletBeenRemoved;

            if (__instance.Handgun.Magazine != null)
            {
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = uninteractableLayer;
            }
            else
            {
                if (lowerBulletExists) data.lowerBullet.gameObject.layer = normalLayer;
            }
        }

        [HarmonyPatch(typeof(Handgun), "ExtractRound")]
        [HarmonyPrefix]
        private static bool DontChamberRoundIfDoubleFeeding(Handgun __instance)
        {
            var data = __instance.GetComponent<DoubleFeedData>();

            if (data is null) return true;

            return !data.IsDoubleFeeding || !data.hasFinishedEjectingDoubleFeedRounds;
        }
    }
}