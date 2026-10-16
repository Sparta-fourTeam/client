namespace Game.Core
{
    /// <summary>소리를 ID로 재생한다. ID는 AudioLibrary에 등록된 것이다. 없는 ID는 경고만 남기고 무시한다</summary>
    public interface IAudioService
    {
        /// <summary>효과음을 재생한다. 전투음의 duration이 양수면 해당 게임 시간 동안 반복하고, 마지막 fadeOutSeconds(실제 초) 동안 줄여 멈춘다.</summary>
        void PlaySfx(string id, float duration = 0f, float fadeOutSeconds = 0f);

        /// <summary>UI 연출용 효과음을 반복한다. 같은 ID가 재생 중이면 다시 시작하지 않는다</summary>
        void PlayUiLoop(string id);

        /// <summary>지정한 UI 반복 효과음을 멈춘다</summary>
        void StopUiLoop(string id);

        /// <summary>전투 효과음만 재생 위치를 보존해 멈추거나 이어서 재생한다. UI 효과음과 배경음에는 영향이 없다</summary>
        void SetBattleSfxPaused(bool paused);

        /// <summary>전투 효과음을 종료하고 보존한 재생 위치도 버린다</summary>
        void StopBattleSfx();

        /// <summary>배경음을 바꾼다. 이전 배경음은 fadeSeconds 동안 줄이고 새 배경음은 그동안 키운다. 이미 재생 중인 ID면 무시한다</summary>
        void PlayBgm(string id, float fadeSeconds = 1f);

        /// <summary>배경음을 fadeSeconds 동안 줄여 멈춘다</summary>
        void StopBgm(float fadeSeconds = 1f);
    }
}
