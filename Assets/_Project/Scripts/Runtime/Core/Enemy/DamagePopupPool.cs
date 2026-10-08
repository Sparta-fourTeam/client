using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>데미지 숫자를 만들고 재사용한다. 동시에 보이는 숫자는 MaxActive개까지이고, 넘으면 가장 오래된 숫자를 다시 써서 새 숫자를 보인다.
    /// 스테이지 씬 안에 만들어지므로 스테이지가 끝나 씬이 내려가면 같이 사라진다.
    /// 숫자는 적의 자식이 아니라서 적이 죽거나 사라져도 끝까지 보인다.
    /// 숫자 모양은 Resources/UI/DamagePopup 프리팹이 정한다</summary>
    public sealed class DamagePopupPool : MonoBehaviour
    {
        public const int MaxActive = 40;
        private const string PrefabPath = "UI/DamagePopup";

        private static DamagePopupPool s_instance;
        private static bool s_warned;

        private readonly List<DamagePopup> _active = new(); // 오래된 순서
        private readonly Stack<DamagePopup> _free = new();
        private DamagePopup _prefab;

        public int ActiveCount => _active.Count;
        public int FreeCount => _free.Count;

        /// <summary>amount만큼의 피해를 position에 띄운다. scene은 풀을 둘 씬(보통 피해를 받은 적이 있는 씬)이다</summary>
        public static void Show(int amount, Vector3 position, Scene scene)
        {
            if (amount <= 0)
            {
                return;
            }

            var pool = Get(scene);
            if (pool != null)
            {
                pool.Spawn(amount, position);
            }
        }

        /// <summary>풀을 없앤다. 씬이 내려가면 알아서 사라지지만 씬을 그대로 둔 채 정리해야 할 때(테스트 등) 쓴다</summary>
        public static void Clear()
        {
            if (s_instance != null)
            {
                DestroyImmediate(s_instance.gameObject);
            }

            s_instance = null;
        }

        private static DamagePopupPool Get(Scene scene)
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            var prefab = Resources.Load<DamagePopup>(PrefabPath);
            if (prefab == null)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Debug.LogWarning($"[DamagePopupPool] Resources/{PrefabPath} 프리팹을 찾지 못해 데미지 숫자를 표시하지 않는다");
                }

                return null;
            }

            var go = new GameObject("DamagePopups");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            s_instance = go.AddComponent<DamagePopupPool>();
            s_instance._prefab = prefab;
            return s_instance;
        }

        public void Spawn(int amount, Vector3 position)
        {
            DamagePopup popup;
            if (_active.Count >= MaxActive)
            {
                // 가장 오래된 숫자를 새 숫자로 바꿔 쓴다
                popup = _active[0];
                _active.RemoveAt(0);
            }
            else if (_free.Count > 0)
            {
                popup = _free.Pop();
            }
            else
            {
                popup = Instantiate(_prefab, transform);
            }

            popup.Show(amount, position, Release);
            _active.Add(popup);
        }

        /// <summary>다 보인 숫자를 끄고 풀로 돌려보낸다</summary>
        private void Release(DamagePopup popup)
        {
            if (!_active.Remove(popup))
            {
                return;
            }

            popup.gameObject.SetActive(false);
            _free.Push(popup);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
