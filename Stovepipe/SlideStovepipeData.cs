using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class SlideStovepipeData : MonoBehaviour
    {
        public FVRFireArmRound ejectedRound;
        public int roundDefaultLayer;
        public float ejectedRoundWidth;
        public float ejectedRoundHeight;
        public float defaultFrontPosition;
        public float[] randomPosAndRot;
        public float stovepipeProb;
        public bool hasBulletBeenStovepiped;
        public bool hasCollectedDefaultFrontPosition;
        public CapsuleCollider bulletCollider;
        public float timeSinceStovepiping;
        
        public bool IsStovepiping { get; set; }
        public bool ejectsToTheLeft;

        public SlideStovepipeData()
        {
            ejectsToTheLeft = Stovepipe.FindIfGunEjectsToTheLeft(this.gameObject.GetComponent<HandgunSlide>());
        }
    }
}