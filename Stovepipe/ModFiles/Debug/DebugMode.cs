using System.Collections.Generic;
using System.IO;
using System.Linq;
using FistVR;
using HarmonyLib;
using Newtonsoft.Json;
using Stovepipe.ModFiles;
using UnityEngine;
using Valve.VR.InteractionSystem;
using Object = System.Object;

namespace Stovepipe.Debug
{
    public static class DebugMode
    {
        public static bool IsDebuggingWeapon;
        public static FVRFireArm CurrentDebugWeapon;

        private static GameObject _currentStovepipeDebugRound;
        private static FVRFireArmRound _currentStovepipeDebugRoundScript;
        private static readonly GameObject[] DoubleFeedDebugBullets = new GameObject[2];

        public static bool HasSetFrontBoltPos;
        public static float CurrentBoltForward;


        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        private static void CheckIfDebugInput(FVRViveHand __instance)
        {
            var hasUserPressed = __instance.Input.TouchpadDown &&
                              Vector2.Angle(__instance.Input.TouchpadAxes, Vector2.right) < 45f;

            if (!hasUserPressed) return;
            if (__instance.CurrentInteractable == null) return;

            var currentInteractable = __instance.CurrentInteractable;

            switch (currentInteractable)
            {
                case ClosedBoltWeapon cbw:
                    CurrentDebugWeapon = cbw;
                    break;
                case Handgun h:
                    CurrentDebugWeapon = h;
                    break;
                case TubeFedShotgun s:
                    CurrentDebugWeapon = s;
                    break;
                case OpenBoltReceiver o:
                    CurrentDebugWeapon = o;
                    break;
            }

            if (CurrentDebugWeapon == null) return;
            
            if (IsDebuggingWeapon)
            {
                IsDebuggingWeapon = false;
                if (!UserConfig.IsDoubleFeedDebugMode.Value) StovepipeResetDebugAndSaveVals();
                else DoubleFeedResetDebugAndSaveVals();
            }
            else
            {
                IsDebuggingWeapon = true;
                if (!UserConfig.IsDoubleFeedDebugMode.Value) StovepipeCreateDebugRound();
                else DoubleFeedCreateDebugRounds();
            }
        }

        private static void DoubleFeedResetDebugAndSaveVals()
        {
            var name = CurrentDebugWeapon.gameObject.name;
            name = name.Remove(name.Length - 7);

            var adjustment = new DoubleFeedAdjustment
            {
                UpperBulletLocalPos = DoubleFeedDebugBullets[0].transform.localPosition,
                UpperBulletDir = DoubleFeedDebugBullets[0].transform.localRotation,
                
                LowerBulletLocalPos = DoubleFeedDebugBullets[1].transform.localPosition,
                LowerBulletDir = DoubleFeedDebugBullets[1].transform.localRotation,
                
                BoltZ = CurrentBoltForward
            };

            DebugIO.WriteNewAdjustment(name, adjustment);
            UnityEngine.Object.Destroy(DoubleFeedDebugBullets[0]);
            UnityEngine.Object.Destroy(DoubleFeedDebugBullets[1]);
        }

        private static void DoubleFeedCreateDebugRounds()
        {

            DoubleFeedDebugBullets[0] = GenerateDebugRound();
            if (DoubleFeedDebugBullets[0] == null) return;

            DoubleFeedDebugBullets[1] = GenerateDebugRound();


            foreach (var round in DoubleFeedDebugBullets)
            {
                var roundScript = round.GetComponent<FVRFireArmRound>();
                var rb = roundScript.RootRigidbody;
                rb.detectCollisions = false;
                rb.useGravity = false;
                round.GetComponent<CapsuleCollider>().isTrigger = false;
                roundScript.StoreAndDestroyRigidbody();
                round.transform.parent = CurrentDebugWeapon.transform;
            }

            DoubleFeedDebugBullets[0].transform.position += CurrentDebugWeapon.transform.up * 0.02f;
        }
        
