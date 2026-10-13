namespace Game.Core
{
    /// <summary>결과 제출 실패 경로(자동 재시도, 재시도 버튼)를 개발 중에 확인하려는 스위치.
    /// Local 백엔드는 이 오류들을 스스로 던지지 않아서 평소 플레이로는 그 경로가 나타나지 않는다.
    /// Local 백엔드만 구현하고 서버 구현에는 없다. 에디터 디버거가 켠다</summary>
    public interface ISubmitFaultSwitch
    {
        /// <summary>0보다 크면 결과 제출을 이 횟수만큼 네트워크 오류로 실패시키고, 실패할 때마다 1씩 줄어든다</summary>
        int FailNextSubmits { get; set; }
    }
}
