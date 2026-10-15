namespace Game.Core
{
    /// <summary>소리를 ID로 재생한다. ID는 AudioLibrary에 등록된 것이다. 없는 ID는 경고만 남기고 무시한다</summary>
    public interface IAudioService
    {
        /// <summary>효과음을 한 번 재생한다</summary>
        void PlaySfx(string id);

        /// <summary>배경음을 바꾼다. 이전 배경음은 fadeSeconds 동안 줄이고 새 배경음은 그동안 키운다. 이미 재생 중인 ID면 무시한다</summary>
        void PlayBgm(string id, float fadeSeconds = 1f);

        /// <summary>배경음을 fadeSeconds 동안 줄여 멈춘다</summary>
        void StopBgm(float fadeSeconds = 1f);
    }
}
