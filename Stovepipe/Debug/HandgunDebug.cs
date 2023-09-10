using FistVR;
using HarmonyLib;

namespace Stovepipe.Debug
{
    public static class HandgunDebug
    {
        [HarmonyPatch(typeof(HandgunSlide), "UpdateSlide")]
        [HarmonyPrefix]
        private static void BoltPlacementIfDebugging(HandgunSlide __instance,
            ref float ___m_slideZ_forward, ref float ___m_slideZ_current)
        {
            if (!DebugMode.IsDebuggingWeapon) return;
            if (__instance.Handgun != DebugMode.CurrentDebugWeapon) return;

            if (__instance.IsHeld)
            {
                ___m_slideZ_forward = __instance.Point_Slide_Forward.localPosition.z;
                DebugMode.HasSetFrontBoltPos = false;
            }
            else
            {
                if (!DebugMode.HasSetFrontBoltPos) DebugMode.CurrentBoltForward = ___m_slideZ_current;
                
                ___m_slideZ_forward = DebugMode.CurrentBoltForward;

                DebugMode.HasSetFrontBoltPos = true;
            }
        }
    }
}