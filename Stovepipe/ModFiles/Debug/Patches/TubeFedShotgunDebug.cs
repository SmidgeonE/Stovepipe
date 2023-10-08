using FistVR;
using HarmonyLib;

namespace Stovepipe.Debug
{
    public class TubeFedShotgunDebug
    {
        [HarmonyPatch(typeof(TubeFedShotgunBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltPlacementIfDebugging(TubeFedShotgunBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            if (!DebugMode.IsDebuggingWeapon) return;
            if (__instance.Shotgun != DebugMode.CurrentDebugWeapon) return;

            if (__instance.IsHeld || (__instance.Shotgun.Handle != null && __instance.Shotgun.Handle.IsHeld))
            {
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
                DebugMode.HasSetFrontBoltPos = false;
            }
            else
            {
                if (!DebugMode.HasSetFrontBoltPos) DebugMode.CurrentBoltForward = ___m_boltZ_current;
                
                ___m_boltZ_forward = DebugMode.CurrentBoltForward;

                DebugMode.HasSetFrontBoltPos = true;
            }
        }
    }
}