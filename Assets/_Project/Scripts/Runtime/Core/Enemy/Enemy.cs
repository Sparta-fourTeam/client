using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class Enemy : MonoBehaviour
    {
        private EnemyModel _enemyModel;
        private IDisposable _subscriptions;

        public void Bind(EnemyModel enemyModel,
            ISubscriber<EnemyHpChanged> hpChanged,
            ISubscriber<EnemyDied> died)
        {
            _enemyModel = enemyModel;

            var bag = DisposableBag.CreateBuilder();
            hpChanged.Subscribe(OnHpChanged).AddTo(bag);
            died.Subscribe(OnDied).AddTo(bag);
            _subscriptions = bag.Build();
        }

        private void Update()
        {
            // Instantiate 직후 Bind가 호출 되기 전 null 체크
            if (_enemyModel == null)
            {
                return;
            }

            transform.position = _enemyModel.Position;
        }

        private void OnHpChanged(EnemyHpChanged message)
        {
            if (message.EnemyId != _enemyModel.Id)
                return;
        }

        private void OnDied(EnemyDied message)
        {
            if (message.EnemyId != _enemyModel.Id)
                return;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
