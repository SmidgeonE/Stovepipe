using System.Collections.Generic;
using FistVR;
using Stovepipe.StovepipePatches;
using UnityEngine;

namespace Stovepipe
{
    public class StovepipeData : MonoBehaviour
    {
        public bool IsStovepiping { get; set; }
        public FVRFireArmRound ejectedRound;
        public float ejectedRoundRadius;
        public float ejectedRoundHeight;
        public float defaultFrontPosition;
        public float[] randomPosAndRot;
        public float stovepipeProb;
        public bool hasBulletBeenStovepiped;
        public bool hasCollectedWeaponCharacteristics;
        public CapsuleCollider ejectedRoundCollider;
        public float timeSinceStovepiping;
        public bool ejectsToTheLeft;
        public bool hasFoundIfItEjectsUpwards;
        public bool ejectsUpwards;
        public StovepipeAdjustment Adjustments;
        public bool hasFoundAdjustments;

        public StovepipeData()
        {
            var slide = gameObject.GetComponent<HandgunSlide>();
            var bolt = gameObject.GetComponent<ClosedBolt>();
            
            if (slide != null)
            {
                ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(slide);
                stovepipeProb = FailureScriptManager.stovepipeHandgunProb.Value;
            }
            else if (bolt != null)
            {
                ejectsToTheLeft = StovepipeBase.FindIfGunEjectsToTheLeft(bolt);
                stovepipeProb = FailureScriptManager.stovepipeRifleProb.Value;
            }
        }
    }
}