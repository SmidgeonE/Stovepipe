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
            
            if (stoveData != null &&
                (stoveData.IsStovepiping || stoveData.numOfRoundsSinceLastJam < UserConfig.MinRoundBeforeNextJam.Value)) return;
            
            var data = __instance.Handgun.gameObject.GetComponent<DoubleFeedData>() ?? __instance.Handgun.gameObject.AddComponent<DoubleFeedData>();
            data.SetProbability(false);
            data.thisWeaponsStovepipeData = stoveData;
            
            if (data.isDoubleFeeding) return;
            
            data.hasUpperBulletBeenRemoved = false;
            data.hasLowerBulletBeenRemoved = false;
            
            var exampleRound = AM.GetRoundSelfPrefab(__instance.Handgun.Chamber.RoundType,
                AM.GetDefaultRoundClass(__instance.Handgun.Chamber.RoundType)).GetGameObject();
            if (exampleRound.GetComponent<FVRFireArmRound>().IsCaseless) return;
            
            data.isDoubleFeeding = UnityEngine.Random.Range(0f, 1f) < data.doubleFeedChance;
            if (!data.isDoubleFeeding) return;

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

            var upperBulletCol = data.upperBullet.gameObject.GetComponent<CapsuleCollider>();
            data.bulletHeight = upperBulletCol.height;
            data.bulletRadius = upperBulletCol.radius;
            
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

            if (data is null || !data.isDoubleFeeding)
            {
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
            }
            else
            {
                var newFrontPos = __instance.Point_Slide_Forward.localPosition.z - data.bulletHeight * 1.1f;
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
            if (data is null || !data.isDoubleFeeding) return;
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

            if (data is null || !data.isDoubleFeeding || __instance.Handgun.Magazine != null) return;

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
            if (!data.isDoubleFeeding) return;
            if (__instance.Handgun.MagazineType == FireArmMagazineType.mag_InternalGeneric) return;


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

            return !data.isDoubleFeeding || !data.hasFinishedEjectingDoubleFeedRounds;
        }
        
        // This patch is necessary due to luger toggle actions not working due to our manipulation of the private
        // field m_boltZ_current in our code.

        [HarmonyPatch(typeof(LugerToggleAction), "Update")]
        [HarmonyPrefix]
        private static bool DontChamberRoundIfDoubleFeeding(LugerToggleAction __instance)
        {
            var slide = __instance.Slide;
            var slideZRear = slide.Point_Slide_Rear.localPosition.z;
            var slideZForward = slide.Point_Slide_Forward.localPosition.z;
            var slideZCurrent = __instance.Slide.transform.localPosition.z;
            var t = 1f - Mathf.InverseLerp(slideZRear, slideZForward, slideZCurrent);
            
            __instance.BarrelSlide.localPosition = Vector3.Lerp(__instance.BarrelSlideForward.localPosition, __instance.BarrelSlideLockPoint.localPosition, t);
            var x = Mathf.Lerp(__instance.RotSet1.x, __instance.RotSet1.y, t);
            var x2 = Mathf.Lerp(__instance.RotSet2.x, __instance.RotSet2.y, t);
            var z = Mathf.Lerp(__instance.PosSet1.x, __instance.PosSet1.y, t);
            var localEulerAngles = new Vector3(x, 0f, 0f);
            __instance.TogglePiece1.localEulerAngles = localEulerAngles;
            var localEulerAngles2 = new Vector3(x2, 0f, 0f);
            __instance.TogglePiece2.localEulerAngles = localEulerAngles2;
            var localPosition = new Vector3(0f, __instance.Height, z);
            __instance.TogglePiece3.localPosition = localPosition;

            return false;
        }
        
    }
}