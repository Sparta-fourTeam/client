using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class Enemy : MonoBehaviour, IParalyzableTarget
    {
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private const float DefaultDeathDuration = 0.6f;

        private EnemyModel _enemyModel;
        private Animator _animator; // 몬스터 비주얼 프리팹의 Animator (없으면 애니메이션 없이 동작)
        private bool _hasMoving, _hasHit, _hasDie, _hasAttack; // 컨트롤러마다 가진 파라미터가 다르다
        private EnemyProjectile _projectilePrefab;
        private IDisposable _subscriptions;
        private HitFlash _hitFlash;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                return;
            }

            foreach (var parameter in _animator.parameters)
            {
                _hasMoving |= parameter.nameHash == MovingHash;
                _hasHit |= parameter.nameHash == HitHash;
                _hasDie |= parameter.nameHash == DieHash;
                _hasAttack |= parameter.nameHash == AttackHash;
            }
        }

        public void Bind(EnemyModel enemyModel,
            EnemyProjectile projectilePrefab,
            ISubscriber<EnemyHpChanged> hpChanged,
            ISubscriber<EnemyDied> died)
        {
            _enemyModel = enemyModel;
            _projectilePrefab = projectilePrefab;
            _enemyModel.ProjectileFired += OnProjectileFired;
            _enemyModel.Attacked += OnAttacked;

            // 프리팹에 없으면 붙인다. 프리팹을 고치지 않아도 모든 적(스프라이트가 여러 조각인 리그 포함)이 피격 플래시를 가진다
            _hitFlash = GetComponent<HitFlash>();
            if (_hitFlash == null)
            {
                _hitFlash = gameObject.AddComponent<HitFlash>();
            }

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

            Vector3 position = _enemyModel.Position;
            if (_hasMoving && !_enemyModel.IsDead)
            {
                _animator.SetBool(MovingHash, (position - transform.position).sqrMagnitude > 0f);
            }

            transform.position = position;
        }

        private void OnAttacked()
        {
            if (_hasAttack)
            {
                _animator.SetTrigger(AttackHash);
            }
        }

        private void OnProjectileFired(EnemyProjectileModel projectile)
        {
            if (_projectilePrefab == null)
            {
                return;
            }

            var view = Instantiate(_projectilePrefab, projectile.Position, Quaternion.identity);
            view.Bind(projectile);
        }

        private void OnHpChanged(EnemyHpChanged message)
        {
            if (message.EnemyId != _enemyModel.Id)
            {
                return;
            }

            // EnemyHpChanged는 데미지를 받을 때만 발행된다
            _hitFlash.Flash();

            if (_hasHit && message.Current > 0)
            {
                _animator.SetTrigger(HitHash);
            }
        }

        private void OnDied(EnemyDied message)
        {
            if (message.EnemyId != _enemyModel.Id)
            {
                return;
            }

            if (!_hasDie)
            {
                Destroy(gameObject);
                return;
            }

            // 사망 연출이 끝난 뒤 제거
            if (_hasHit)
            {
                _animator.ResetTrigger(HitHash);
            }

            if (_hasAttack)
            {
                _animator.ResetTrigger(AttackHash);
            }

            _animator.SetTrigger(DieHash);
            Destroy(gameObject, GetClipLength("_Die", DefaultDeathDuration));
        }

        private float GetClipLength(string suffix, float fallback)
        {
            var controller = _animator.runtimeAnimatorController;
            if (controller == null)
            {
                return fallback;
            }

            foreach (var clip in controller.animationClips)
            {
                if (clip.name.EndsWith(suffix))
                {
                    return clip.length;
                }
            }

            return fallback;
        }

        private void OnDestroy()
        {
            if (_enemyModel != null)
            {
                _enemyModel.ProjectileFired -= OnProjectileFired;
                _enemyModel.Attacked -= OnAttacked;
            }

            _subscriptions?.Dispose();
        }

        public bool IsDead => _enemyModel == null || _enemyModel.IsDead;
        public IEnemyTarget Target => _enemyModel;

        public void ApplyParalysis(float duration) => _enemyModel?.ApplyParalysis(duration);

        public void TakeDamage(int damage)
        {
            _enemyModel.TakeDamage(damage);
        }
    }
}