        private static void StovepipeCreateDebugRound()
        {
            _currentStovepipeDebugRound = GenerateDebugRound();
            if (_currentStovepipeDebugRound == null) return;


            _currentStovepipeDebugRoundScript = _currentStovepipeDebugRound.GetComponent<FVRFireArmRound>();
            _currentStovepipeDebugRoundScript.Fire();

            var rb = _currentStovepipeDebugRoundScript.RootRigidbody;
            rb.detectCollisions = false;
            rb.useGravity = false;
            _currentStovepipeDebugRound.GetComponent<CapsuleCollider>().isTrigger = false;
            _currentStovepipeDebugRoundScript.StoreAndDestroyRigidbody();
            _currentStovepipeDebugRound.transform.parent = CurrentDebugWeapon.transform;
        }

        private static void StovepipeResetDebugAndSaveVals()
        {
            var name = CurrentDebugWeapon.gameObject.name;
            name = name.Remove(name.Length - 7);
            
            var adjustment = new StovepipeAdjustment
            {
                    BulletDir = _currentStovepipeDebugRound.transform.localRotation,
                    BulletLocalPos = _currentStovepipeDebugRound.transform.localPosition,
                    BoltZ = CurrentBoltForward
            };

            DebugIO.WriteNewAdjustment(name, adjustment);
            UnityEngine.Object.Destroy(_currentStovepipeDebugRound);
        }
        
        private static GameObject GenerateDebugRound()
        {
            FVRObject bulletObj;
            var bulletGameObj = new GameObject();
            
            switch (CurrentDebugWeapon)
            {
                case Handgun handgun:
                    bulletObj =
                        AM.GetRoundSelfPrefab(handgun.RoundType, AM.GetDefaultRoundClass(handgun.RoundType));
                    bulletGameObj = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                        handgun.RoundPos_Ejection.position,
                        Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
                    break;

                case ClosedBoltWeapon closedBoltWeapon:
                    bulletObj =
                        AM.GetRoundSelfPrefab(closedBoltWeapon.RoundType, AM.GetDefaultRoundClass(closedBoltWeapon.RoundType));
                    bulletGameObj = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                        closedBoltWeapon.RoundPos_Ejection.position,
                        Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
                    break;

                case TubeFedShotgun tubeFedShotgun:
                    bulletObj =
                        AM.GetRoundSelfPrefab(tubeFedShotgun.RoundType, AM.GetDefaultRoundClass(tubeFedShotgun.RoundType));
                    bulletGameObj = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                        tubeFedShotgun.RoundPos_Ejection.position,
                        Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
                    break;

                case OpenBoltReceiver openBolt:
                    bulletObj =
                        AM.GetRoundSelfPrefab(openBolt.RoundType, AM.GetDefaultRoundClass(openBolt.RoundType));
                    bulletGameObj = UnityEngine.Object.Instantiate(bulletObj.GetGameObject(),
                        openBolt.RoundPos_Ejection.position,
                        Quaternion.Euler(CurrentDebugWeapon.transform.right)) as GameObject;
                    break;
            }

            return bulletGameObj;
        }

        [HarmonyPatch(typeof(FVRPhysicalObject), "EndInteraction")]
        [HarmonyPostfix]
        private static void InteractionEndDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsDebuggingWeapon) return;
            if (__instance != _currentStovepipeDebugRoundScript && 
                !DoubleFeedDebugBullets.Contains(__instance.gameObject)) return;
            
            __instance.StoreAndDestroyRigidbody();
            __instance.transform.parent = CurrentDebugWeapon.transform;
        }
        
        [HarmonyPatch(typeof(FVRPhysicalObject), "UpdateInteraction")]
        [HarmonyPostfix]
        private static void InteractionDestroyRigidBodyPatch(FVRPhysicalObject __instance)
        {
            if (!IsDebuggingWeapon) return;
            if (__instance != _currentStovepipeDebugRoundScript && 
                !DoubleFeedDebugBullets.Contains(__instance.gameObject)) return;
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