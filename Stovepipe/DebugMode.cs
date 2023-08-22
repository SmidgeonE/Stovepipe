using System;
using FistVR;
using HarmonyLib;
using UnityEngine;
using Object = System.Object;

namespace Stovepipe
{
    public class DebugMode
    {
        public static bool IsInDebug;
        private static ClosedBoltWeapon _currentDebugWeapon;
        private static GameObject _currentDebugRound;
        private static FVRFireArmRound _currentDebugRoundScript;

        private static bool _hasSetFrontBoltPos;
        private static float _currentBoltForward;


        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        private static void CheckIfDebugInput(FVRViveHand __instance)
        {
            var hasUserPressed = __instance.Input.TouchpadDown &&
                              Vector2.Angle(__instance.Input.TouchpadAxes, Vector2.right) < 45f;


            if (!hasUserPressed) return;

            Debug.Log("Pressed down debug mode");
            if (__instance.CurrentInteractable == null)
            {
                Debug.Log("no object in hand");
                return;
            }
            
            _currentDebugWeapon = __instance.CurrentInteractable.GetComponent<ClosedBoltWeapon>();

            if (_currentDebugWeapon == null)
            {
                Debug.Log("player is not holding a gun ");
                return;
            }

            if (IsInDebug)
            {
                Debug.Log("is in debug, destroyting");
                IsInDebug = false;
                // Destroy objects
                
                DestroyDebugRoundAndSaveValues();
            }
            else
            {
                Debug.Log("is not in debug, creating objects");
                IsInDebug = true;

                // Create Objects
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

            if (_currentDebugRound == null)
            {
                Debug.Log("bullet is null");
                return;
            }


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
            GameObject.Destroy(_currentDebugRound);
        }

        [HarmonyPatch(typeof(FVRPhysicalObject), "EndInteraction")]
        [HarmonyPostfix]
        private static void InteractionEndDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsInDebug) return;
            if (__instance != _currentDebugRoundScript) return;
            
            __instance.StoreAndDestroyRigidbody();
            __instance.transform.parent = _currentDebugWeapon.transform;
        }
        
        [HarmonyPatch(typeof(FVRPhysicalObject), "BeginInteraction")]
        [HarmonyPostfix]
        private static void InteractionDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsInDebug) return;
            if (__instance != _currentDebugRoundScript) return;
            
            __instance.StoreAndDestroyRigidbody();
        }
        
        [HarmonyPatch(typeof(ClosedBolt), "UpdateBolt")]
        [HarmonyPostfix]
        private static void BoltForwardBeginPatch(ClosedBolt __instance,
            ref float ___m_boltZ_forward, ref float ___m_boltZ_current)
        {
            if (!IsInDebug) return;
            if (__instance.Weapon != _currentDebugWeapon) return;

            if (__instance.IsHeld || (__instance.Weapon.Handle != null && __instance.Weapon.Handle.IsHeld))
            {
                Debug.Log("it is held");
                ___m_boltZ_forward = __instance.Point_Bolt_Forward.localPosition.z;
                _hasSetFrontBoltPos = false;
            }
            else
            {
                if (!_hasSetFrontBoltPos)
                {
                    _currentBoltForward = ___m_boltZ_current;
                }
                
                Debug.Log("not held, setting bolt forward to current");
                Debug.Log(_currentBoltForward);
  
                ___m_boltZ_forward = _currentBoltForward;

                _hasSetFrontBoltPos = true;
            }
        }
        
        [HarmonyPatch(typeof(FVRFireArmRound), "FVRUpdate")]
        [HarmonyPostfix]
        private static void BulletDecayPatch(ref float ___m_killAfter, FVRFireArmRound __instance)
        {
            if (!IsInDebug) return;
            
            ___m_killAfter = 5f;
        }

    }
}