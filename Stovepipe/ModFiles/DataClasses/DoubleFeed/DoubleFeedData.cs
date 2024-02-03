using FistVR;
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
        public bool usesIntegralMagazines;
        
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

        private void Start()
        {
            var weaponScript = gameObject.GetComponent<FVRFireArm>();
            if (!weaponScript) return;
            
            switch (weaponScript.MagazineType)
            {
                case FireArmMagazineType.mag_InternalGeneric:
                case FireArmMagazineType.m792x57mmMauserInternal:
                case FireArmMagazineType.mC96MauserInternal:
                case FireArmMagazineType.m38MosinInternal:
                case FireArmMagazineType.m1903SpringfieldInteral:
                case FireArmMagazineType.aJohnson1941Internal:
                case FireArmMagazineType.mM40Internal:
                case FireArmMagazineType.mModel70Internal:
                    usesIntegralMagazines = true;
                return;
                
                default:
                    return;
            }
        }
    }
}