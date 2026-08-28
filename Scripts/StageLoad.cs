using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "카드 덱" 방식의 세로 무한 맵 청크 매니저.
/// - 프리팹마다 인스턴스를 '딱 1개씩' 미리 생성해 재사용(메모리 효율).
/// - 화면엔 visibleCount개만 켜두고, 슬라임이 위로 올라가면
///   맨 아래 청크를 끄고(덱 반환) 새 카드를 맨 위에 붙인다.
/// </summary>
public class StageLoad : MonoBehaviour
{
    [Header("카드 덱 (맵 청크 프리팹들)")]
    [SerializeField] private List<GameObject> chunkPrefabs = new List<GameObject>();

    [Header("배치 설정")]
    [Tooltip("청크 하나의 Y 높이(간격). 예: 360")]
    [SerializeField] private float chunkHeight = 360f;
    [Tooltip("동시에 켜둘 청크 수(창 크기). 예: 4")]
    [SerializeField] private int visibleCount = 4;
    [Tooltip("맨 아래(첫) 청크가 놓일 위치")]
    [SerializeField] private Vector3 startPosition = Vector3.zero;

    [Header("재배치 트리거")]
    [Tooltip("기준이 되는 슬라임")]
    [SerializeField] private Transform slime;
    [Tooltip("슬라임이 '맨 아래 청크'보다 이만큼 위로 올라가면 재배치")]
    [SerializeField] private float recycleDistance = 720f;

    [Header("진행 / 뽑기")]
    [Tooltip("총 스폰할 청크 수. 0 이하이면 무한 반복.")]
    [SerializeField] private int totalChunks = 0;
    [Tooltip("true=무작위(직전 것 제외), false=덱 순서대로")]
    [SerializeField] private bool drawRandom = true;

    // ── 내부 상태 ──────────────────────────────
    private readonly List<GameObject> _pool   = new List<GameObject>(); // 프리팹당 1개 (전체 덱)
    private readonly List<GameObject> _active = new List<GameObject>(); // 켜진 청크 (아래→위 순서)
    private readonly List<GameObject> _avail  = new List<GameObject>(); // 뽑기 후보(재사용 버퍼, GC 방지)


    [Header("장애물 패턴 덱")]
    [SerializeField] private List<GameObject> obstacles = new List<GameObject>();
    [SerializeField] private int obstacleStartCount = 4;
    [SerializeField] private int obstacleAdvanceCount = 4;

    private readonly List<GameObject> _obstaclePool = new List<GameObject>();
    private readonly List<GameObject> _obstacleActive = new List<GameObject>();
    private readonly List<GameObject> _obstacleAvail = new List<GameObject>();
    private GameObject _lastDrawnObstacle;
    private int _obstacleSeqIndex;
    private float _obstacleTopY;
    private int _obstacleBand = -1;
    private SlimeSizeController _size;


    // 프리팹에서 꺼져 있던 자식. 루트를 SetActive(true)해도 다시 켜지지 않게 복구한다.
    private readonly Dictionary<GameObject, List<GameObject>> _inactiveChildren
        = new Dictionary<GameObject, List<GameObject>>();
    private float _topY;            // 현재 맨 위 청크의 y
    private int _spawnedCount;      // 지금까지 스폰한 총 개수
    private int _seqIndex;          // 순서대로 뽑기 커서
    private GameObject _lastDrawn;  // 직전에 뽑은 카드(연속 방지)
    private bool _mainChunksStarted;
    
    [SerializeField] private GameObject cameraTarget;
    [SerializeField] private GameObject startpos;
    [SerializeField] private SlimeLaunchController Slime;
    [SerializeField] private GameObject Tutorialobject;

