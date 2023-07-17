using HarmonyLib;
using UnityEngine;
using BepInEx;
using FistVR;


namespace Stovepipe
{

    [BepInPlugin("dll.smidgeon.failuretoeject", "Failure To Eject", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public class EjectionFailure : BaseUnityPlugin
    {
        private static FVRFireArmRound EjectedRound;
        private static Rigidbody EjectedRoundRb;
        private static Transform EjectedRoundTransform;
        private static float EjectedRoundWidth;
        private const float stovepipeProb = 0.5f;
        private static bool isStovepiping;
        private static bool isClippingThroughbullet;
        private static bool hasBulletBeenSetNonColliding;

        private void Awake()
        {
            Debug.Log("Failure to eject");
            Harmony.CreateAndPatchAll(typeof(EjectionFailure), null);
        }

        [HarmonyPatch(typeof(Handgun), "EjectExtractedRound")]
        [HarmonyPrefix]
        private static bool GetBulletReference(Handgun __instance)
        {
            if (!__instance.Chamber.IsFull) return false;

            var handgunTransform = __instance.transform;
            
            EjectedRound = __instance.Chamber.EjectRound(__instance.RoundPos_Ejection.position, 
                handgunTransform.right * __instance.RoundEjectionSpeed.x 
                + handgunTransform.up * __instance.RoundEjectionSpeed.y 
                + handgunTransform.forward * __instance.RoundEjectionSpeed.z, 
                handgunTransform.right * __instance.RoundEjectionSpin.x 
                + handgunTransform.up * __instance.RoundEjectionSpin.y 
                + handgunTransform.forward * __instance.RoundEjectionSpin.z, 
                __instance.RoundPos_Ejection.position, __instance.RoundPos_Ejection.rotation, 
                false);
            
            EjectedRoundRb = EjectedRound.GetComponent<Rigidbody>();
            EjectedRoundTransform = EjectedRound.transform;

            var bulletRenderer = EjectedRound.GetComponent<MeshRenderer>();
            
            if (bulletRenderer is null) Debug.Log("bullet has no renderer");

            EjectedRoundWidth = bulletRenderer.bounds.size.y;
            
            Debug.Log("eject round has width:" + EjectedRoundWidth);

            return false;
        }

        [HarmonyPatch(typeof(HandgunSlide), "SlideEvent_EjectRound")]
        [HarmonyPostfix]
        private static void StovepipePatch(HandgunSlide __instance)
        {
            if (__instance.IsHeld) return;
            if (!__instance.Handgun.Chamber.IsFull) return;
            if (!EjectedRound.IsSpent) return;
            
            isStovepiping = Random.Range(0f, 1f) < stovepipeProb;
            hasBulletBeenSetNonColliding = false;

            if (isStovepiping) Debug.Log("Stovepiping...");
        }


        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void SlidePatch(HandgunSlide __instance, ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            if (!isStovepiping) return;
            
            
            Debug.Log("forward float:" + ___m_slideZ_forward);
            Debug.Log("Ejected round width float: " + EjectedRoundWidth);
            Debug.Log("current position float: " + (___m_slideZ_forward - EjectedRoundWidth));

            var forwardPositionLimit = ___m_slideZ_forward - EjectedRoundWidth;
            if (___m_slideZ_current > forwardPositionLimit) isClippingThroughbullet = true;
            
            if (__instance.IsHeld) return;

            /* Stovepipe the round...
             */
            
            if (!hasBulletBeenSetNonColliding) SetBulletToNonColliding(__instance);

        }

        private static void SetBulletToNonColliding(HandgunSlide slide)
        {
            EjectedRoundRb.useGravity = false;
            EjectedRound.gameObject.layer = LayerMask.NameToLayer("Interactable");
            EjectedRoundTransform.position = slide.Point_Slide_Forward.position;
            EjectedRoundTransform.parent = slide.Handgun.transform;
            hasBulletBeenSetNonColliding = true;
        }
        
        
        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPostfix]
        private static void SlidePostfix(ref float ___m_slideZ_forward,
            ref float ___m_slideZ_current)
        {
            if (isStovepiping && isClippingThroughbullet)
            {
                Debug.Log("Is clipping through the bullet, returning to end of bullet");
                ___m_slideZ_current = ___m_slideZ_forward - EjectedRoundWidth;
                isClippingThroughbullet = false;
            }
        }








    }
}