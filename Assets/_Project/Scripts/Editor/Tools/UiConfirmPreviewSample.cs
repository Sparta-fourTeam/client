using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Uses the public confirmation contract on the isolated Workspace clone, without account actions.</summary>
    internal static class UiConfirmPreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            if (sample == "기본" || root.GetComponent<ConfirmPopupView>() is not { } view) { return; }
            var request = sample switch
            {
                "게스트 로그인" => new ConfirmRequest
                {
                    Title = "게스트 로그인",
                    Message = "게스트로 로그인할까요?",
                    Warning = "게스트 계정은 앱을 삭제하거나 기기를 바꾸면 데이터가 사라질 수 있어요.",
                    ConfirmLabel = "로그인",
                    CancelLabel = "취소"
                },
                "구글 로그인" => new ConfirmRequest
                {
                    Title = "구글 로그인",
                    Message = "구글 계정으로 로그인할까요?",
                    ConfirmLabel = "로그인",
                    CancelLabel = "취소"
                },
                "애플 로그인" => new ConfirmRequest
                {
                    Title = "애플 로그인",
                    Message = "애플 계정으로 로그인할까요?",
                    ConfirmLabel = "로그인",
                    CancelLabel = "취소"
                },
                "안내" => new ConfirmRequest { Title = "알림", Message = "계정이 연동되었습니다.", ConfirmLabel = "확인" },
                "경고" => new ConfirmRequest
                {
                    Title = "계정을 삭제하시겠습니까?",
                    Message = "계정을 삭제하기 전에 아래 내용을 확인해 주세요.",
                    Warning = "계정 삭제는 되돌릴 수 없으며, 삭제된 데이터는 복구할 수 없습니다.",
                    ConfirmLabel = "계정 삭제",
                    CancelLabel = "취소"
                },
                "입력 확인" or "입력 완료" => new ConfirmRequest
                {
                    Title = "계정 삭제",
                    Message = "계정 삭제는 되돌릴 수 없습니다. 계속하려면 닉네임을 입력해 주세요.",
                    ConfirmLabel = "계정 삭제",
                    CancelLabel = "취소",
                    InputPlaceholder = "닉네임을 입력하세요",
                    RequiredInput = "Review_Ninja"
                },
                _ => null
            };
            if (request == null) { return; }
            view.Initialize();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_transition").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _ = view.ShowAsync(request);
            if (sample == "입력 완료" && serialized.FindProperty("_input").objectReferenceValue is TMPro.TMP_InputField input)
            {
                input.text = request.RequiredInput;
            }
        }
    }
}
