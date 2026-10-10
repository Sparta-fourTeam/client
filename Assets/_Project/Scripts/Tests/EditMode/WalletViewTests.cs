using System;
using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using Game.View;
using MessagePipe;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WalletViewTests
    {
        private GameObject _root;
        private WalletView _view;
        private TMP_Text _text;
        private IServiceProvider _provider;
        private LobbyModel _model;
        private PlayerProfile _profile;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WalletChanged>();
            builder.AddMessageBroker<ProgressChanged>();
            _provider = builder.BuildServiceProvider();
            _root = new GameObject("Wallet", typeof(RectTransform));
            _text = _root.AddComponent<TextMeshProUGUI>();
            _text.text = "Waiting";
            _view = _root.AddComponent<WalletView>();
            typeof(WalletView).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, _text);
            _profile = new PlayerProfile(new GameDataStore());
            _model = new LobbyModel(_profile,
                _provider.GetRequiredService<IBufferedPublisher<WalletChanged>>(),
                _provider.GetRequiredService<IBufferedPublisher<ProgressChanged>>());
        }

        [TearDown]
        public void TearDown()
        {
            _model.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
            (_provider as IDisposable)?.Dispose();
        }

        [TestCase(0)]
        [TestCase(8500)]
        public void StartupIgnoresEmptyBufferAndDisplaysSavedBalanceImmediately(int gold)
        {
            _view.Construct(_provider.GetRequiredService<IBufferedSubscriber<WalletChanged>>());
            Assert.AreEqual("Waiting", _text.text, "An empty buffer must not establish a zero balance.");
            _profile.Apply(new PlayerSnapshot { gold = gold });
            _model.Start();
            Assert.AreEqual(CurrencyFormat.Format(gold), _text.text);
        }

        [TestCase(0, 100)]
        [TestCase(100, 50)]
        public void ActualBalanceChangesStillAnimateAfterInitialization(int initial, int next)
        {
            _profile.Apply(new PlayerSnapshot { gold = initial });
            _model.Start();
            _view.Construct(_provider.GetRequiredService<IBufferedSubscriber<WalletChanged>>());
            Assert.AreEqual(CurrencyFormat.Format(initial), _text.text);
            _profile.Apply(new PlayerSnapshot { gold = next });
            Assert.AreEqual(CurrencyFormat.Format(initial), _text.text, "An actual change starts from the displayed balance.");
            var tween = (Tween)typeof(WalletView).GetField("_countTween", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_view);
            Assert.IsNotNull(tween);
            tween.Complete();
            Assert.AreEqual(CurrencyFormat.Format(next), _text.text);
        }
    }
}
