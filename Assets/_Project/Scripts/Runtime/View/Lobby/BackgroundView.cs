using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    public class BackgroundView : MonoBehaviour
    {
        [SerializeField] private Image[] images;
        [SerializeField] private float speed;

        private void Update()
        {
            foreach (var image in images)
            {
                image.rectTransform.rotation = Quaternion.Euler(0f, 0f, speed * Time.time);
            }
        }
    }
}
