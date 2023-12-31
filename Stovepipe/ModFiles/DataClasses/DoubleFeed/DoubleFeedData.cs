﻿using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class DoubleFeedData : MonoBehaviour
    {
        public bool isDoubleFeeding;
        public float bulletHeight;
        public float bulletRadius;
        
        public float doubleFeedChance;
        public float doubleFeedMaxChance;
        public bool hasSetDefaultChance;
        
        public FVRFireArmRound upperBullet;
        public FVRFireArmRound lowerBullet;

        public bool hasUpperBulletBeenRemoved;
        public bool hasLowerBulletBeenRemoved;

        public bool hasFinishedEjectingDoubleFeedRounds;

        // Probabilities that are randomised on every jam
        public bool slideRackUnjamsUpperBullet;
        public bool slideRackUnjamsLowerBullet;
        public bool slideRackAndJiggleUnjamsUpperBullet;
        public bool slideRackAndJiggleUnjamsLowerBullet;
        public bool slideRackUnjamsLowerButRackAndJiggleUnjamsUpper;

        public StovepipeData thisWeaponsStovepipeData;

        public DoubleFeedAdjustment Adjustments;
        public bool hasFoundAdjustments;

        public float[,] BulletRandomness;

        public void SetProbability(bool weaponIsRifle)
        {
            if (weaponIsRifle)
            {
                doubleFeedChance = UserConfig.DoubleFeedRifleProb.Value;
                doubleFeedMaxChance = doubleFeedChance;
                hasSetDefaultChance = true;
                return;
            }

            doubleFeedChance = UserConfig.DoubleFeedHandgunProb.Value;
            doubleFeedMaxChance = doubleFeedChance;
            hasSetDefaultChance = true;
        }

        public void SetDoubleFeedProbToMin()
        {
            doubleFeedChance = doubleFeedMaxChance / UserConfig.ProbabilityCreepNumRounds.Value;
        }
    }
}