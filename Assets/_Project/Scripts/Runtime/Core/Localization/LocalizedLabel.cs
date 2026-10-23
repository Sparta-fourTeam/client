using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>코드가 쓴 라벨 문구를 로케일이 바뀔 때 제자리에서 다시 계산한다. TMP_Text와 같은 오브젝트에 붙고 SetL10n이 붙여 준다.
    /// 바인딩한 뒤 다른 코드가 라벨에 직접 문구를 써 넣었으면(label.text != 마지막으로 넣은 값) 바인딩을 풀고 덮어쓰지 않는다.
    /// 재사용되는 라벨이 옛 문구로 되돌아가는 일을 막는 검사다. 갱신은 SelectedLocaleChanged에서 동기로 해서 LocalizedFont의 폰트 교체와 순서가 엇갈리지 않게 한다</summary>
    // 에디터 테스트(편집 모드)에서도 OnEnable 구독이 동작하게 한다. 이 컴포넌트는 코드로만 붙는다
    [ExecuteAlways]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        private TMP_Text _label;
        private Func<string> _producer;
        // 마지막으로 이 컴포넌트가 라벨에 넣은 문구. 다르면 다른 코드가 라벨을 가져간 것이다
        private string _lastSet;
        private string _appliedLocaleCode;
        private bool _subscribed;

        /// <summary>생산자를 저장하고 즉시 한 번 계산해 라벨에 넣는다. 이미 바인딩돼 있으면 새 생산자로 바꾼다</summary>
        public void Bind(TMP_Text label, Func<string> producer)
        {
            _label = label;
            _producer = producer;
            if (_label == null || _producer == null) { _producer = null; return; }
            Evaluate();
        }

        private void OnEnable()
        {
            if (!LocalizationSettings.HasSettings) { return; }
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _subscribed = true;
            // 비활성인 동안 로케일이 바뀌었으면 켜질 때 따라잡는다
            if (_producer != null && CurrentLocaleCode() != _appliedLocaleCode) { Refresh(); }
        }

        private void OnDisable()
        {
            if (!_subscribed) { return; }
            _subscribed = false;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale)
        {
            if (_producer != null) { Refresh(); }
        }

        /// <summary>다른 코드가 라벨을 바꿔 놓지 않았을 때만 생산자를 다시 호출한다</summary>
        private void Refresh()
        {
            if (_label == null || _label.text != _lastSet)
            {
                _producer = null;
                return;
            }
            Evaluate();
        }

        private void Evaluate()
        {
            try
            {
                string text = _producer();
                _label.text = text;
                // TMP가 정규화한 값이 아니라 우리가 넣은 값과 비교하지 않도록 라벨에서 다시 읽는다
                _lastSet = _label.text;
                _appliedLocaleCode = CurrentLocaleCode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _producer = null;
            }
        }

        // 초기화가 끝나지 않았으면 기다리지 않고 null을 돌려준다(SelectedLocale 프로퍼티는 완료를 동기로 기다린다)
        private static string CurrentLocaleCode()
        {
            if (!LocalizationSettings.HasSettings) { return null; }
            var op = LocalizationSettings.SelectedLocaleAsync;
            return op.IsDone && op.Result != null ? op.Result.Identifier.Code : null;
        }
    }
}
