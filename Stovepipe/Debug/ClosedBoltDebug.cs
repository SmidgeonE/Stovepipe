using FistVR;
using HarmonyLib;

namespace Stovepipe.Debug
{
    public static class ClosedBoltDebug
    {
        
        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltPlacementIfDebugging(ClosedBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            if (!DebugMode.IsDebuggingWeapon) return;
            if (__instance.Weapon != DebugMode.CurrentDebugWeapon) return;

            if (__instance.IsHeld || (__instance.Weapon.Handle != null && __instance.Weapon.Handle.IsHeld))
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