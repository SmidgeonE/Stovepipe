using FistVR;
using UnityEngine;

namespace Stovepipe
{
    public class StovepipeData : MonoBehaviour
    {
        public FVRFireArmRound ejectedRound;
        public int roundDefaultLayer;
        public float ejectedRoundWidth;
        public float ejectedRoundHeight;
        public float defaultFrontPosition;
        public float[] randomPosAndRot;
        public float stovepipeProb;
        public bool hasBulletBeenStovepiped;
        public bool hasCollectedWeaponCharacteristics;
        public CapsuleCollider bulletCollider;
        public float timeSinceStovepiping;
        public bool IsStovepiping { get; set; }
        public bool ejectsToTheLeft;


        public StovepipeData()
        {
            var slide = gameObject.GetComponent<HandgunSlide>();
            var bolt = gameObject.GetComponent<ClosedBolt>();
            if (slide != null) ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(slide);
            else if (bolt != null) ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(bolt);
            else Debug.Log("Could not determine the direction of ejection");

            stovepipeProb = StovepipeScriptManager.stovepipeProb.Value;
        }
    }
}