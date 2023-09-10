using UnityEngine;

namespace Stovepipe
{
    public class BulletStovepipeData : MonoBehaviour
    {
        public StovepipeData data;

        public float timeSinceStovepiped;

        private void Update()
        {
            timeSinceStovepiped += Time.deltaTime;
        }
    }
}