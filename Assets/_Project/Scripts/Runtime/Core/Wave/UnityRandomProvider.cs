using UnityEngine;

namespace Game.Core
{
    public class UnityRandomProvider : IRandomProvider
    {
        public float Range(float min, float max) => Random.Range(min, max);
    }
}
