using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>풀링한 AudioSource로 효과음을 내고, 배경음은 두 AudioSource를 번갈아 크로스페이드한다.
    /// 출력은 GameMixer의 Sfx·Music 그룹이라 SoundVolumeApplier가 적용하는 볼륨을 그대로 따른다.
    /// 믹서는 Awake 시점에 무시될 수 있어 SoundVolumeApplier처럼 Start에서 만든다.
    /// 페이드는 unscaled time이라 전투 배속과 무관하다</summary>
    public sealed class AudioService : IAudioService, IStartable, IDisposable
    {
        private const int SfxVoices = 12;

        private sealed class BgmSlot
        {
            public AudioSource Source;
            public string Id;
            public float Peak;
            public float Target;
            public float Rate;
        }

        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly BgmSlot[] _bgm = { new BgmSlot(), new BgmSlot() };

        private AudioLibrary _library;
        private GameObject _root;
        private AudioSource[] _sfx;
        private int _active;
        private bool _fading;
        private bool _missingLibraryWarned;
        private readonly HashSet<string> _unknownWarned = new HashSet<string>();

        /// <summary>기본 생성자. 라이브러리는 Start에서 Resources에서 불러온다</summary>
        [Inject]
        public AudioService()
        {
        }

        /// <summary>라이브러리를 직접 준다. Resources 에셋 없이 쓰거나 검증할 때 쓴다</summary>
        public AudioService(AudioLibrary library)
        {
            _library = library;
        }

        public void Start()
        {
            if (_library == null)
            {
                _library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            }

            var mixer = Resources.Load<AudioMixer>(SoundVolumeApplier.MixerPath);

            _root = new GameObject("AudioService");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var sfxGroup = FindGroup(mixer, nameof(SoundChannel.Sfx));
            _sfx = new AudioSource[SfxVoices];
            for (var i = 0; i < _sfx.Length; i++)
            {
                _sfx[i] = CreateSource("Sfx" + i, sfxGroup, false);
            }

            var musicGroup = FindGroup(mixer, nameof(SoundChannel.Music));
            for (var i = 0; i < _bgm.Length; i++)
            {
                _bgm[i].Source = CreateSource("Bgm" + i, musicGroup, true);
            }
        }

        public void PlaySfx(string id)
        {
            if (!TryGetEntry(id, out var entry))
            {
                return;
            }

            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < entry.minInterval)
            {
                return;
            }

            // 모든 소리가 재생 중이면 새 소리를 버린다. 이미 나는 소리를 끊는 것보다 덜 거슬린다
            var source = FindIdleSfx();
            if (source == null)
            {
                return;
            }

            _lastPlayed[id] = now;
            source.clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
            source.volume = entry.volume;
            source.pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariance, entry.pitchVariance);
            source.Play();
        }

        public void PlayBgm(string id, float fadeSeconds = 1f)
        {
            if (_root == null || !TryGetEntry(id, out var entry))
            {
                return;
            }

            var current = _bgm[_active];
            if (current.Id == id && current.Target > 0f)
            {
                return;
            }

            FadeOut(current, fadeSeconds);

            // 이전 크로스페이드에서 줄어드는 중이던 슬롯은 여기서 끊고 새 배경음에 쓴다
            _active = 1 - _active;
            var incoming = _bgm[_active];
            incoming.Source.clip = entry.clips[0];
            incoming.Source.volume = 0f;
            incoming.Source.Play();
            incoming.Id = id;
            incoming.Peak = entry.volume;
            incoming.Target = entry.volume;
            incoming.Rate = RateOf(entry.volume, fadeSeconds);
            if (fadeSeconds <= 0f)
            {
                incoming.Source.volume = entry.volume;
            }

            StartFading();
        }

        public void StopBgm(float fadeSeconds = 1f)
        {
            if (_root == null)
            {
                return;
            }

            // 크로스페이드 도중에는 두 슬롯이 모두 들리므로 재생 중인 슬롯을 모두 줄인다
            foreach (var slot in _bgm)
            {
                if (slot.Source.isPlaying)
                {
                    FadeOut(slot, fadeSeconds);
                }
            }

            StartFading();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private static float RateOf(float peak, float fadeSeconds) =>
            // 즉시 전환은 무한대 대신 아주 큰 값을 쓴다. 무한대에 deltaTime 0을 곱하면 NaN이 되어 볼륨이 깨진다
            fadeSeconds <= 0f ? float.MaxValue : peak / fadeSeconds;

        private static void FadeOut(BgmSlot slot, float fadeSeconds)
        {
            slot.Id = null;
            slot.Target = 0f;
            // 페이드 인이 끝나기 전에 바뀌어도 fadeSeconds 안에 줄어들도록 현재 볼륨에서 비율을 잡는다
            slot.Rate = RateOf(Mathf.Max(slot.Source.volume, 0.0001f), fadeSeconds);
            if (fadeSeconds <= 0f)
            {
                slot.Source.volume = 0f;
            }
        }

        // 두 슬롯의 목표 볼륨까지 한 루프가 움직인다. 페이드 도중 새 요청이 와도 목표만 바뀌어 겹쳐 돌지 않는다
        private void StartFading()
        {
            if (!_fading)
            {
                FadeLoop(_lifetime.Token).Forget();
            }
        }

        private async UniTaskVoid FadeLoop(CancellationToken token)
        {
            _fading = true;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var moving = false;
                    foreach (var slot in _bgm)
                    {
                        var source = slot.Source;
                        source.volume = Mathf.MoveTowards(source.volume, slot.Target, slot.Rate * Time.unscaledDeltaTime);
                        if (!Mathf.Approximately(source.volume, slot.Target))
                        {
                            moving = true;
                        }
                        else if (slot.Target <= 0f && source.isPlaying)
                        {
                            source.Stop();
                        }
                    }

                    if (!moving)
                    {
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                _fading = false;
            }
        }

        private bool TryGetEntry(string id, out AudioEntry entry)
        {
            entry = null;
            if (_library == null)
            {
                if (!_missingLibraryWarned)
                {
                    _missingLibraryWarned = true;
                    Debug.LogWarning($"[AudioService] Resources/{AudioLibrary.ResourcePath} 라이브러리를 찾지 못해 소리를 재생하지 않는다");
                }

                return false;
            }

            if (_library.TryGet(id, out entry))
            {
                // 클립을 아직 넣지 않은 자리 ID는 조용히 건너뛴다
                return entry.clips != null && entry.clips.Length > 0;
            }

            if (_unknownWarned.Add(id ?? string.Empty))
            {
                Debug.LogWarning($"[AudioService] AudioLibrary에 등록되지 않은 소리 ID '{id}'");
            }

            return false;
        }

        private AudioSource FindIdleSfx()
        {
            if (_sfx == null)
            {
                return null;
            }

            foreach (var source in _sfx)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            return null;
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.outputAudioMixerGroup = group;
            return source;
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            if (mixer == null)
            {
                return null;
            }

            var groups = mixer.FindMatchingGroups(name);
            return groups.Length > 0 ? groups[0] : null;
        }
    }
}
