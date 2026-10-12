using System;
using UnityEngine;

namespace Game.Core
{
    public enum SoundChannel
    {
        Music,
        Sfx,
        Voice,
    }

    /// <summary>볼륨 값을 기기에 남기는 저장소. 테스트에서는 메모리 구현으로 바꾼다</summary>
    public interface IVolumeStore
    {
        bool TryGet(string key, out float value);
        void Set(string key, float value);
    }

    /// <summary>PlayerPrefs에 볼륨을 저장한다. 계정이 아니라 기기 설정이다</summary>
    public sealed class PlayerPrefsVolumeStore : IVolumeStore
    {
        public bool TryGet(string key, out float value)
        {
            value = PlayerPrefs.GetFloat(key, -1f);
            return PlayerPrefs.HasKey(key);
        }

        public void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>음악·효과음·음성 볼륨(0~1). 바꾸면 저장하고 Changed로 알린다. 저장된 값이 없으면 기본값(1)이다</summary>
    public sealed class SoundSettings
    {
        public const float DefaultVolume = 1f;
        private readonly IVolumeStore _store;
        private readonly float[] _volumes = new float[3];

        public event Action<SoundChannel, float> Changed;

        public SoundSettings(IVolumeStore store)
        {
            _store = store;
            foreach (SoundChannel channel in Enum.GetValues(typeof(SoundChannel)))
            {
                _volumes[(int)channel] = store.TryGet(Key(channel), out var saved) ? Mathf.Clamp01(saved) : DefaultVolume;
            }
        }

        public static string Key(SoundChannel channel) => $"settings.volume.{channel.ToString().ToLowerInvariant()}";

        public float Get(SoundChannel channel) => _volumes[(int)channel];

        public void Set(SoundChannel channel, float volume)
        {
            volume = Mathf.Clamp01(volume);
            if (Mathf.Approximately(_volumes[(int)channel], volume))
            {
                return;
            }

            _volumes[(int)channel] = volume;
            _store.Set(Key(channel), volume);
            Changed?.Invoke(channel, volume);
        }
    }
}
