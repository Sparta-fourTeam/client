using Game.Core;
using Game.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AudioRoutingTests
    {
        [Test]
        public void SkillCatalog_AllSkillsHaveImportedCastAndHitClips()
        {
            var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            Assert.That(library, Is.Not.Null);
            foreach (var skill in new GameDataStore().LoadSkills())
            {
                foreach (var id in new[] { SoundIds.SkillCast(skill.id), SoundIds.SkillHit(skill.id) })
                {
                    Assert.That(id, Is.Not.Null, $"Skill {skill.id}");
                    Assert.That(library.TryGet(id, out var entry), Is.True, id);
                    Assert.That(entry.clips, Is.Not.Empty, id);
                    Assert.That(entry.pauseWithBattle, Is.True, id);
                    foreach (var clip in entry.clips)
                    {
                        Assert.That(clip, Is.Not.Null, id);
                        Assert.That(clip.length, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), id);
                    }
                    Assert.That(entry.minInterval, Is.GreaterThan(0f), id);
                }
            }
        }

        [Test]
        public void UiFeedback_HasClipsAndRemainsAvailableDuringBattlePause()
        {
            var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            foreach (var id in new[] { SoundIds.UiClick, SoundIds.UiLocked, SoundIds.StageClear, SoundIds.StageFail,
                SoundIds.Upgrade, SoundIds.LevelUp, SoundIds.RouletteSpin, SoundIds.SpecialWarning, SoundIds.CardShow })
            {
                Assert.That(library.TryGet(id, out var entry), Is.True, id);
                Assert.That(entry.clips, Is.Not.Empty, id);
                Assert.That(entry.clips, Has.All.Not.Null, id);
                Assert.That(entry.pauseWithBattle, Is.False, id);
            }
        }

        [Test]
        public void SceneMusic_AllTracksAreStereoStreamingClips()
        {
            var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            foreach (var id in new[] { SoundIds.IntroBgm, SoundIds.LobbyBgm, SoundIds.BattleBgm })
            {
                Assert.That(library.TryGet(id, out var entry), Is.True, id);
                Assert.That(entry.clips, Is.Not.Empty, id);
                Assert.That(entry.clips[0].channels, Is.EqualTo(2), id);
                Assert.That(entry.clips[0].loadType, Is.EqualTo(AudioClipLoadType.Streaming), id);
            }
        }

        [Test]
        public void HitSound_RequiresActualSkillDamageButAllowsAreaTicks()
        {
            Assert.That(SoundIds.SkillHit(default(DamageInfo)), Is.Null);
            Assert.That(SoundIds.SkillHit(new DamageInfo(0, 1)), Is.Null);
            Assert.That(SoundIds.SkillHit(new DamageInfo(-1, 1)), Is.Null);
            Assert.That(SoundIds.SkillHit(new DamageInfo(10)), Is.Null);
            Assert.That(SoundIds.SkillHit(new DamageInfo(10, 9999)), Is.Null);
            Assert.That(SoundIds.SkillHit(new DamageInfo(10, 1, isImpact: true)), Is.EqualTo("sfx_arrow_hit"));
            Assert.That(SoundIds.SkillHit(new DamageInfo(10, 6)), Is.EqualTo("sfx_cold_zone_hit"));
            Assert.That(SoundIds.SkillHit(new DamageInfo(10, 16)), Is.EqualTo("sfx_focus_hit"));
        }
    }
}
