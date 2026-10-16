using System;
using System.Collections;
using System.Linq;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class AudioPlaybackTests
    {
        [UnityTest]
        public IEnumerator BattlePause_PreservesEffectCursorAndUiAudio_ThenResumesWithoutRestarting()
        {
            var audio = new AudioService();
            GameObject listener = null;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                listener = new GameObject("AudioPlaybackTestListener", typeof(AudioListener));
            }

            var existing = Object.FindObjectsByType<AudioSource>().ToHashSet();
            try
            {
                audio.Start();
                audio.PlayBgm(SoundIds.BattleBgm, 0f);
                audio.PlaySfx(SoundIds.SkillCast(13));
                var sources = Object.FindObjectsByType<AudioSource>().Where(s => !existing.Contains(s)).ToArray();
                var effect = sources.Single(s => s.clip != null && !s.loop);
                var music = sources.Single(s => s.clip != null && s.loop);
                yield return Until(() => effect.timeSamples > 0 && music.timeSamples > 0);

                audio.SetBattleSfxPaused(true);
                audio.SetBattleSfxPaused(true); // 같은 정지 통지가 반복돼도 보존한 보이스를 잃지 않는다.
                int pausedAt = effect.timeSamples;
                int musicAt = music.timeSamples;
                Assert.That(pausedAt, Is.GreaterThan(0));
                Assert.That(effect.isPlaying, Is.False);

                audio.PlaySfx(SoundIds.UiClick);
                var click = sources.Single(s => s.clip != null && s != effect && !s.loop);
                yield return Until(() => click.timeSamples > 0 && music.timeSamples > musicAt);
                Assert.That(effect.timeSamples, Is.EqualTo(pausedAt), "선택 중에는 공격음의 재생 위치를 보존한다");

                audio.SetBattleSfxPaused(false);
                Assert.That(effect.isPlaying, Is.True);
                Assert.That(effect.timeSamples, Is.GreaterThanOrEqualTo(pausedAt), "처음부터 재생하면 안 된다");
                yield return Until(() => effect.timeSamples > pausedAt);

                audio.SetBattleSfxPaused(true);
                audio.StopBattleSfx();
                audio.SetBattleSfxPaused(false);
                Assert.That(effect.isPlaying, Is.False, "전투 종료 뒤에는 보존했던 공격음을 다시 재생하지 않는다");
                Assert.That(music.isPlaying, Is.True);
            }
            finally
            {
                audio.Dispose();
                if (listener != null) { Object.Destroy(listener); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiLoop_StopsOnCancelAndReusesSourceWhenRestartedDuringBattlePause()
        {
            var audio = new AudioService();
            GameObject listener = null;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                listener = new GameObject("UiLoopTestListener", typeof(AudioListener));
            }
            var existing = Object.FindObjectsByType<AudioSource>().ToHashSet();
            try
            {
                audio.Start();
                audio.SetBattleSfxPaused(true);
                audio.PlayUiLoop(SoundIds.RouletteSpin);
                var loop = Object.FindObjectsByType<AudioSource>().Single(s => !existing.Contains(s) && s.clip != null);
                Assert.That(loop.loop, Is.True);
                yield return Until(() => loop.timeSamples > 0);
                var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
                foreach (var id in new[] { SoundIds.Upgrade, SoundIds.LevelUp, SoundIds.UiClick })
                {
                    audio.PlaySfx(id);
                    Assert.That(library.TryGet(id, out var entry), Is.True);
                    Assert.That(Object.FindObjectsByType<AudioSource>().Any(s => !existing.Contains(s)
                        && !s.loop && entry.clips.Contains(s.clip) && s.isPlaying), Is.True,
                        "강화·레벨업·클릭음이 겹쳐도 누락되지 않아야 한다: " + id);
                }
                int position = loop.timeSamples;
                audio.PlayUiLoop(SoundIds.RouletteSpin);
                Assert.That(loop.timeSamples, Is.GreaterThanOrEqualTo(position), "중복 요청으로 회전음을 되감지 않는다");
                audio.StopUiLoop(SoundIds.RouletteSpin);
                audio.StopUiLoop(SoundIds.RouletteSpin);
                Assert.That(loop.isPlaying, Is.False, "스킵하면 긴 클립의 남은 소리를 멈춘다");
                audio.PlayUiLoop(SoundIds.RouletteSpin);
                yield return Until(() => loop.isPlaying && loop.timeSamples > 0);
                Assert.That(Object.FindObjectsByType<AudioSource>().Count(s => !existing.Contains(s) && s.clip == loop.clip), Is.EqualTo(1));
            }
            finally
            {
                audio.Dispose();
                if (listener != null) { Object.Destroy(listener); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimedBattleSound_FadesBeforeAttackEndsAndPreservesPause()
        {
            var audio = new AudioService();
            GameObject listener = null;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                listener = new GameObject("BeamFadeTestListener", typeof(AudioListener));
            }
            var existing = Object.FindObjectsByType<AudioSource>().ToHashSet();
            try
            {
                audio.Start();
                const float duration = 1.3f;
                const float fadeSeconds = 0.3f;
                float startedAt = Time.time;
                audio.PlaySfx(SoundIds.SkillCast(13), duration, fadeSeconds);
                var beam = Object.FindObjectsByType<AudioSource>().Single(s => !existing.Contains(s) && s.clip != null);
                float peak = beam.volume;
                Assert.That(beam.loop, Is.True, "클립이 공격보다 짧아도 공격이 끝날 때까지 유지한다");
                yield return Until(() => beam.isPlaying && beam.volume < peak * 0.9f);
                float remainingSeconds = (duration - (Time.time - startedAt)) / Time.timeScale;
                Assert.That(remainingSeconds, Is.InRange(0f, fadeSeconds + 0.05f), "공격 종료 전 마지막 실제 0.3초에 줄어든다");
                float pausedVolume = beam.volume;
                audio.SetBattleSfxPaused(true);
                int pausedSamples = beam.timeSamples;
                float resumeAt = Time.realtimeSinceStartup + 0.1f;
                yield return Until(() => Time.realtimeSinceStartup >= resumeAt);
                Assert.That(beam.volume, Is.EqualTo(pausedVolume));
                Assert.That(beam.timeSamples, Is.EqualTo(pausedSamples));
                audio.SetBattleSfxPaused(false);
                Assert.That(beam.isPlaying, Is.True);
                yield return Until(() => !beam.isPlaying);
                Assert.That(beam.volume, Is.Zero);
                Assert.That(beam.loop, Is.False);

                // 같은 보이스에 다음 일반 공격이 들어와도 이전 페이드가 그 공격을 끊으면 안 된다.
                audio.PlaySfx(SoundIds.SkillCast(1));
                Assert.That(beam.isPlaying, Is.True);
                Assert.That(beam.loop, Is.False);
                Assert.That(beam.volume, Is.GreaterThan(0f));
                yield return Until(() => beam.timeSamples > 0);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                audio.Dispose();
                if (listener != null) { Object.Destroy(listener); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedAudioObjects_DoNotInterruptStageScopeDisposal()
        {
            var existing = Object.FindObjectsByType<AudioSource>().ToHashSet();
            var messages = new BuiltinContainerBuilder();
            messages.AddMessagePipe();
            messages.AddMessageBroker<EnemyDied>();
            messages.AddMessageBroker<EnemyHpChanged>();
            messages.AddMessageBroker<SkillFired>();
            messages.AddMessageBroker<WaveStarted>();
            messages.AddMessageBroker<SpecialEnemyWarning>();
            messages.AddMessageBroker<WallHpChanged>();
            messages.AddMessageBroker<WallDestroyed>();
            messages.AddMessageBroker<StageEnded>();
            messages.AddMessageBroker<StageStateChanged>();
            var services = messages.BuildServiceProvider();
            var builder = new ContainerBuilder();
            builder.Register<StageContext>(Lifetime.Singleton);
            builder.Register<AudioService>(Lifetime.Singleton).AsSelf().As<IAudioService>();
            var root = builder.Build();
            IScopedObjectResolver stage = null;
            GameObject listener = null;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                listener = new GameObject("AudioDisposalTestListener", typeof(AudioListener));
            }
            try
            {
                stage = root.CreateScope(scope => scope.RegisterEntryPoint(r => new BattleSoundPlayer(
                    r.Resolve<IAudioService>(), r.Resolve<StageContext>(),
                    services.GetRequiredService<ISubscriber<EnemyDied>>(),
                    services.GetRequiredService<ISubscriber<EnemyHpChanged>>(),
                    services.GetRequiredService<ISubscriber<SkillFired>>(),
                    services.GetRequiredService<ISubscriber<WaveStarted>>(),
                    services.GetRequiredService<ISubscriber<SpecialEnemyWarning>>(),
                    services.GetRequiredService<ISubscriber<WallHpChanged>>(),
                    services.GetRequiredService<ISubscriber<WallDestroyed>>(),
                    services.GetRequiredService<ISubscriber<StageEnded>>(),
                    services.GetRequiredService<IBufferedSubscriber<StageStateChanged>>()), Lifetime.Singleton));
                var audio = root.Resolve<AudioService>();
                audio.Start();
                audio.PlayBgm(SoundIds.BattleBgm, 0f);
                audio.PlaySfx(SoundIds.SkillCast(13), 5f, 0.3f);
                audio.PlayUiLoop(SoundIds.RouletteSpin);
                audio.SetBattleSfxPaused(true);
                var audioRoot = Object.FindObjectsByType<AudioSource>().First(s => !existing.Contains(s)).transform.root.gameObject;

                // Unity의 종료 순서처럼 AudioSource가 서비스·스테이지보다 먼저 파괴된다.
                Object.Destroy(audioRoot);
                yield return Until(() => audioRoot == null);
                Assert.DoesNotThrow(() => audio.StopUiLoop(SoundIds.RouletteSpin));
                Assert.DoesNotThrow(() => audio.SetBattleSfxPaused(false));
                Assert.DoesNotThrow(() => stage.Dispose(), "오디오 정리 예외로 엔트리포인트 정리가 중단되면 안 된다");
                stage = null;
                Assert.DoesNotThrow(root.Dispose);
                Assert.DoesNotThrow(audio.Dispose);
                audio.PlaySfx(SoundIds.UiClick);
                audio.PlayUiLoop(SoundIds.RouletteSpin);
                Assert.That(Object.FindObjectsByType<AudioSource>().Any(s => !existing.Contains(s)), Is.False,
                    "종료된 서비스가 다음 재생에서 오디오 오브젝트를 다시 만들면 안 된다");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                stage?.Dispose();
                root.Dispose();
                (services as IDisposable)?.Dispose();
                if (listener != null) { Object.Destroy(listener); }
            }
            yield return null;
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "오디오 재생이 진행되지 않았다");
                yield return null;
            }
        }
    }
}
