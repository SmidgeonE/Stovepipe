using System.Collections.Generic;
using System.IO;
using FistVR;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Stovepipe.Debug
{
    public class DebugMode
    {
        public static bool IsDebuggingWeapon;
        public static FVRInteractiveObject CurrentDebugWeapon;

        private static GameObject _currentDebugRound;
        private static FVRFireArmRound _currentDebugRoundScript;

        public static bool HasSetFrontBoltPos;
        public static float CurrentBoltForward;
        
        private static readonly JsonSerializerSettings IgnoreSelfReference = new JsonSerializerSettings
            { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };


        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        private static void CheckIfDebugInput(FVRViveHand __instance)
        {
            var hasUserPressed = __instance.Input.TouchpadDown &&
                              Vector2.Angle(__instance.Input.TouchpadAxes, Vector2.right) < 45f;

            if (!hasUserPressed) return;
            if (__instance.CurrentInteractable == null) return;

            CurrentDebugWeapon = __instance.CurrentInteractable.GetComponent<ClosedBoltWeapon>();
            if (CurrentDebugWeapon == null) CurrentDebugWeapon = __instance.CurrentInteractable.GetComponent<Handgun>();

            if (CurrentDebugWeapon == null) return;

            if (IsDebuggingWeapon)
            {
                IsDebuggingWeapon = false;
                DestroyDebugRoundAndSaveValues();
            }
            else
            {
                IsDebuggingWeapon = true;
                CreateDebugRound();
            }
        }
        
        private static void CreateDebugRound()
        {
            FVRObject bulletObj;

            if (CurrentDebugWeapon is Handgun handgun)
            {
                bulletObj =
                    AM.GetRoundSelfPrefab(handgun.Chamber.RoundType, AM.GetDefaultRoundClass(handgun.Chamber.RoundType));
                _currentDebugRound = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                    handgun.RoundPos_Ejection.position,
                    Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
            }
            else if (CurrentDebugWeapon is ClosedBoltWeapon closedBoltWeapon)
            {
                bulletObj =
                    AM.GetRoundSelfPrefab(closedBoltWeapon.Chamber.RoundType, AM.GetDefaultRoundClass(closedBoltWeapon.Chamber.RoundType));
                _currentDebugRound = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                    closedBoltWeapon.RoundPos_Ejection.position,
                    Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
            }
            
            if (_currentDebugRound == null) return;


            _currentDebugRoundScript = _currentDebugRound.GetComponent<FVRFireArmRound>();
            _currentDebugRoundScript.Fire();

            var rb = _currentDebugRoundScript.RootRigidbody;
            rb.detectCollisions = false;
            rb.useGravity = false;
            _currentDebugRound.GetComponent<CapsuleCollider>().isTrigger = false;
            _currentDebugRoundScript.StoreAndDestroyRigidbody();
            _currentDebugRound.transform.parent = CurrentDebugWeapon.transform;
        }

        private static void DestroyDebugRoundAndSaveValues()
        {
            var name = CurrentDebugWeapon.gameObject.name;
            name = name.Remove(name.Length - 7);
            
            var adjustment = new StovepipeAdjustment
            {
                    BulletDir = _currentDebugRound.transform.localRotation,
                    BulletLocalPos = _currentDebugRound.transform.localPosition,
                    BoltZ = CurrentBoltForward
            };
            
            WriteNewAdjustment(name, adjustment);
            UnityEngine.Object.Destroy(_currentDebugRound);
        }
        
        public static void WriteNewAdjustment(string nameOfGun, StovepipeAdjustment adjustments)
        {
            if (FailureScriptManager.isWriteToDefault.Value)
                WriteOrReplaceInDict(nameOfGun, adjustments, FailureScriptManager.Defaults, FailureScriptManager.defaultsDir);
            else
                WriteOrReplaceInDict(nameOfGun, adjustments, FailureScriptManager.UserDefs, FailureScriptManager.userDefsDir);
        }

        private static void WriteOrReplaceInDict(string nameOfGun, StovepipeAdjustment adjustment,
            IDictionary<string, StovepipeAdjustment> dict, string dictDir)
        {
            if (dict.TryGetValue(nameOfGun, out _)) dict.Remove(nameOfGun);
            dict.Add(nameOfGun, adjustment);
            File.WriteAllText(dictDir, JsonConvert.SerializeObject(dict, Formatting.Indented, IgnoreSelfReference));
        }

        [HarmonyPatch(typeof(FVRPhysicalObject), "EndInteraction")]
        [HarmonyPostfix]
        private static void InteractionEndDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsDebuggingWeapon) return;
            if (__instance != _currentDebugRoundScript) return;
            
            __instance.StoreAndDestroyRigidbody();
            __instance.transform.parent = CurrentDebugWeapon.transform;
        }
        
        [HarmonyPatch(typeof(FVRPhysicalObject), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void InteractionDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsDebuggingWeapon) return;
            if (__instance != _currentDebugRoundScript) return;
            if (__instance.RootRigidbody == null) return;

            __instance.RootRigidbody.detectCollisions = false;
        }
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            if (!IsDebuggingWeapon) return;
            
            ___m_killAfter = 5f;
        }
    }
}