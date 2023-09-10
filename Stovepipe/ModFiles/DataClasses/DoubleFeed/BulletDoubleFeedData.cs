using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class BulletDoubleFeedData : MonoBehaviour
    {
        private const float TimeUntilBulletCanBeRechamberedAfterUnDoubleFeeding = 1f;
        
        public DoubleFeedData gunData;
        public float timeSinceUnDoubleFed;
        public bool isThisBulletDoubleFeeding;
        private FVRFireArmRound _thisRoundScript;

        private void Start()
        {
            _thisRoundScript = gameObject.GetComponent<FVRFireArmRound>();
        }

        private void Update()
        {
            if (!isThisBulletDoubleFeeding) timeSinceUnDoubleFed += Time.deltaTime;
            else timeSinceUnDoubleFed = 0f;
            
            // Then, if enough time has passed, it may be chamberable again, this stops bullets from falling 
            // Directly back into the chamber (unrealistic)

            if (timeSinceUnDoubleFed > TimeUntilBulletCanBeRechamberedAfterUnDoubleFeeding)
                _thisRoundScript.isManuallyChamberable = true;
        }
    }
}