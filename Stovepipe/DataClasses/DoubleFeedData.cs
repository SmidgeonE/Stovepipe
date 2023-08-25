using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class DoubleFeedData : MonoBehaviour
    {
        public bool IsDoubleFeeding;
        public float DoubleFeedChance;
        public bool DoesProxyExist;

        public CapsuleCollider mainBulletCol;

        public FVRFireArmRound firstRound;
        public FVRFireArmRound secondRound;

        public bool hasFirstBulletBeenRemoved;
        public bool hasSecondBulletBeenRemoved;

        public void SetProbability(bool weaponIsRifle)
        {
            if (weaponIsRifle)
            {
                DoubleFeedChance = FailureScriptManager.doubleFeedRifleProb.Value;
                return;
            }

            DoubleFeedChance = FailureScriptManager.doubleFeedHandgunProb.Value;
        }
    }
}