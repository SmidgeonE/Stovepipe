using FistVR;
using HarmonyLib;

namespace Stovepipe.Debug
{
    public class OpenBoltDebug
    {
        private static bool _isHandleHeld;
        
        [HarmonyPatch(typeof(OpenBoltReceiverBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltPlacementIfDebugging(OpenBoltReceiverBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            if (!DebugMode.IsDebuggingWeapon) return;
            if (__instance.Receiver != DebugMode.CurrentDebugWeapon) return;

            if (__instance.IsHeld || _isHandleHeld)
            {
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
                DebugMode.HasSetFrontBoltPos = false;
            }
            else
            {
                if (!DebugMode.HasSetFrontBoltPos) DebugMode.CurrentBoltForward = __instance.transform.localPosition.z;

                ___m_boltZ_forward = DebugMode.CurrentBoltForward;
                DebugMode.HasSetFrontBoltPos = true;
            }
        }

        [HarmonyPatch(typeof(OpenBoltChargingHandle), "UpdateInteraction")]
        [HarmonyPrefix]
        private static void FindIfHandleIsHeld(OpenBoltChargingHandle __instance)
        {
            _isHandleHeld = __instance.IsHeld;
        }
    }
}