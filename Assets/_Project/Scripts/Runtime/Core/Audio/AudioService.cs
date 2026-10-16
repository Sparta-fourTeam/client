using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>풀링한 AudioSource로 효과음을 내고, 배경음은 두 AudioSource를 번갈아 크로스페이드한다.
    /// 출력은 GameMixer의 Sfx·Music 그룹이라 SoundVolumeApplier가 적용하는 볼륨을 그대로 따른다.
    /// 믹서는 Awake 시점에 무시될 수 있어 SoundVolumeApplier처럼 Start에서 만든다.
    /// 배경음 페이드는 unscaled time이라 전투 배속과 무관하다</summary>
    public sealed class AudioService : IAudioService, IStartable, IDisposable
    {
        private const int SfxVoices = 12;
        private const int UiSfxVoices = 4;

        private sealed class BgmSlot
        {
            public AudioSource Source;
            public string Id;
            public float Peak;
            public float Target;
            public float Rate;
        }

        private struct SfxEnvelope
        {
            public float Remaining;
            public float Volume;
            public float FadeSeconds;
        }

        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly Dictionary<string, AudioSource> _uiLoops = new Dictionary<string, AudioSource>();
        private readonly System.Random _soundRandom = new System.Random();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly BgmSlot[] _bgm = { new BgmSlot(), new BgmSlot() };

        private AudioLibrary _library;
        private AudioMixer _mixer;
        private GameObject _root;
        private AudioSource[] _sfx;
        private readonly bool[] _pausedSfx = new bool[SfxVoices];
        private readonly SfxEnvelope[] _sfxEnvelopes = new SfxEnvelope[SfxVoices];
        private bool _enveloping;
        private bool _battleSfxPaused;
        private int _active;
        private bool _fading;
        private bool _missingLibraryWarned;
        private bool _disposed;
        private readonly HashSet<string> _unknownWarned = new HashSet<string>();

        public void Start()
        {
            _library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            _mixer = Resources.Load<AudioMixer>(SoundVolumeApplier.MixerPath);
        }

        // AudioSource는 소리를 실제로 낼 때 처음 만든다. 클립이 없는 동안(또는 소리를 내지 않는 화면)에는 활성 AudioSource가 없어
        // AudioListener가 없는 씬에서도 "no audio listeners" 경고가 나지 않는다
        private void EnsureSources()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("AudioService");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var sfxGroup = FindGroup(_mixer, nameof(SoundChannel.Sfx));
            // 전투 효과음이 모두 멈춰 있어도 선택창의 클릭음은 별도 보이스로 재생한다.
            _sfx = new AudioSource[SfxVoices + UiSfxVoices];
            for (var i = 0; i < _sfx.Length; i++)
            {
                _sfx[i] = CreateSource("Sfx" + i, sfxGroup, false);
            }

            var musicGroup = FindGroup(_mixer, nameof(SoundChannel.Music));
            for (var i = 0; i < _bgm.Length; i++)
            {
                _bgm[i].Source = CreateSource("Bgm" + i, musicGroup, true);
            }
        }

        public void PlaySfx(string id, float duration = 0f, float fadeOutSeconds = 0f)
        {
            if (!TryGetEntry(id, out var entry))
            {
                return;
            }

            if (entry.pauseWithBattle && _battleSfxPaused) { return; }

            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < entry.minInterval)
            {
                return;
            }

            EnsureSources();

            // 모든 소리가 재생 중이면 새 소리를 버린다. 이미 나는 소리를 끊는 것보다 덜 거슬린다
            var source = FindIdleSfx(entry.pauseWithBattle);
            if (source == null)
            {
                return;
            }

            _lastPlayed[id] = now;
            source.clip = entry.clips[_soundRandom.Next(entry.clips.Length)];
            source.volume = entry.volume;
            source.pitch = 1f + ((float)_soundRandom.NextDouble() * 2f - 1f) * entry.pitchVariance;
            source.loop = entry.pauseWithBattle && duration > 0f;
            int voiceIndex = Array.IndexOf(_sfx, source);
            if (voiceIndex < SfxVoices) { _sfxEnvelopes[voiceIndex] = default; }
            if (source.loop)
            {
                _sfxEnvelopes[voiceIndex] = new SfxEnvelope
                {
                    Remaining = duration,
                    Volume = entry.volume,
                    FadeSeconds = Mathf.Max(0f, fadeOutSeconds)
                };
                if (!_enveloping) { FadeBattleSfx(_lifetime.Token).Forget(); }
            }
            source.Play();
        }

        private async UniTaskVoid FadeBattleSfx(CancellationToken token)
        {
            _enveloping = true;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (_disposed || _sfx == null) { return; }
                    bool active = false;
                    for (int i = 0; i < SfxVoices; i++)
                    {
                        var envelope = _sfxEnvelopes[i];
                        if (envelope.Remaining <= 0f) { continue; }
                        var source = _sfx[i];
                        if (source == null) { _sfxEnvelopes[i] = default; continue; }
                        active = true;
                        if (_battleSfxPaused || Time.timeScale <= 0f) { continue; }
                        envelope.Remaining = Mathf.Max(0f, envelope.Remaining - Time.deltaTime);
                        // 공격은 게임 시간으로 진행하고, 마지막 페이드는 배속에 맞춰 실제 초로 환산한다.
                        float fadeWindow = envelope.FadeSeconds * Time.timeScale;
                        source.volume = envelope.Volume * (fadeWindow > 0f
                            ? Mathf.Clamp01(envelope.Remaining / fadeWindow) : 1f);
                        if (envelope.Remaining <= 0f)
                        {
                            source.Stop();
                            source.loop = false;
                            envelope = default;
                        }
                        _sfxEnvelopes[i] = envelope;
                    }
                    if (!active) { return; }
                }
            }
            finally { _enveloping = false; }
        }

        public void SetBattleSfxPaused(bool paused)
        {
            if (_disposed || _battleSfxPaused == paused) { return; }
            _battleSfxPaused = paused;
            if (_sfx == null) { return; }

            for (int i = 0; i < SfxVoices; i++)
            {
                if (_sfx[i] == null) { _pausedSfx[i] = false; continue; }
                if (paused)
                {
                    _pausedSfx[i] = _sfx[i].isPlaying;
                    if (_pausedSfx[i]) { _sfx[i].Pause(); }
                }
                else if (_pausedSfx[i])
                {
                    _sfx[i].UnPause();
                    _pausedSfx[i] = false;
                }
            }
        }

        public void PlayUiLoop(string id)
        {
            if (!TryGetEntry(id, out var entry)) { return; }
            EnsureSources();
            if (!_uiLoops.TryGetValue(id, out var source))
            {
                source = CreateSource("UiLoop_" + id, FindGroup(_mixer, nameof(SoundChannel.Sfx)), true);
                _uiLoops.Add(id, source);
            }
            if (source.isPlaying) { return; }
            source.clip = entry.clips[0];
            source.volume = entry.volume;
            source.pitch = 1f;
            source.Play();
        }

        public void StopUiLoop(string id)
        {
            if (id != null && _uiLoops.TryGetValue(id, out var source) && source != null) { source.Stop(); }
        }

        public void StopBattleSfx()
        {
            _battleSfxPaused = false;
            for (int i = 0; i < SfxVoices; i++)
            {
                if (_sfx != null && _sfx[i] != null)
                {
                    _sfx[i].Stop();
                    _sfx[i].loop = false;
                }
                _pausedSfx[i] = false;
                _sfxEnvelopes[i] = default;
            }
        }

        public void PlayBgm(string id, float fadeSeconds = 1f)
        {
            if (!TryGetEntry(id, out var entry))
            {
                return;
            }

            EnsureSources();

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
            if (_disposed || _root == null)
            {
                return;
            }

            // 크로스페이드 도중에는 두 슬롯이 모두 들리므로 소리가 나는 슬롯을 모두 줄인다
            foreach (var slot in _bgm)
            {
                if (slot.Source != null && (slot.Target > 0f || slot.Source.volume > 0f))
                {
                    FadeOut(slot, fadeSeconds);
                }
            }

            StartFading();
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
            _sfx = null;
            _uiLoops.Clear();
        }

        private static float RateOf(float peak, float fadeSeconds) =>
            // 즉시 전환은 무한대 대신 아주 큰 값을 쓴다. 무한대에 deltaTime 0을 곱하면 NaN이 되어 볼륨이 깨진다
            fadeSeconds <= 0f ? float.MaxValue : peak / fadeSeconds;

        private static void FadeOut(BgmSlot slot, float fadeSeconds)
        {
            slot.Id = null;
            slot.Target = 0f;
            if (slot.Source == null) { return; }
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
                        if (source == null) { continue; }
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
            if (_disposed) { return false; }
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

        private AudioSource FindIdleSfx(bool pauseWithBattle)
        {
            if (_sfx == null)
            {
                return null;
            }

            int first = pauseWithBattle ? 0 : SfxVoices;
            int end = pauseWithBattle ? SfxVoices : _sfx.Length;
            for (int i = first; i < end; i++)
            {
                var source = _sfx[i];
                if (source != null && !source.isPlaying && (i >= SfxVoices || !_pausedSfx[i]))
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
