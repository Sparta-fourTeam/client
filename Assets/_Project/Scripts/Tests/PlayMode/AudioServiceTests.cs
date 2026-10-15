using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>페이드인 도중 정지·교체해도 지정한 페이드 시간을 지킨다</summary>
    public sealed class AudioServiceTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private AudioService _service;
        private AudioLibrary _library;
        private AudioClip _clip;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("FadeTest", 44100, 1, 44100, false);
            _library = ScriptableObject.CreateInstance<AudioLibrary>();
            typeof(AudioLibrary).GetField("entries", Fields).SetValue(_library, new List<AudioEntry>
            {
                new AudioEntry { id = "first", clips = new[] { _clip }, volume = 0.8f },
                new AudioEntry { id = "second", clips = new[] { _clip }, volume = 0.6f },
            });
            _service = new AudioService();
            _service.Start();
            typeof(AudioService).GetField("_library", Fields).SetValue(_service, _library);
            _root = (GameObject)typeof(AudioService).GetField("_root", Fields).GetValue(_service);
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

        private IEnumerator CheckInterruptedFade(bool replace)
        {
            _service.PlayBgm("first", 4f);
            var outgoing = _root.transform.Find("Bgm1").GetComponent<AudioSource>();
            yield return Until(() => outgoing.volume >= 0.08f, 2f);
            var startingVolume = outgoing.volume;
            Assert.Less(startingVolume, 0.4f, "첫 곡이 아직 페이드인 중이어야 한다");

            const float fadeSeconds = 1f;
            var startedAt = Time.unscaledTime;
            if (replace) { _service.PlayBgm("second", fadeSeconds); }
            else { _service.StopBgm(fadeSeconds); }

            yield return Until(() => Time.unscaledTime - startedAt >= fadeSeconds * 0.5f, 2f);
            Assert.Greater(outgoing.volume, startingVolume * 0.2f,
                "페이드인 도중인 곡도 지정 시간의 절반에는 아직 소리가 남아야 한다");
            Assert.Less(outgoing.volume, startingVolume * 0.7f, "페이드아웃이 진행되어야 한다");

            yield return Until(() => outgoing.volume == 0f, 2f);
            Assert.That(Time.unscaledTime - startedAt, Is.InRange(fadeSeconds - 0.15f, fadeSeconds + 0.25f));
            Assert.IsFalse(outgoing.isPlaying);
            if (replace)
            {
                var incoming = _root.transform.Find("Bgm0").GetComponent<AudioSource>();
                yield return Until(() => Mathf.Approximately(incoming.volume, 0.6f), 1f);
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
