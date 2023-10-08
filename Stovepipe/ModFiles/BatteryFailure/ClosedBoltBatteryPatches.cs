using FistVR;
using HarmonyLib;
using UnityEngine;

namespace Stovepipe.ModFiles.BatteryFailure
{
    public class ClosedBoltBatteryPatches
    {
        [HarmonyPatch(typeof(ClosedBolt), "BoltEvent_SmackRear")]
        [HarmonyPostfix]
        private static void BatteryFailureDiceRoll(ClosedBolt __instance)
        {
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;
            if (data.IsStovepiping) return;
            if (data.isWeaponBatteryFailing) return;

            data.isWeaponBatteryFailing = UnityEngine.Random.Range(0f, 1f) < UserConfig.BatteryFailureProb.Value;
            data.pointOfBatteryFail = UnityEngine.Random.Range(0.05f, 0.5f);
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void SlidePositionPatch(ClosedBolt __instance, ref float ___m_boltZ_forward)
        {
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;

            if (data.isWeaponBatteryFailing)
                ___m_boltZ_forward = Mathf.Lerp(__instance.Point_Bolt_Forward.localPosition.z,
                    __instance.Point_Bolt_Forward.localPosition.z,
                    data.pointOfBatteryFail);
            else
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
        }

        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void AllowSlideForwardWhenUserForcesForward(ClosedBolt __instance, 
            ref float ___m_boltZ_forward, float ___m_boltZ_heldTarget)
        {
            if (!__instance.IsHeld) return;
            
            var data = __instance.GetComponent<StovepipeData>();
            if (data == null) return;
            if (!data.isWeaponBatteryFailing) return;

            // i.e. if the users hand is infront of the weapon (they have put a lot of force in moving it forward)

            if (___m_boltZ_heldTarget >= __instance.Point_Bolt_Forward.localPosition.z)
            {
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
                __instance.Weapon.PlayAudioEvent(FirearmAudioEventType.BoltSlideForward);
                data.isWeaponBatteryFailing = false;
            }
        }

        [HarmonyPatch(typeof(ClosedBoltWeapon), "Fire")]
        [HarmonyPrefix]
        private static bool StopFromFiringIfBatteryFailure(ClosedBoltWeapon __instance)
        {
            var data = __instance.Bolt.GetComponent<StovepipeData>();
            if (data == null) return true;

            return !data.isWeaponBatteryFailing;
        }

        [HarmonyPatch(typeof(FVRInteractiveObject), "ForceBreakInteraction")]
        [HarmonyPrefix]
        private static bool StopHandFallingOffSlideWhileBatteryFailure(FVRInteractiveObject __instance)
        {
            switch (__instance)
            {
                case ClosedBolt bolt:
                
                    var data = bolt.GetComponent<StovepipeData>();
                    if (data == null) return true;

                    return !data.isWeaponBatteryFailing;

                case ClosedBoltHandle handle:
                    var handleData = handle.Weapon.Bolt.GetComponent<StovepipeData>();
                    if (handleData == null) return true;

                    return !handleData.isWeaponBatteryFailing;
                
                default:
                    return true;
            }
        }
    }
}