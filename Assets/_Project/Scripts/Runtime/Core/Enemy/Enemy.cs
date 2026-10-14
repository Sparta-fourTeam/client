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
    public class Enemy : MonoBehaviour, IEnemyTarget, IFreezableTarget, IKnockbackTarget, IPullTarget, IFrostbiteTarget,
        IParalyzableTarget, IFreezeMaxHpDotTarget, IBurnableTarget, IAreaSlowTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget
    {
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private const float DefaultDeathDuration = 0.6f;
        private const float DamagePopupJitter = 0.15f; // 연속으로 맞아도 숫자가 한 자리에 겹쳐 쌓이지 않도록 가로로 살짝 흩는다

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
        private bool _deathPlayed;
        private static GameObject s_deathEffect;

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
            _deathPlayed = false;
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

            var hpBar = GetComponent<EnemyHpBar>();
            if (hpBar == null) { hpBar = gameObject.AddComponent<EnemyHpBar>(); }
            hpBar.Bind(enemyModel);

            // 그림자를 먼저 깔고, 앞뒤 정렬은 그림자를 뺀 발끝으로 잰다
            if (!TryGetComponent<EnemyShadow>(out var shadow)) { shadow = gameObject.AddComponent<EnemyShadow>(); }
            shadow.Bind();

            var depthSort = GetComponent<EnemyDepthSort>();
            if (depthSort == null) { depthSort = gameObject.AddComponent<EnemyDepthSort>(); }
            depthSort.Bind();

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

        private void OnSpawnRequested(int monsterId, Vector2 offset, bool isSummon)
        {
            SpawnRequested?.Invoke(new EnemySpawnRequest(monsterId, Position + offset, isSummon));
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
            int damage = _lastHp - message.Current;
            _lastHp = message.Current;
            if (damage <= 0)
            {
                return;
            }

            bool impact = message.Damage.Amount == 0 || message.Damage.IsImpact &&
                message.Damage.CastType != CastType.Area && message.Damage.CastType != CastType.Beam;
            _hitFlash.Flash(impact ? 1f : .22f);

            // 실제로 깎인 체력(남은 체력을 넘는 과잉 피해는 빠진 값)을 머리 위에 띄운다
            float jitter = UnityEngine.Random.Range(-DamagePopupJitter, DamagePopupJitter);
            float top = VisualBounds.TopY(transform);
            var center = new Vector3(Position.x, (top + VisualBounds.BottomY(transform, transform.position.y)) * 0.5f, transform.position.z);
            DamagePopupPool.Show(damage, new Vector3(Position.x + jitter, top, transform.position.z), gameObject.scene,
                DamagePopupKinds.Of(message.Damage), center);

            if (impact && _hasHit && message.Current > 0)
            {
                _animator.SetTrigger(HitHash);
            }
        }

        private void OnDied(EnemyDied message)
        {
            if (message.EnemyId != _enemyModel.Id || _deathPlayed)
            {
                return;
            }

            _deathPlayed = true;
            _hitFlash?.Restore();
            GetComponent<EnemyStatusEffects>()?.Clear();
            if (s_deathEffect == null) { s_deathEffect = Resources.Load<GameObject>("VFX/EnemyDeath"); }
            if (s_deathEffect != null)
            {
                var body = _animator != null ? _animator.transform : transform;
                var bounds = new Bounds(Position, Vector3.one * .6f);
                var renderers = body.GetComponentsInChildren<SpriteRenderer>();
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
                }
                var effect = Instantiate(s_deathEffect, bounds.center, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
                effect.transform.localScale = Vector3.one * Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.y), .4f, 2f);
                // 새 분열체와 다른 적을 가리지 않도록 죽은 적의 바로 뒤에 그린다.
                var bodySorting = GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
                var effectSorting = effect.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (bodySorting != null && effectSorting != null)
                {
                    effectSorting.sortingLayerID = bodySorting.sortingLayerID;
                    effectSorting.sortingOrder = bodySorting.sortingOrder - 1;
                }
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
            float deathSeconds = GetClipLength("_Die", DefaultDeathDuration);
            if (TryGetComponent<EnemyShadow>(out var shadow)) { shadow.FadeOut(deathSeconds); }
            Destroy(gameObject, deathSeconds);
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

        public int CurrentHp => _enemyModel == null ? 0 : _enemyModel.Hp;

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
        public void ApplyFreezeMaxHpDot(float maxHpRatio) => _enemyModel.ApplyFreezeMaxHpDot(maxHpRatio);
        public void ApplyStun(float duration) => _enemyModel.ApplyStun(duration);
        public void ApplySlow(float ratio, float duration) => _enemyModel.ApplySlow(ratio, duration);
        public void ApplyVulnerability(float ratio, float duration) => _enemyModel.ApplyVulnerability(ratio, duration);
        public void SetAreaSlow(object source, float ratio) => _enemyModel.SetAreaSlow(source, ratio);
        public void RemoveAreaSlow(object source) => _enemyModel.RemoveAreaSlow(source);
        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
            => _enemyModel.ApplyBurn(damagePerSecond, duration, maxHpRatio, onDeath);

        // 모델이 저항 등을 반영해 정한 거리만큼 실제로 밀려난다 (항상 위로)
        public void ApplyKnockback(Vector2 direction, float distance)
        {
            transform.position += (Vector3)_enemyModel.ResolveKnockback(direction, distance);
        }

        // 장판 중심 쪽으로 끌려간다 (좌우 포함)
        public void ApplyPull(Vector2 direction, float distance)
        {
            transform.position += (Vector3)_enemyModel.ResolvePull(direction, distance);
        }
    }
}
