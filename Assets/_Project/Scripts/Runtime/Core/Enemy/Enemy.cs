using System;
using Game.Core.Combat;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    // 적 한 마리. 위치와 이동(Transform)을 갖고, 규칙은 EnemyModel에 맡긴다.
    // 스킬이 겨누는 대상(IEnemyTarget)이고, 상태이상·밀치기 대상 인터페이스를 모델에 위임해서 구현한다
    public class Enemy : MonoBehaviour, IEnemyTarget, IFreezableTarget, IKnockbackTarget, IFrostbiteTarget,
        IParalyzableTarget, IBurnableTarget, IAreaSlowTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget
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
        private Vector3 _previousPosition; // 이동 애니메이션 판정용
        private int _lastHp; // EnemyHpChanged가 체력 감소(피격)인지 회복인지 가리는 기준
        private Vector2 _lastPosition; // 오브젝트가 파괴된 뒤에도 남은 참조가 읽을 마지막 위치
        private bool _destroyed;

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

        // 모델과 이어 붙이는 로직 쪽 연결. 피격 플래시, 상태 이펙트 같은 뷰 반응은 Bind가 더한다
        public void Attach(EnemyModel enemyModel, EnemyProjectile projectilePrefab)
        {
            _enemyModel = enemyModel;
            _projectilePrefab = projectilePrefab;
            _lastHp = enemyModel.Hp;
            _previousPosition = transform.position;
            _lastPosition = transform.position;
            _enemyModel.ProjectileFired += OnProjectileFired;
            _enemyModel.Attacked += OnAttacked;
            _enemyModel.SpawnRequested += OnSpawnRequested;
            _enemyModel.DeathExplosionRequested += OnDeathExplosionRequested;
        }

        public void Bind(EnemyModel enemyModel,
            EnemyProjectile projectilePrefab,
            ISubscriber<EnemyHpChanged> hpChanged,
            ISubscriber<EnemyDied> died)
        {
            Attach(enemyModel, projectilePrefab);

            // 프리팹에 없으면 붙인다. 프리팹을 고치지 않아도 모든 적(스프라이트가 여러 조각인 리그 포함)이 피격 플래시를 가진다
            _hitFlash = GetComponent<HitFlash>();
            if (_hitFlash == null)
            {
                _hitFlash = gameObject.AddComponent<HitFlash>();
            }

            var statuses = GetComponent<EnemyStatusEffects>();
            if (statuses == null) { statuses = gameObject.AddComponent<EnemyStatusEffects>(); }
            statuses.Bind(enemyModel);

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

            if (_hasMoving && !_enemyModel.IsDead)
            {
                _animator.SetBool(MovingHash, (transform.position - _previousPosition).sqrMagnitude > 0f);
            }

            _previousPosition = transform.position;
            _lastPosition = transform.position;
        }

        public int Id => _enemyModel.Id;
        public EnemyModel Model => _enemyModel;

        // 위치의 단일 출처는 Transform. 파괴된 뒤에도 남은 참조(스킬 효과 등)가 읽을 수 있게 마지막 위치를 돌려준다
        public Vector2 Position => _destroyed ? _lastPosition : (Vector2)transform.position;

        // 분열·소환 요청. 위치는 이 적의 현재 위치를 기준으로 한 절대 좌표다
        public event Action<EnemySpawnRequest> SpawnRequested;

        private void OnSpawnRequested(int monsterId, Vector2 offset)
        {
            SpawnRequested?.Invoke(new EnemySpawnRequest(monsterId, Position + offset));
        }

        private void OnDeathExplosionRequested(Action<Vector2> explosion) => explosion(Position);

        // EnemySpawner가 매 프레임 부른다: 사거리 안이면 공격, 밖이면 벽 쪽으로 이동, 그리고 상태이상·패시브 시간 진행
        public void Tick(float deltaTime, Wall wall, EnemyProjectileSystem projectiles)
        {
            if (IsDead)
            {
                return;
            }

            if (IsInAttackRange(wall))
            {
                Attack(deltaTime, wall, projectiles);
            }
            else
            {
                Move(deltaTime);
            }

            _enemyModel.TickStatus(deltaTime);
            _enemyModel.TickPassives(deltaTime);
        }

        public bool IsInAttackRange(Wall wall) => _enemyModel.IsInAttackRange(Position, wall);

        public void Attack(float deltaTime, Wall wall, EnemyProjectileSystem projectiles)
            => _enemyModel.Attack(deltaTime, Position, wall, projectiles);

        // 아래쪽(벽 방향)으로 모델이 정한 속도만큼 이동
        public void Move(float deltaTime)
        {
            transform.position += Vector3.down * (_enemyModel.MoveSpeed * deltaTime);
        }

        public void TickStatus(float deltaTime) => _enemyModel.TickStatus(deltaTime);

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

            // 체력이 늘었다면(회복) 피격 연출은 없다
            bool damaged = message.Current < _lastHp;
            _lastHp = message.Current;
            if (!damaged)
            {
                return;
            }

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
            _lastPosition = transform.position;
            _destroyed = true;
            if (_enemyModel != null)
            {
                _enemyModel.ProjectileFired -= OnProjectileFired;
                _enemyModel.Attacked -= OnAttacked;
                _enemyModel.SpawnRequested -= OnSpawnRequested;
                _enemyModel.DeathExplosionRequested -= OnDeathExplosionRequested;
            }

            _subscriptions?.Dispose();
        }

        public bool IsDead => _enemyModel == null || _enemyModel.IsDead;

        public void TakeDamage(int damage)
        {
            _enemyModel.TakeDamage(damage);
        }

        // 속성과 시전 형태를 가진 피해. 모델의 피해 파이프라인이 속성·시전 형태 저항을 계산한다
        public void TakeDamage(DamageInfo info)
        {
            _enemyModel.TakeDamage(info);
        }

        // 투사체 차단 몬스터에 맞은 투사체는 관통하지 못한다
        public bool BlocksPierce => _enemyModel != null && _enemyModel.BlocksPierce;

        public void ApplyParalysis(float duration) => _enemyModel?.ApplyParalysis(duration);
        public void ApplyFreeze(float duration) => _enemyModel.ApplyFreeze(duration);
        public void ApplyFrostbite(float damagePerSecond) => _enemyModel.ApplyFrostbite(damagePerSecond);
        public void ApplyStun(float duration) => _enemyModel.ApplyStun(duration);
        public void ApplySlow(float ratio, float duration) => _enemyModel.ApplySlow(ratio, duration);
        public void ApplyVulnerability(float ratio, float duration) => _enemyModel.ApplyVulnerability(ratio, duration);
        public void SetAreaSlow(object source, float ratio) => _enemyModel.SetAreaSlow(source, ratio);
        public void RemoveAreaSlow(object source) => _enemyModel.RemoveAreaSlow(source);
        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
            => _enemyModel.ApplyBurn(damagePerSecond, duration, maxHpRatio, onDeath);

        // 모델이 저항 등을 반영해 정한 거리만큼 실제로 밀려난다
        public void ApplyKnockback(Vector2 direction, float distance)
        {
            transform.position += (Vector3)_enemyModel.ResolveKnockback(direction, distance);
        }
    }
}
