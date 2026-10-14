using UnityEngine;

namespace Game.View
{
    /// <summary>이 버튼(과 자식 버튼)은 눌러도 크기가 변하지 않는다. 자체 애니메이션이 있는 버튼 등에 붙인다</summary>
    [DisallowMultipleComponent]
    public sealed class NoPressFeedback : MonoBehaviour
    {
    }
}
