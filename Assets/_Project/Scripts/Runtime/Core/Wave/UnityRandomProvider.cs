using UnityEngine;

namespace Game.Core.Wave
{
    public class UnityRandomProvider : IRandomProvider
    {
        public float Range(float min, float max) => Random.Range(min, max);
    }
}
