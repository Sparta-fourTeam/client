using System;
using System.Collections;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>페이드인 도중 정지·교체해도 지정한 페이드 시간을 지킨다.
    /// 프레임 단위로 진행되는 페이드라 PlayMode에서 보고, 시간은 버그(조기 무음)와 구분되는 넉넉한 범위로만 단언한다</summary>
    public sealed class AudioServiceTests
    {
        private const float FadeInSeconds = 4f;
        private const float FadeSeconds = 2f;
        private AudioService _service;
        private AudioLibrary _library;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("FadeTest", 44100, 1, 44100, false);
            _library = AudioLibrary.FromEntries(new[]
            {
                new AudioEntry { id = "first", clips = new[] { _clip }, volume = 0.8f },
                new AudioEntry { id = "second", clips = new[] { _clip }, volume = 0.6f },
            });
            _service = new AudioService(_library);
            _service.Start();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _service?.Dispose();
            UnityEngine.Object.Destroy(_library);
            UnityEngine.Object.Destroy(_clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StopBgm_DuringFadeIn_PreservesRequestedFadeDuration() => CheckInterruptedFade(false);

        [UnityTest]
        public IEnumerator PlayBgm_DuringFadeIn_PreservesOutgoingFadeDuration() => CheckInterruptedFade(true);

        [UnityTest]
        public IEnumerator StopBgm_DuringCrossfade_FadesOutBothTracksWithinRequestedDuration()
        {
            var music = GameObject.Find("AudioService").GetComponentsInChildren<AudioSource>().Where(s => s.loop).ToArray();
            _service.PlayBgm("first", 0f);
            yield return null;
            _service.PlayBgm("second", 10f);
            yield return Until(() => music.All(s => s.isPlaying && s.volume > 0.05f), 3f);

            var startedAt = Time.unscaledTime;
            _service.StopBgm(1f);
            yield return Until(() => music.All(s => !s.isPlaying), 2.5f);
            Assert.Less(Time.unscaledTime - startedAt, 2.5f);
        }

        private IEnumerator CheckInterruptedFade(bool replace)
        {
            var music = GameObject.Find("AudioService").GetComponentsInChildren<AudioSource>().Where(s => s.loop).ToArray();
            _service.PlayBgm("first", FadeInSeconds);
            var outgoing = music.Single(s => s.isPlaying);
            yield return Until(() => outgoing.volume >= 0.08f, 2f);
            var startingVolume = outgoing.volume;
            Assert.Less(startingVolume, 0.4f, "첫 곡이 아직 페이드인 중이어야 한다");

            var startedAt = Time.unscaledTime;
            if (replace) { _service.PlayBgm("second", FadeSeconds); }
            else { _service.StopBgm(FadeSeconds); }

            // 조기 무음 버그에서는 지정 시간의 1/8 안에 끝난다. 절반 지점에서 아직 소리가 남고 줄어드는 중이면 시간을 지킨 것이다
            yield return Until(() => Time.unscaledTime - startedAt >= FadeSeconds * 0.5f, FadeSeconds);
            Assert.Greater(outgoing.volume, startingVolume * 0.15f, "페이드인 도중인 곡도 지정 시간의 절반에는 아직 소리가 남아야 한다");
            Assert.Less(outgoing.volume, startingVolume * 0.9f, "페이드아웃이 진행되어야 한다");

            yield return Until(() => !outgoing.isPlaying, FadeSeconds + 1.5f);
            Assert.GreaterOrEqual(Time.unscaledTime - startedAt, FadeSeconds * 0.75f, "지정한 시간보다 너무 일찍 끝났다");
            if (replace)
            {
                var incoming = music.Single(s => s != outgoing);
                yield return Until(() => Mathf.Approximately(incoming.volume, 0.6f), 1.5f);
                Assert.IsTrue(incoming.isPlaying);
            }
        }

        private static IEnumerator Until(Func<bool> finished, float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!finished() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(finished(), "오디오 페이드가 제한 시간 안에 진행되지 않았다");
        }
    }
}
