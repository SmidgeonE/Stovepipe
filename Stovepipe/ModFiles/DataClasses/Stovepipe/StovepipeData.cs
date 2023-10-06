
using System;
using System.Collections.Generic;
using System.Diagnostics;
using FistVR;
using Stovepipe.ModFiles;
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
        public CapsuleCollider ejectedRoundCollider;
        
        public float defaultFrontPosition;
        public float[] randomPosAndRot;
        public bool hasBulletBeenStovepiped;
        public bool hasCollectedWeaponCharacteristics;
        public float timeSinceStovepiping;
        public bool ejectsToTheLeft;
        public bool hasFoundIfItEjectsUpwards;
        public bool ejectsUpwards;
        public StovepipeAdjustment Adjustments;
        public bool hasFoundAdjustments;
        public WeaponType weaponType;
        public int numOfRoundsSinceLastJam;
        
        
        public float stovepipeProb;
        public float stovepipeMaxProb;

        public DoubleFeedData thisDoubleFeedData;
        
        public bool isWeaponBatteryFailing;
        public float pointOfBatteryFail;

        public StovepipeData()
        {
            numOfRoundsSinceLastJam = UserConfig.MinRoundBeforeNextJam.Value;
        }

        public void CheckAndIncreaseProbability()
        {
            if (!UserConfig.UseProbabilityCreep.Value) return;
            if (UserConfig.ProbabilityCreepNumRounds.Value == 0) return;

            if (stovepipeProb < stovepipeMaxProb)
                stovepipeProb += stovepipeMaxProb / UserConfig.ProbabilityCreepNumRounds.Value;
            
            CheckAndIncreaseDoubleFeedProbability();
        }

        public void CheckAndIncreaseDoubleFeedProbability()
        {
            switch (weaponType)
            {
                case WeaponType.Handgun:
                    var slide = gameObject.GetComponent<HandgunSlide>();
                    if (slide is null) return;
                    var handgunData = slide.Handgun.GetComponent<DoubleFeedData>();
                    if (handgunData is null) return;
                    
                    thisDoubleFeedData = handgunData;
                    if (handgunData.doubleFeedChance < handgunData.doubleFeedMaxChance)
                        handgunData.doubleFeedChance += handgunData.doubleFeedMaxChance /
                                                        UserConfig.ProbabilityCreepNumRounds.Value;
                    break;
                
                case WeaponType.Rifle:
                    var bolt = gameObject.GetComponent<ClosedBolt>();
                    if (bolt == null) return;
                    var rifleData = bolt.GetComponent<DoubleFeedData>();
                    if (rifleData is null) return;
                    thisDoubleFeedData = rifleData;
                    if (rifleData.doubleFeedChance < rifleData.doubleFeedMaxChance)
                        rifleData.doubleFeedChance += rifleData.doubleFeedMaxChance /
                                                      UserConfig.ProbabilityCreepNumRounds.Value;
                    break;
            }
        }

        public void SetStoveProbToMin()
        {
            stovepipeProb = stovepipeMaxProb / UserConfig.ProbabilityCreepNumRounds.Value;
        }

        public void SetWeaponType(WeaponType type)
        {
            weaponType = type;
            
            switch (type)
            {
                case WeaponType.Handgun:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<HandgunSlide>());
                    stovepipeMaxProb = UserConfig.StovepipeHandgunProb.Value;
                    stovepipeProb = stovepipeMaxProb;
                    break;
                
                case WeaponType.Rifle:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<ClosedBolt>());
                    stovepipeMaxProb = UserConfig.StovepipeRifleProb.Value;
                    stovepipeProb = stovepipeMaxProb;
                    break;
                
                case WeaponType.TubeFedShotgun:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<TubeFedShotgunBolt>());
                    stovepipeMaxProb = UserConfig.StovepipeTubeFedProb.Value;
                    stovepipeProb = stovepipeMaxProb;
                    break;
                
                case WeaponType.OpenBolt:
                    ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(gameObject.GetComponent<OpenBoltReceiverBolt>());
                    stovepipeMaxProb = UserConfig.StovepipeOpenBoltProb.Value;
                    stovepipeProb = stovepipeMaxProb;
                    break;
            }
        }
    }
}