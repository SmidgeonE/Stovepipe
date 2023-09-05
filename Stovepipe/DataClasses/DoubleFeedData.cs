using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class DoubleFeedData : MonoBehaviour
    {
        public bool IsDoubleFeeding;
        public float DoubleFeedChance;
        public bool IsRifle;

        public CapsuleCollider upperBulletCol;
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
        


        public void SetProbability(bool weaponIsRifle)
        {
            IsRifle = weaponIsRifle;
            
            if (weaponIsRifle)
            {
                DoubleFeedChance = FailureScriptManager.doubleFeedRifleProb.Value;
                return;
            }

            DoubleFeedChance = FailureScriptManager.doubleFeedHandgunProb.Value;
        }
    }
}