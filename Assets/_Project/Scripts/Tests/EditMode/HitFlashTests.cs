using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HitFlashTests
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private readonly List<GameObject> _created = new();
        private readonly List<Material> _materials = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _created.Clear();
            _materials.Clear();
        }

        private GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            _created.Add(go);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        /// <summary>원래 머티리얼(구분하려고 이름을 붙인 임시 머티리얼)과 원래 색을 가진 SpriteRenderer</summary>
        private SpriteRenderer AddRenderer(GameObject go, Color color, string materialName)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            var material = new Material(sr.sharedMaterial) { name = materialName };
            _materials.Add(material);
            sr.sharedMaterial = material;
            return sr;
        }

        private static float AmountOf(SpriteRenderer sr)
        {
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            return block.GetFloat(FlashAmountId);
        }

        private static Color FlashColorOf(SpriteRenderer sr)
        {
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            return block.GetColor(FlashColorId);
        }

        // ───────── 섞는 정도 ─────────

        [Test(Description = "앞쪽 60%는 peak을 유지하고 마지막 40% 동안 0으로 줄어든다")]
        public void AmountAt_HoldsPeakThenFades()
        {
            Assert.AreEqual(1f, HitFlash.AmountAt(0.15f, 0.15f, 1f), 0.0001f, "맞은 직후");
            Assert.AreEqual(1f, HitFlash.AmountAt(0.075f, 0.15f, 1f), 0.0001f, "절반까지는 유지");
            Assert.AreEqual(1f, HitFlash.AmountAt(0.06f, 0.15f, 1f), 0.0001f, "유지가 끝나는 지점");
            Assert.AreEqual(0.5f, HitFlash.AmountAt(0.03f, 0.15f, 1f), 0.0001f, "줄어드는 구간의 중간");
            Assert.AreEqual(0f, HitFlash.AmountAt(0f, 0.15f, 1f), 0.0001f, "끝");
        }

        [Test(Description = "남은 시간이 범위를 벗어나거나 지속 시간이 0 이하여도 0~peak 안이다")]
        public void AmountAt_IsClampedAndSafe()
        {
            Assert.AreEqual(1f, HitFlash.AmountAt(10f, 0.15f, 1f), 0.0001f);
            Assert.AreEqual(0f, HitFlash.AmountAt(-1f, 0.15f, 1f), 0.0001f);
            Assert.AreEqual(0f, HitFlash.AmountAt(0.1f, 0f, 1f), 0.0001f);
            Assert.AreEqual(0.4f, HitFlash.AmountAt(0.15f, 0.15f, 0.4f), 0.0001f, "peak이 작으면 그 값이 상한");
        }

        // ───────── 머티리얼 바꿔 끼우기 ─────────

        [Test(Description = "플래시용 머티리얼이 Resources에 있고 셰이더에 섞는 정도와 색 속성이 있다")]
        public void FlashMaterial_ExistsInResources()
        {
            var material = Resources.Load<Material>("Materials/SpriteHitFlash");

            Assert.IsNotNull(material);
            Assert.IsTrue(material.shader.isSupported);
            Assert.IsTrue(material.HasProperty(FlashAmountId));
            Assert.IsTrue(material.HasProperty(FlashColorId));
        }

        [Test(Description = "Flash하면 자식 조각까지 모든 SpriteRenderer가 플래시 머티리얼로 바뀌고 섞는 정도가 peak이다")]
        public void Flash_SwapsEveryChildRenderer()
        {
            var root = NewObject("Enemy");
            var rootSprite = AddRenderer(root, Color.white, "RootMat");
            var body = AddRenderer(NewObject("Body", root.transform), Color.white, "BodyMat");
            var arm = AddRenderer(NewObject("Arm", root.transform), Color.white, "ArmMat");
            var flash = root.AddComponent<HitFlash>();

            flash.Flash();

            foreach (var sr in new[] { rootSprite, body, arm })
            {
                Assert.AreEqual("SpriteHitFlash", sr.sharedMaterial.name, sr.name);
                Assert.AreEqual(1f, AmountOf(sr), 0.0001f, sr.name);
            }
        }

        [Test(Description = "원래 색이 무엇이든 같은 정도로 같은 플래시 색이 적용된다")]
        public void Flash_IsIndependentOfBaseColor()
        {
            var colors = new[]
            {
                Color.white,
                new Color(0.2f, 0.9f, 0.3f, 1f),   // 초록 슬라임
                Color.black,
                new Color(1f, 0.1f, 0.1f, 1f)       // 이미 빨간 몬스터
            };
            var amounts = new List<float>();
            var flashColors = new List<Color>();
            foreach (var baseColor in colors)
            {
                var root = NewObject("Enemy");
                var sr = AddRenderer(root, baseColor, "Mat");
                root.AddComponent<HitFlash>().Flash();
                amounts.Add(AmountOf(sr));
                flashColors.Add(FlashColorOf(sr));
            }

            Assert.That(amounts, Is.All.EqualTo(1f).Within(0.0001f));
            for (int i = 1; i < flashColors.Count; i++)
            {
                Assert.AreEqual(flashColors[0], flashColors[i], "원래 색 " + colors[i] + "과 상관없이 같은 플래시 색");
            }
        }

        [Test(Description = "플래시 중에도 스프라이트의 원래 색(SpriteRenderer.color)은 건드리지 않는다")]
        public void Flash_DoesNotChangeSpriteColor()
        {
            var root = NewObject("Enemy");
            var baseColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            var sr = AddRenderer(root, baseColor, "Mat");
            var flash = root.AddComponent<HitFlash>();

            flash.Flash();
            flash.Advance(0.05f);

            Assert.AreEqual(baseColor, sr.color);
        }

        [Test(Description = "시간이 지나면 섞는 정도가 줄어들고, 끝나면 원래 머티리얼로 돌아가고 속성 블록도 지워진다")]
        public void Advance_FadesAndRestoresOriginalMaterial()
        {
            var root = NewObject("Enemy");
            var sr = AddRenderer(root, Color.white, "OriginalMat");
            var original = sr.sharedMaterial;
            var flash = root.AddComponent<HitFlash>();
            flash.Flash();

            flash.Advance(0.12f);                        // 남은 0.03초는 줄어드는 구간의 중간

            Assert.AreEqual(0.5f, AmountOf(sr), 0.0001f);
            Assert.AreEqual("SpriteHitFlash", sr.sharedMaterial.name);

            flash.Advance(1f);

            Assert.AreSame(original, sr.sharedMaterial);
            Assert.IsFalse(sr.HasPropertyBlock());
        }

        [Test(Description = "플래시 중에 또 맞으면 처음부터 다시 시작한다")]
        public void Flash_WhileFlashing_RestartsFromPeak()
        {
            var root = NewObject("Enemy");
            var sr = AddRenderer(root, Color.white, "Mat");
            var flash = root.AddComponent<HitFlash>();
            flash.Flash();
            flash.Advance(0.1f);

            flash.Flash();

            Assert.AreEqual(1f, AmountOf(sr), 0.0001f);
        }

        [Test(Description = "Restore하면 진행 중이던 플래시를 멈추고 원래 머티리얼로 돌린다 (비활성화될 때 남지 않게)")]
        public void Restore_ReturnsOriginalMaterial()
        {
            var root = NewObject("Enemy");
            var sr = AddRenderer(root, Color.white, "OriginalMat");
            var original = sr.sharedMaterial;
            var flash = root.AddComponent<HitFlash>();
            flash.Flash();

            flash.Restore();
            flash.Advance(0.05f);

            Assert.AreSame(original, sr.sharedMaterial);
            Assert.IsFalse(sr.HasPropertyBlock());
        }

        [Test(Description = "플래시를 시작하지 않았으면 Advance와 Restore는 아무것도 바꾸지 않는다")]
        public void NotFlashing_LeavesRendererUntouched()
        {
            var root = NewObject("Enemy");
            var sr = AddRenderer(root, new Color(0.3f, 0.6f, 0.9f, 1f), "OriginalMat");
            var original = sr.sharedMaterial;
            var flash = root.AddComponent<HitFlash>();

            flash.Advance(0.1f);
            flash.Restore();

            Assert.AreSame(original, sr.sharedMaterial);
            Assert.IsFalse(sr.HasPropertyBlock());
        }

        // ───────── Enemy 뷰와 연결 ─────────

        private sealed class Fixture
        {
            public Enemy Enemy;
            public SpriteRenderer Sprite;
            public EnemyModel Model;
            public IPublisher<EnemyHpChanged> HpPublisher;
        }

        // preDamage: Bind하기 전에 먼저 받은 피해(체력이 줄어든 상태로 시작하는 적)
        private Fixture CreateBoundEnemy(int id, Color spriteColor, int preDamage = 0)
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<EnemyHpChanged>();
            builder.AddMessageBroker<EnemyDied>();
            IServiceProvider provider = builder.BuildServiceProvider();
            var hpPublisher = provider.GetRequiredService<IPublisher<EnemyHpChanged>>();
            var diedPublisher = provider.GetRequiredService<IPublisher<EnemyDied>>();

            var model = new EnemyModel(id, 0f, EnemyType.Normal, 10,
                new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f), hpPublisher, diedPublisher);

            if (preDamage > 0)
            {
                model.TakeDamage(preDamage);
            }

            var go = NewObject("EnemyView");
            var sr = AddRenderer(go, spriteColor, "OriginalMat");
            var enemy = go.AddComponent<Enemy>();
            enemy.Bind(model, null,
                provider.GetRequiredService<ISubscriber<EnemyHpChanged>>(),
                provider.GetRequiredService<ISubscriber<EnemyDied>>());

            return new Fixture { Enemy = enemy, Sprite = sr, Model = model, HpPublisher = hpPublisher };
        }

        [Test(Description = "프리팹에 HitFlash가 없어도 Bind하면 붙는다")]
        public void Bind_AddsHitFlashWhenMissing()
        {
            var f = CreateBoundEnemy(1, Color.white);

            Assert.IsNotNull(f.Enemy.GetComponent<HitFlash>());
        }

        [Test(Description = "이 적이 데미지를 받으면 원래 색과 상관없이 플래시한다")]
        public void Damage_FlashesThisEnemy_WhateverItsColor()
        {
            var green = CreateBoundEnemy(1, new Color(0.2f, 0.9f, 0.3f, 1f));

            green.Model.TakeDamage(3);

            Assert.AreEqual("SpriteHitFlash", green.Sprite.sharedMaterial.name);
            Assert.AreEqual(1f, AmountOf(green.Sprite), 0.0001f);
        }

        [Test(Description = "체력이 회복될 때는 피격 플래시를 내지 않는다")]
        public void Heal_DoesNotFlash()
        {
            var f = CreateBoundEnemy(1, Color.white, preDamage: 5);

            f.Model.Heal(3);

            Assert.AreEqual("OriginalMat", f.Sprite.sharedMaterial.name);
            Assert.IsFalse(f.Sprite.HasPropertyBlock());
        }

        [Test(Description = "회복한 뒤에 다시 맞으면 플래시한다")]
        public void DamageAfterHeal_StillFlashes()
        {
            var f = CreateBoundEnemy(1, Color.white, preDamage: 5);

            f.Model.Heal(3);
            f.Model.TakeDamage(1);

            Assert.AreEqual("SpriteHitFlash", f.Sprite.sharedMaterial.name);
        }

        [Test(Description = "다른 적의 피격 메시지에는 반응하지 않는다")]
        public void OtherEnemyDamage_DoesNotFlash()
        {
            var f = CreateBoundEnemy(1, Color.white);

            f.HpPublisher.Publish(new EnemyHpChanged(2, 5, 10));

            Assert.AreEqual("OriginalMat", f.Sprite.sharedMaterial.name);
            Assert.IsFalse(f.Sprite.HasPropertyBlock());
        }
    }
}
