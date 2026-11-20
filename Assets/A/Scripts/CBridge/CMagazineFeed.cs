using System.Reflection;
using MilTrainSimVR.A.Rifle;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CMagazine = MilTrainSimVR.UrbanCombat.RifleMagazine;
using CMagazineWell = MilTrainSimVR.UrbanCombat.MagazineWell;

namespace MilTrainSimVR.A.CBridge
{
    // A 총(K2C1Rifle)이 C 탄창 삽입구(C MagazineWell · C_Magazine)를 쓰게 하는 다리 (11/17 총기 통합 5단계 — Docs/A/1117_총기통합_6_C탄창.md).
    // 탄창은 C 것 한 벌 — 끼우기 · 손으로 빼기 · 떨어뜨리기 · 남은 탄 보이기는 C 삽입구 · C 탄창이 그대로 한다.
    // 같은 총에 C MagazineWell 과 함께 붙이고, K2C1Rifle 의 탄창 삽입구 칸(m_Well)에 이걸 넣는다.
    // 이 폴더는 asmdef 가 없어(Assembly-CSharp) C 코드를 볼 수 있다. 사로 실전 사격 다리(CRangeRifle)도 탄창 놓기 · 시험 끼우기를 여기 것으로 쓴다.
    [RequireComponent(typeof(CMagazineWell))]
    public class CMagazineFeed : MonoBehaviour, IMagazineFeed
    {
        [Tooltip("사로에 놓는 C 탄창 (Assets/C/Prefabs/C_Magazine.prefab)")]
        [SerializeField] CMagazine m_MagazinePrefab;

        CMagazineWell m_Well;

        // 늦게 찾는다 — 사로가 OnEnable 에서 이벤트를 걸 때 이 컴포넌트의 Awake 가 아직일 수 있다
        CMagazineWell CWell => m_Well != null ? m_Well : (m_Well = GetComponent<CMagazineWell>());

        public bool HasMagazine => CWell.HasMagazine;
        public int Rounds => CWell.Rounds;

        // C 삽입구의 끼움 · 뺌을 그대로 넘긴다. 시험 끼우기(SetSeated)는 C 가 알리지 않아서 여기서 알린다
        public event System.Action Inserted;
        public event System.Action Removed;

        void Awake()
        {
            CWell.Inserted += OnInserted;
            CWell.Removed += OnRemoved;
        }

        void OnDestroy()
        {
            if (m_Well != null)
            {
                m_Well.Inserted -= OnInserted;
                m_Well.Removed -= OnRemoved;
            }
        }

        void OnInserted() => Inserted?.Invoke();
        void OnRemoved() => Removed?.Invoke();

        public bool TakeRound() => CWell.TakeRound();
        public void Drop() => CWell.Drop();
        public void Clear() => CWell.Clear();

        public void ResetMagazines(Vector3 position, Quaternion rotation, int rounds) => ResetCMagazines(m_MagazinePrefab, position, rotation, rounds);

        public void TestInsert() => TestInsert(CWell, OnInserted);
        public void TestRemove() => CWell.Drop();

        // 씬의 C 탄창(손에 든 것 · 떨어진 것)을 모두 치우고 그 자리에 새 탄창 하나
        public static void ResetCMagazines(CMagazine prefab, Vector3 position, Quaternion rotation, int rounds)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Range] 사로 탄창(C_Magazine) 프리팹이 비었다");
                return;
            }
            for (int i = CMagazine.All.Count - 1; i >= 0; i--)
            {
                CMagazine old = CMagazine.All[i];
                if (old.Grab.isSelected && old.Grab.interactionManager != null)
                    old.Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)old.Grab);
                old.gameObject.SetActive(false);   // 바로 목록에서 빠지게
                Destroy(old.gameObject);
            }
            CMagazine mag = Instantiate(prefab, position, rotation);
            mag.Rounds = rounds;
        }

        // 시험용 (손 없이) — 놓인 C 탄창 하나를 꽂는다. C 에 공개 끼우기 함수가 없어 끼울 때 불리는 내부 함수(SetSeated)를 리플렉션으로 부른다.
        // SetSeated 는 Inserted 를 알리지 않아서 inserted 로 대신 알린다
        public static void TestInsert(CMagazineWell well, System.Action inserted)
        {
            if (well == null || well.HasMagazine || CMagazine.All.Count == 0)
                return;
            CMagazine mag = CMagazine.All[0];
            int rounds = mag.Rounds;
            mag.gameObject.SetActive(false);
            Destroy(mag.gameObject);
            MethodInfo seat = typeof(CMagazineWell).GetMethod("SetSeated", BindingFlags.Instance | BindingFlags.NonPublic);
            if (seat == null)
            {
                Debug.LogWarning("[Range] C 탄창 시험 끼우기 실패 — MagazineWell.SetSeated 가 없다 (C 코드가 바뀜, CMagazineFeed 맞출 것)");
                return;
            }
            seat.Invoke(well, new object[] { true, rounds });
            inserted?.Invoke();
        }
    }
}
