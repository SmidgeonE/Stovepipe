using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class SlideStovepipeData : MonoBehaviour
    {
        public FVRFireArmRound ejectedRound;
        public int roundDefaultLayer;
        public float ejectedRoundWidth;
        public float defaultFrontPosition;
        
        public float stovepipeProb = 0.5f;
        private bool _isStovepiping;

        public bool IsStovepiping
        {
            get => _isStovepiping;
            set
            {
                if (ejectedRound == null) return;
                var bulletData = ejectedRound.GetComponent<BulletStovepipeData>();
                if (bulletData != null) bulletData.isStovepiping = value;
                _isStovepiping = value;
            }
        }

        public bool hasBulletBeenSetNonColliding;

        public bool hasCollectedDefaultFrontPosition;
        public CapsuleCollider bulletCollider;
    }
}