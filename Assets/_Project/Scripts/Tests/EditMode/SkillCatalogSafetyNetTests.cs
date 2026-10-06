using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// 스킬 구조를 바꾸기 전의 안전망. 새 카드와 새 스킬을 넣을 때 조용히 깨지는 곳을 테스트가 먼저 알려 준다.
    /// 카탈로그 의미 검증, 스냅샷 복사 누락, 카드 효과 골든, 프리팹과 아이콘 연결을 지킨다.
    /// </summary>
    public sealed class SkillCatalogSafetyNetTests
    {
        private const string GoldenRelativePath = "_Project/Scripts/Tests/EditMode/Golden/WeaponCardStats.golden.txt";

        private static IEnumerable<string> AllEffectKinds() => EffectRegistry.Keys.Where(EffectRegistry.IsStatKind).ToList();

        private static List<SkillData> LoadUnvalidated() =>
            JsonConvert.DeserializeObject<List<SkillData>>(Resources.Load<TextAsset>("MockData/Skills").text);

        private static SkillStats Baseline() => SkillStats.FromDefinition(new SkillBaseStats
        {
            cast = { baseDamage = 100, cooldown = 2, projectileCount = 2, castCount = 1 },
            projectile = { speed = 10, pierceCount = 1, knockbackDistance = 1 },
            status = { freezeDuration = 1, paralysisDuration = 1, stunDuration = 1, slowDuration = 1 },
            explosion = { radius = 2, damageRatio = .5f },
            area = { radius = 2, duration = 3, pulseInterval = 1, moveSpeed = 1, pull = 1 },
            beam = { length = 10, width = 1, duration = 2, pulses = 4 },
            chain = { bounces = 3, jumpRange = 4, hopInterval = .1f, pathWidth = 1 },
            field = { damageRatio = .5f, radius = 2, slowRatio = .3f }
        });

        // 기준값과 겹치지 않으면서 규칙이 받아들이는 값. 폭발 계열 두 개는 1만, 형태는 정의된 값만 받는다.
        private static float ProbeValue(string type) => type switch
        {
            "burnDeathExplosion" => 1,
            "form" => 3,
            _ => 5
        };

        // ── 새 효과 종류 등록 누락 ──────────────────────────────────

        [TestCaseSource(nameof(AllEffectKinds))]
        public void EveryEffectKind_IsConsumedByAtLeastOneAttack(string type)
        {
            Assert.IsTrue(Enum.GetValues(typeof(CastType)).Cast<CastType>().Any(c => EffectRegistry.Supports(c, type)),
                $"{type}을 소비하는 공격이 없습니다. EffectRegistry의 허용 공격을 확인하세요.");
        }

        [TestCaseSource(nameof(AllEffectKinds))]
        public void EveryEffectKind_HasAcceptingRule(string type)
        {
            Assert.IsTrue(SkillStatEffects.TryCompile(new EffectDef { kind = type, value = ProbeValue(type) }, out _),
                $"{type}의 적용 규칙이 없거나 값을 받아들이지 않습니다.");
        }

        [TestCaseSource(nameof(AllEffectKinds))]
        public void EveryEffectKind_ChangesSomeStat(string type)
        {
            var before = SkillStatsDump.Flatten(Baseline());
            var after = SkillStatsDump.Flatten(SkillTestFactory.Apply(Baseline(), type, ProbeValue(type)));
            Assert.AreNotEqual("(변화 없음)", SkillStatsDump.Diff(before, after), $"{type}이 어떤 스탯도 바꾸지 않습니다.");
        }

        // ── 스냅샷 복사 누락 ──────────────────────────────────────────

        [Test]
        public void StatsDump_CoversEveryStatGroup()
        {
            var keys = SkillStatsDump.Flatten(Baseline()).Keys.ToList();
            foreach (var group in new[] { "Cast.", "Projectile.", "Status.", "Burn.", "Explosion.", "Lightning.", "Field.", "Area.", "Beam.", "Chain." })
            {
                Assert.IsTrue(keys.Any(k => k.StartsWith(group, StringComparison.Ordinal)), group);
            }
            Assert.GreaterOrEqual(keys.Count, 35, "리플렉션이 스탯을 충분히 읽지 못했습니다.");
        }

        [Test]
        public void SnapshotCopy_PreservesEveryField()
        {
            var effects = AllEffectKinds().Select(t => new EffectDef { kind = t, value = ProbeValue(t) }).ToList();
            Assert.IsTrue(SkillStatEffects.TryApply(Baseline(), effects, out var full));
            Assert.IsTrue(SkillStatEffects.TryApply(full, new List<EffectDef>(), out var copy), "빈 강화 적용이 실패했습니다.");

            var expected = SkillStatsDump.Flatten(full);
            var actual = SkillStatsDump.Flatten(copy);
            Assert.AreEqual("(변화 없음)", SkillStatsDump.Diff(expected, actual),
                "스냅샷을 복사하면서 값이 바뀌었습니다. WeaponStatsBuilder의 복사 생성자에서 필드가 빠졌는지 확인하세요.");
        }

        // ── 카탈로그 의미 검증 ────────────────────────────────────────

        [Test]
        public void Validator_AcceptsShippedCatalog()
        {
            Assert.DoesNotThrow(() => SkillCatalogValidator.Validate(LoadUnvalidated()));
        }

        [Test]
        public void Validator_RejectsProjectileOnlyEffectOnHitscanWeapon()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Hitscan);
            var card = weapon.upgrades[0];
            card.effects.Add(new EffectDef { kind = "pierceCount", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
            StringAssert.Contains("pierceCount", error.Message);
        }

        [Test]
        public void Validator_RejectsHitscanOnlyEffectOnProjectileWeapon()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Projectile);
            var card = weapon.upgrades[0];
            card.effects.Add(new EffectDef { kind = "fieldDuration", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
            StringAssert.Contains("fieldDuration", error.Message);
        }

        [Test]
        public void Validator_ChecksEffectsInsideVariants()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Hitscan && w.upgrades.Any(u => u.variants != null && u.variants.Length > 0));
            var card = weapon.upgrades.First(u => u.variants != null && u.variants.Length > 0);
            card.variants[0].effects.Add(new EffectDef { kind = "projectileSpeed", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
        }

        // ── 카드 효과 골든 ────────────────────────────────────────────
        // 구조를 바꾸는 동안 카드 66장의 수치가 달라지지 않는지 지킨다.
        // 의도한 밸런스 변경이면 RegenerateGolden을 실행해 파일을 갱신하고 변경 내용을 리뷰한다.

        [Test]
        public void CardEffects_MatchGoldenSnapshot()
        {
            string path = Path.Combine(Application.dataPath, GoldenRelativePath);
            Assert.IsTrue(File.Exists(path), "골든 파일이 없습니다. RegenerateGolden을 실행하세요: " + path);

            string expected = Normalize(File.ReadAllText(path));
            string actual = Normalize(BuildGolden());
            if (expected != actual)
            {
                Assert.Fail("카드 효과 수치가 골든과 다릅니다.\n" + FirstDifference(expected, actual));
            }
        }

        [Test, Explicit("골든 파일을 현재 동작으로 다시 만든다. 의도한 변경일 때만 실행한다.")]
        public void RegenerateGolden()
        {
            string path = Path.Combine(Application.dataPath, GoldenRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BuildGolden(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Assert.Pass("골든을 갱신했습니다: " + path);
        }

        private static string BuildGolden()
        {
            var text = new StringBuilder();
            foreach (var weapon in new DefaultSkillDataProvider(new GameDataStore()).LoadAll().OrderBy(w => w.id))
            {
                // 자식 스킬 시전 효과도 적용해 볼 수 있도록 아무것도 하지 않는 시전기를 연결한다.
                var start = SkillConfig.FromDefinition(weapon).WithChildCaster(new NoopChildCaster());
                var baseline = SkillStatsDump.Flatten(start.Stats);
                foreach (var option in weapon.upgrades)
                {
                    foreach (int level in PermanentLevels(option))
                    {
                        Assert.IsTrue(SkillUpgradeResolver.TryResolve(option, level, out var resolved), option.id);
                        var builder = new SkillConfigBuilder(start);
                        Assert.IsTrue(builder.TryApplyCatalog(resolved.Effects), option.id);
                        text.Append(weapon.id).Append('/').Append(option.id).Append('@').Append(level).Append(": ")
                            .Append(SkillStatsDump.Diff(baseline, SkillStatsDump.Flatten(builder.Build().Stats))).Append('\n');
                    }
                }

                // 조건과 무관하게 모든 카드를 최대 횟수까지 순서대로 적용한 결과
                var all = new SkillConfigBuilder(start);
                foreach (var option in weapon.upgrades)
                {
                    Assert.IsTrue(SkillUpgradeResolver.TryResolve(option, 0, out var resolved), option.id);
                    for (int n = 0; n < option.maxPickCount; n++) { Assert.IsTrue(all.TryApplyCatalog(resolved.Effects), option.id); }
                }
                text.Append(weapon.id).Append("/ALL@0: ")
                    .Append(SkillStatsDump.Diff(baseline, SkillStatsDump.Flatten(all.Build().Stats))).Append('\n');
            }
            return text.ToString();
        }

        private static IEnumerable<int> PermanentLevels(SkillUpgradeOption option) =>
            new[] { 0 }.Concat((option.variants ?? Array.Empty<SkillUpgradeVariant>()).Select(v => v.minPermanentLevel))
                .Distinct().OrderBy(level => level);

        private static string Normalize(string text) => text.Replace("\r\n", "\n");

        private static string FirstDifference(string expected, string actual)
        {
            var a = expected.Split('\n');
            var b = actual.Split('\n');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string left = i < a.Length ? a[i] : "(없음)";
                string right = i < b.Length ? b[i] : "(없음)";
                if (left != right) { return $"{i + 1}번째 줄\n  골든: {left}\n  현재: {right}"; }
            }
            return "(차이를 찾지 못했습니다)";
        }

        // ── 프리팹과 아이콘 연결 (SkillAssetTable) ───────────────────

        private static SkillAssetTable Table()
        {
            var table = TestSkillAssets.Real();
            Assert.IsNotNull(table, TestSkillAssets.TablePath);
            return table;
        }

        [Test]
        public void EveryCatalogSkill_HasPrefabWithMatchingComponent()
        {
            var table = Table();
            var problems = new List<string>();
            foreach (var skill in new DefaultSkillDataProvider(new GameDataStore()).LoadAll())
            {
                var prefab = table.GetPrefab(skill.assetKey);
                if (prefab == null)
                {
                    problems.Add($"{skill.id}({skill.name}): SkillAssetTable에 assetKey '{skill.assetKey}'의 프리팹이 없습니다.");
                    continue;
                }
                bool ok = skill.castType switch
                {
                    CastType.Hitscan => prefab.GetComponent<HitscanEffect>() != null,
                    CastType.Area or CastType.Beam => prefab.GetComponent<AreaZone>() != null,
                    CastType.Chain => prefab.GetComponent<ChainBolt>() != null,
                    _ => prefab.GetComponent<Projectile>() != null
                };
                if (!ok) { problems.Add($"{skill.id}({skill.name}): {prefab.name}에 {skill.castType}용 컴포넌트가 없습니다."); }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test(Description = "HUD와 카드에 나오는 스킬(자식 전용 제외)은 HUD 아이콘과 카드 아이콘 두 장이 모두 있어야 한다")]
        public void EveryShownSkill_HasHudAndCardIcons()
        {
            var table = Table();
            var missing = new List<string>();
            foreach (var skill in new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Where(w => !w.childOnly))
            {
                table.TryGet(skill.assetKey, out var entry);
                if (entry == null || entry.hudIcon == null) { missing.Add($"{skill.id}({skill.name}): {skill.assetKey} hudIcon"); }
                if (entry == null || entry.newCardIcon == null) { missing.Add($"{skill.id}({skill.name}): {skill.assetKey} newCardIcon"); }
                if (entry == null || entry.upgradeCardIcon == null) { missing.Add($"{skill.id}({skill.name}): {skill.assetKey} upgradeCardIcon"); }
            }
            Assert.IsEmpty(missing, "SkillAssetTable에 아이콘이 없습니다.\n" + string.Join("\n", missing));
        }

        [Test(Description = "표에는 기본 아이콘이 있고, 키가 비어 있거나 겹치거나 스킬 없이 남은 항목이 없어야 한다")]
        public void AssetTable_HasFallbacksAndConsistentKeys()
        {
            var table = Table();
            var so = new SerializedObject(table);
            Assert.IsNotNull(so.FindProperty("hudFallback").objectReferenceValue, "hudFallback이 비어 있습니다.");
            Assert.IsNotNull(so.FindProperty("cardFallback").objectReferenceValue, "cardFallback이 비어 있습니다.");

            var known = new HashSet<string>(new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Select(w => w.assetKey));
            foreach (var card in new GameDataStore().GeneralCards) { known.Add(card.IconKey); }

            var seen = new HashSet<string>();
            var problems = new List<string>();
            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrEmpty(entry.key)) { problems.Add("키가 빈 항목이 있습니다."); continue; }
                if (!seen.Add(entry.key)) { problems.Add($"키가 겹칩니다: {entry.key}"); }
                if (!known.Contains(entry.key)) { problems.Add($"어느 스킬(assetKey)이나 일반 카드(IconKey)도 쓰지 않는 항목: {entry.key}"); }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
