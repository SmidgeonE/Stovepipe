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
        
        public float stovepipeProb = 0.5f;
        private bool _isStovepiping;

        public bool IsStovepiping { get; set; }
        public bool hasBulletBeenSetNonColliding;

        public bool hasCollectedDefaultFrontPosition;
        public CapsuleCollider bulletCollider;
    }
}