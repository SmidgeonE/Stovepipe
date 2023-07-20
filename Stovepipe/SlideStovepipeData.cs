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

        public bool IsStovepiping
        {
            get => _isStovepiping;
            set
            {
                if (ejectedRound == null)
                {
                    Debug.Log("Ejected round is null when trying to change value to " + value);
                    return;
                }
                var bulletData = ejectedRound.gameObject.GetComponent<BulletStovepipeData>();
                if (bulletData == null) Debug.Log("Tried to set stovepiping, but bullet is dead.");
                if (bulletData != null)
                {
                    Debug.Log("setting bullet data to stovepiping, value to " + value);
                    bulletData.isStovepiping = value;
                }
                _isStovepiping = value;
            }
        }

        public bool hasBulletBeenSetNonColliding;

        public bool hasCollectedDefaultFrontPosition;
        public CapsuleCollider bulletCollider;
    }
}