    private void Awake()
    {
        // 프리팹마다 인스턴스 1개씩만 생성 → 이후엔 SetActive로 재사용 (메모리 1회 할당)
        foreach (var prefab in chunkPrefabs)
        {
            if (prefab == null) continue;
            GameObject inst = Instantiate(prefab);
            CacheInactiveChildren(inst);
            inst.SetActive(false);
            _pool.Add(inst);
        }

        int obstacleKinds = 0;
        foreach (var prefab in obstacles)
            if (prefab != null) obstacleKinds++;
        int need = obstacleStartCount + obstacleAdvanceCount;
        int copies = obstacleKinds <= 0 ? 0
            : Mathf.Max(1, Mathf.CeilToInt((float)need / obstacleKinds));

        foreach (var prefab in obstacles)
        {
            if (prefab == null) continue;
            for (int i = 0; i < copies; i++)
            {
                GameObject inst = Instantiate(prefab);
                CacheInactiveChildren(inst);
                inst.SetActive(false);
                _obstaclePool.Add(inst);
            }
        }

        if (slime != null)
        {
            _size = slime.GetComponent<SlimeSizeController>();
            if (_size == null) _size = slime.GetComponentInChildren<SlimeSizeController>();
        }
        if (_size == null && Slime != null)
            _size = Slime.GetComponent<SlimeSizeController>();

        if (_pool.Count <= visibleCount)
            Debug.LogWarning($"[StageLoad] 프리팹 종류({_pool.Count})가 visibleCount({visibleCount}) 이하라 여분이 없어 같은 맵이 반복됩니다. 종류를 더 늘리세요.");
    }

    private void Start()
    {
        bool tutoDone = TutorialSequence.IsDone();
        Debug.Log($"tutoDone={tutoDone}");
        if (!tutoDone)
            return; // 튜토 중엔 청크 스폰 안 함. 끝나면 TutorialSequence가 BeginMainChunks 호출

        BeginMainChunks();
    }

    /// <summary>본게임 시작: 튜토 끄고 청크 창을 채운다. 두 번 불려도 한 번만 실행.</summary>
    public void BeginMainChunks()
    {
        if (_mainChunksStarted) return;
        _mainChunksStarted = true;

        if (Tutorialobject != null)
            Tutorialobject.SetActive(false);

        if (Slime != null && startpos != null)
            Slime.transform.position = startpos.transform.position;
        if (cameraTarget != null)
            cameraTarget.transform.position = new Vector3(0.49000001f, 14.1948071f, 0f);

        _topY = startPosition.y - chunkHeight;
        for (int i = 0; i < visibleCount; i++)
            SpawnNextOnTop();

        _obstacleTopY = startPosition.y - chunkHeight;
        for (int i = 0; i < obstacleStartCount; i++)
            SpawnOneObstacle();
        _obstacleBand = 0;
    }

    private void Update()
    {
        if (slime == null) return;

        if (_size != null)
        {
            var size = _size.CurrentState;
            for (int i = 0; i < _obstacleActive.Count; i++)
            {
                Obstacle ob = _obstacleActive[i].GetComponent<Obstacle>();
                if (ob != null) ob.RefreshFood(slime.position.y, size);
            }
        }

        if (_obstaclePool.Count > 0)
        {
            int band = Mathf.FloorToInt((slime.position.y - startPosition.y) / chunkHeight);
            if (band < 0) band = 0;
            if (band > _obstacleBand)
            {
                _obstacleBand = band;
                RecycleFarObstacles();
                for (int i = 0; i < obstacleAdvanceCount; i++)
                    SpawnOneObstacle();
            }
        }

        if (_active.Count == 0) return;

        GameObject bottom = _active[0];
        if (slime.position.y > bottom.transform.position.y + recycleDistance)
            RecycleBottom();
    }

    private void RecycleBottom()
    {
        // 1) 맨 아래 청크 끄고 덱으로 반환
        GameObject bottom = _active[0];
        _active.RemoveAt(0);
        bottom.SetActive(false);

        // 2) 지정 횟수를 다 뽑았으면(무한 아님) 더 안 붙임
        if (totalChunks > 0 && _spawnedCount >= totalChunks)
        {
            if (_active.Count == 0)
                OnAllChunksCleared(); // 마지막 청크까지 지나감 → 종료 지점
            return;
        }

        // 3) 새 카드 맨 위에 붙이기
        SpawnNextOnTop();
    }

