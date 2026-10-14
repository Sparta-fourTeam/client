using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>플레이어 빌드에 설정된 앱 버전을 표시한다.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class BuildVersionLabel : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<TMP_Text>().text = $"v{Application.version}";
        }
    }
}
