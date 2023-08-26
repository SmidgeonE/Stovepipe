using System;
using System.Collections.Generic;
using System.IO;
using FistVR;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Object = System.Object;

namespace Stovepipe
{
    public class DebugMode
    {
        public static bool isDebuggingWeapon;
        private static ClosedBoltWeapon _currentDebugWeapon;
        private static GameObject _currentDebugRound;
        private static FVRFireArmRound _currentDebugRoundScript;

        private static bool _hasSetFrontBoltPos;
        private static float _currentBoltForward;
        
        private static JsonSerializerSettings ignoreSelfReference = new JsonSerializerSettings
            { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };


        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        private static void CheckIfDebugInput(FVRViveHand __instance)
        {
            var hasUserPressed = __instance.Input.TouchpadDown &&
                              Vector2.Angle(__instance.Input.TouchpadAxes, Vector2.right) < 45f;

            if (!hasUserPressed) return;
            if (__instance.CurrentInteractable == null) return;

            _currentDebugWeapon = __instance.CurrentInteractable.GetComponent<ClosedBoltWeapon>();

            if (_currentDebugWeapon == null) return;

            if (isDebuggingWeapon)
            {
                isDebuggingWeapon = false;
                DestroyDebugRoundAndSaveValues();
            }
            else
            {
                isDebuggingWeapon = true;
                CreateDebugRound();
            }
        }
        
        private static void CreateDebugRound()
        {
            var bulletObj =
                AM.GetRoundSelfPrefab(_currentDebugWeapon.Chamber.RoundType, AM.GetDefaultRoundClass(_currentDebugWeapon.Chamber.RoundType));
            _currentDebugRound = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                _currentDebugWeapon.RoundPos_Ejection.position,
                Quaternion.Euler(_currentDebugWeapon.transform.right)) as GameObject;

            if (_currentDebugRound == null) return;


            _currentDebugRoundScript = _currentDebugRound.GetComponent<FVRFireArmRound>();
            _currentDebugRoundScript.Fire();

            var rb = _currentDebugRoundScript.RootRigidbody;
            rb.detectCollisions = false;
            rb.useGravity = false;
            _currentDebugRound.GetComponent<CapsuleCollider>().isTrigger = false;
            _currentDebugRoundScript.StoreAndDestroyRigidbody();
            _currentDebugRound.transform.parent = _currentDebugWeapon.transform;
        }

        private static void DestroyDebugRoundAndSaveValues()
        {
            var name = _currentDebugWeapon.gameObject.name;
            name = name.Remove(name.Length - 7);
            
            var adjustment = new StovepipeAdjustment
            {
                    BulletDir = _currentDebugRound.transform.localRotation,
                    BulletLocalPos = _currentDebugRound.transform.localPosition,
                    BoltZ = _currentBoltForward
            };
            
            WriteNewAdjustment(name, adjustment);
            UnityEngine.Object.Destroy(_currentDebugRound);
        }
        
        public static void WriteNewAdjustment(string nameOfGun, StovepipeAdjustment adjustments)
        {
            if (StovepipeScriptManager.isWriteToDefault.Value)
                WriteOrReplaceInDict(nameOfGun, adjustments, StovepipeScriptManager.Defaults, StovepipeScriptManager.defaultsDir);
            else
                WriteOrReplaceInDict(nameOfGun, adjustments, StovepipeScriptManager.UserDefs, StovepipeScriptManager.userDefsDir);
        }

        private static void WriteOrReplaceInDict(string nameOfGun, StovepipeAdjustment adjustment,
            IDictionary<string, StovepipeAdjustment> dict, string dictDir)
        {
            if (dict.TryGetValue(nameOfGun, out _)) dict.Remove(nameOfGun);
            dict.Add(nameOfGun, adjustment);
            File.WriteAllText(dictDir, JsonConvert.SerializeObject(dict, Formatting.Indented, ignoreSelfReference));
        }

        [HarmonyPatch(typeof(FVRPhysicalObject), "EndInteraction")]
        [HarmonyPostfix]
        private static void InteractionEndDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!isDebuggingWeapon) return;
            if (__instance != _currentDebugRoundScript) return;
            
            __instance.StoreAndDestroyRigidbody();
            __instance.transform.parent = _currentDebugWeapon.transform;
        }
        
        [HarmonyPatch(typeof(FVRPhysicalObject), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void InteractionDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!isDebuggingWeapon) return;
            if (__instance != _currentDebugRoundScript) return;
            if (__instance.RootRigidbody == null) return;

            __instance.RootRigidbody.detectCollisions = false;
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPrefix]
        private static void BoltForwardBeginPatch(ClosedBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current, ref float ___m_curBoltSpeed)
        {
            if (!isDebuggingWeapon)
            {
                return;
            }
            if (__instance.Weapon != _currentDebugWeapon) return;

            if (__instance.IsHeld || (__instance.Weapon.Handle != null && __instance.Weapon.Handle.IsHeld))
            {
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
                _hasSetFrontBoltPos = false;
            }
            else
            {
                if (!_hasSetFrontBoltPos) _currentBoltForward = ___m_boltZ_current;
                

                ___m_boltZ_forward = _currentBoltForward;

                _hasSetFrontBoltPos = true;
            }
        }
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            if (!isDebuggingWeapon) return;
            
            ___m_killAfter = 5f;
        }
    }
}