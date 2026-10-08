using UnityEngine;

namespace Game.Core
{
    /// <summary>엘리트·보스 머리 위에 체력바를 그린다. 체력은 모델(HpRatio)을 직접 읽으므로 피격·회복·초기화가 같은 경로로 맞는다.
    /// 일반 등급은 아무것도 만들지 않고, 사망하면 바로 숨긴다. 막대 모양은 Resources/UI/EnemyHpBar 프리팹이 정한다
    /// (루트 아래에 "Fill" 자식이 있고, Fill의 로컬 스케일 x가 체력 비율이다. 피벗은 왼쪽 끝)</summary>
    public sealed class EnemyHpBar : MonoBehaviour
    {
        private const string PrefabPath = "UI/EnemyHpBar";
        private const string FillName = "Fill";
        private const float Margin = 0.15f;

        private EnemyModel _model;
        private GameObject _bar;
        private Transform _fill;
        private float _fillFullWidth;

        /// <summary>이 등급이 체력바를 보이는가</summary>
        public static bool ShowsFor(EnemyType type) => type == EnemyType.Elite || type == EnemyType.Boss;

        /// <summary>체력 비율을 막대 길이로 바꾼다. 0~1 밖의 값(회복 초과, 사망 직후)도 안전하게 자른다</summary>
        public static float FillWidth(float hpRatio, float fullWidth) => Mathf.Clamp01(hpRatio) * fullWidth;

        public bool IsVisible => _bar != null && _bar.activeSelf;

        // 같은 오브젝트에 다시 Bind해도(재사용) 이전 상태가 남지 않는다
        public void Bind(EnemyModel model)
        {
            _model = model;

            if (!ShowsFor(model.Type))
            {
                if (_bar != null)
                {
                    _bar.SetActive(false);
                }

                return;
            }

            if (_bar == null && !CreateBar())
            {
                return;
            }

            _bar.SetActive(true);
            Sync();
        }

        private bool CreateBar()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyHpBar] Resources/{PrefabPath} 프리팹을 찾지 못해 체력바를 표시하지 않는다", this);
                return false;
            }

            _bar = Instantiate(prefab, transform);
            _fill = _bar.transform.Find(FillName);
            if (_fill == null)
            {
                Debug.LogWarning($"[EnemyHpBar] {PrefabPath}에 '{FillName}' 자식이 없어 체력바를 표시하지 않는다", prefab);
                Destroy(_bar);
                _bar = null;
                return false;
            }

            _fillFullWidth = _fill.localScale.x;

            // 적 크기와 상관없이 같은 크기로 보이도록 부모 스케일을 상쇄하고, 머리 위에 둔다
            var scale = transform.lossyScale;
            var barScale = _bar.transform.localScale;
            _bar.transform.localScale = new Vector3(
                barScale.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                barScale.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                barScale.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            _bar.transform.position = new Vector3(transform.position.x, VisualBounds.TopY(transform, _bar.transform) + Margin, transform.position.z);
            return true;
        }

        private void LateUpdate() => Sync();

        /// <summary>막대를 모델의 현재 체력에 맞춘다. 매 LateUpdate에 돌고, 테스트가 직접 부르기도 한다</summary>
        public void Sync()
        {
            if (_bar == null || _model == null)
            {
                return;
            }

            if (_model.IsDead)
            {
                _bar.SetActive(false);
                return;
            }

            var scale = _fill.localScale;
            scale.x = FillWidth(_model.HpRatio, _fillFullWidth);
            _fill.localScale = scale;
        }
    }
}
