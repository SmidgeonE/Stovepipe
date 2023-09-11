using System;
using System.Collections.Generic;
using FistVR;
using Stovepipe.StovepipePatches;
using UnityEngine;

namespace Stovepipe
{
    public class StovepipeData : MonoBehaviour
    {
        public bool IsStovepiping { get; set; }
        public FVRFireArmRound ejectedRound;
        public float ejectedRoundRadius;
        public float ejectedRoundHeight;
        public float defaultFrontPosition;
        public float[] randomPosAndRot;
        public float stovepipeProb;
        public bool hasBulletBeenStovepiped;
        public bool hasCollectedWeaponCharacteristics;
        public CapsuleCollider ejectedRoundCollider;
        public float timeSinceStovepiping;
        public bool ejectsToTheLeft;
        public bool hasFoundIfItEjectsUpwards;
        public bool ejectsUpwards;
        public StovepipeAdjustment Adjustments;
        public bool hasFoundAdjustments;
        public WeaponType WeaponType;

        public StovepipeData()
        {
            var slide = gameObject.GetComponent<HandgunSlide>();
            var bolt = gameObject.GetComponent<ClosedBolt>();
            
            if (slide != null)
            {
                ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(slide);
                stovepipeProb = FailureScriptManager.stovepipeHandgunProb.Value;
            }
            else
            {
                if (bolt != null) ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(bolt);
                stovepipeProb = FailureScriptManager.stovepipeRifleProb.Value;
            }
        }

        public void SetWeaponType(WeaponType type)
        {
            WeaponType = type;
            
            switch (type)
            {
                case WeaponType.Handgun:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<HandgunSlide>());
                    stovepipeProb = FailureScriptManager.stovepipeHandgunProb.Value;
                    break;
                case WeaponType.Rifle:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<ClosedBolt>());
                    stovepipeProb = FailureScriptManager.stovepipeRifleProb.Value;
                    break;
                case WeaponType.TubeFedShotgun:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<TubeFedShotgunBolt>());
                    stovepipeProb = FailureScriptManager.stovepipeTubeFedProb.Value;
                    break;
                case WeaponType.OpenBolt:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<OpenBoltReceiverBolt>());
                    stovepipeProb = FailureScriptManager.stovepipeOpenBoltProb.Value;
                    break;
                default:
                    stovepipeProb = FailureScriptManager.stovepipeRifleProb.Value;
                    break;
            }
        }
    }
}