    private void SpawnNextOnTop()
    {
        GameObject card = DrawCard();
        if (card == null) return;

        _topY += chunkHeight;
        Vector3 pos = startPosition;
        pos.y = _topY;
        card.transform.position = pos;
        card.SetActive(true);
        RestoreInactiveChildren(card);

        _active.Add(card);
        _lastDrawn = card;
        _spawnedCount++;
    }

    private void SpawnOneObstacle()
    {
        RecycleFarObstacles();
        GameObject card = DrawObstacle();
        if (card == null) return;

        _obstacleTopY += chunkHeight;
        Vector3 pos = startPosition;
        pos.y = _obstacleTopY;
        card.transform.position = pos;
        card.SetActive(true);
        RestoreInactiveChildren(card);

        Obstacle ob = card.GetComponent<Obstacle>();
        if (ob != null)
        {
            var size = _size != null ? _size.CurrentState : SlimeSizeController.SizeState.Normal;
            ob.Apply(pos.y, size);
        }

        _obstacleActive.Add(card);
        _lastDrawnObstacle = card;
    }

    private void RecycleFarObstacles()
    {
        float keepBelow = slime != null ? slime.position.y - chunkHeight * obstacleStartCount : float.NegativeInfinity;
        while (_obstacleActive.Count > 0 && _obstacleActive[0].transform.position.y < keepBelow)
        {
            GameObject bottom = _obstacleActive[0];
            _obstacleActive.RemoveAt(0);
            bottom.SetActive(false);
        }
    }

    private GameObject DrawObstacle()
    {
        _obstacleAvail.Clear();
        foreach (var c in _obstaclePool)
            if (!_obstacleActive.Contains(c)) _obstacleAvail.Add(c);

        if (_obstacleAvail.Count == 0) return null;

        if (!drawRandom)
            return _obstacleAvail[_obstacleSeqIndex++ % _obstacleAvail.Count];

        int pick = Random.Range(0, _obstacleAvail.Count);
        if (_obstacleAvail.Count > 1 && _obstacleAvail[pick] == _lastDrawnObstacle)
            pick = (pick + 1) % _obstacleAvail.Count;
        return _obstacleAvail[pick];
    }

    void CacheInactiveChildren(GameObject root)
    {
        var list = new List<GameObject>();
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i].gameObject;
            if (go == root) continue;
            if (!go.activeSelf)
                list.Add(go);
        }
        _inactiveChildren[root] = list;
    }

    void RestoreInactiveChildren(GameObject root)
    {
        if (!_inactiveChildren.TryGetValue(root, out var list)) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                list[i].SetActive(false);
        }
    }

    /// <summary>대기 중(꺼진) 인스턴스 중 하나를 뽑는다.</summary>
    private GameObject DrawCard()
    {
        _avail.Clear();
        foreach (var c in _pool)
            if (!_active.Contains(c)) _avail.Add(c);

        if (_avail.Count == 0) return null;

        if (!drawRandom)  // 순서대로
            return _avail[_seqIndex++ % _avail.Count];

        // 무작위 (직전 것과 같으면 한 칸 밀어 연속 방지)
        int pick = Random.Range(0, _avail.Count);
        if (_avail.Count > 1 && _avail[pick] == _lastDrawn)
            pick = (pick + 1) % _avail.Count;
        return _avail[pick];
    }

    /// <summary>지정 횟수를 다 돌고 마지막 청크까지 지나갔을 때. (클리어 연출 연결)</summary>
    private void OnAllChunksCleared()
    {
        Debug.Log("[StageLoad] 모든 청크 통과 - 스테이지 클리어");
        // TODO: 여기서 StageManager.Instance.GotoStage(...) 등 연결
    }
}
