using System;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>SoundSettings의 볼륨을 오디오 믹서(Resources/Audio/GameMixer)의 그룹 볼륨에 적용한다.
    /// 그룹은 Music·Sfx·Voice이고, 각 그룹 볼륨은 MusicVolume·SfxVolume·VoiceVolume 파라미터로 노출되어 있다.
    /// 소리를 낼 AudioSource는 출력(Output)을 해당 그룹으로 두면 이 볼륨을 따른다.
    /// 믹서는 Awake 시점의 SetFloat를 무시할 수 있어 Start(엔트리 포인트)에서 처음 적용한다</summary>
    public sealed class SoundVolumeApplier : IStartable, IDisposable
    {
        public const string MixerPath = "Audio/GameMixer";
        private const float MinDecibel = -80f;

        private readonly SoundSettings _settings;
        private AudioMixer _mixer;

        public SoundVolumeApplier(SoundSettings settings)
        {
            _settings = settings;
        }

        public static string ParameterOf(SoundChannel channel) => channel + "Volume";

        /// <summary>0~1 볼륨을 데시벨로 바꾼다. 0이면 -80dB(무음), 1이면 0dB</summary>
        public static float ToDecibel(float volume) =>
            volume <= 0.0001f ? MinDecibel : Mathf.Max(MinDecibel, 20f * Mathf.Log10(volume));

        public void Start()
        {
            _mixer = Resources.Load<AudioMixer>(MixerPath);
            if (_mixer == null)
            {
                Debug.LogWarning($"[SoundVolumeApplier] Resources/{MixerPath} 믹서를 찾지 못해 볼륨을 적용하지 않는다");
                return;
            }

            foreach (SoundChannel channel in Enum.GetValues(typeof(SoundChannel)))
            {
                Apply(channel, _settings.Get(channel));
            }

            _settings.Changed += Apply;
        }

        private void Apply(SoundChannel channel, float volume)
        {
            _mixer.SetFloat(ParameterOf(channel), ToDecibel(volume));
        }

        public void Dispose()
        {
            _settings.Changed -= Apply;
        }
    }
}
