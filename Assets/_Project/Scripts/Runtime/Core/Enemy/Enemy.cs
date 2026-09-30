using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class Enemy : MonoBehaviour
    {
        private EnemyModel _enemyModel;
        private EnemyProjectile _projectilePrefab;
        private IDisposable _subscriptions;

        public void Bind(EnemyModel enemyModel,
            EnemyProjectile projectilePrefab,
            ISubscriber<EnemyHpChanged> hpChanged,
            ISubscriber<EnemyDied> died)
        {
            _enemyModel = enemyModel;
            _projectilePrefab = projectilePrefab;
            _enemyModel.ProjectileFired += OnProjectileFired;

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

        private void OnProjectileFired(EnemyProjectileModel projectile)
        {
            if (_projectilePrefab == null)
                return;

            var view = Instantiate(_projectilePrefab, projectile.Position, Quaternion.identity);
            view.Bind(projectile);
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
            if(_enemyModel != null)
                _enemyModel.ProjectileFired -= OnProjectileFired;

            _subscriptions?.Dispose();
        }
    }
}
