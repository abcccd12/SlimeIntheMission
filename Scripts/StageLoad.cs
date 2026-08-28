using System.Collections.Generic;
using UnityEngine;

public class StageLoad : MonoBehaviour
{
    [Header("카드 덱 (맵 청크 프리팹들)")]
    [SerializeField] private List<GameObject> chunkPrefabs = new List<GameObject>();

    [Header("배치 설정")]
    [SerializeField] private float chunkHeight = 360f; // 청크 y간격
    [SerializeField] private int visibleCount = 4;
    [SerializeField] private Vector3 startPosition = Vector3.zero;

    [Header("재배치 트리거")]
    [SerializeField] private Transform slime;
    [SerializeField] private float recycleDistance = 720f; // 이만큼 올라가면 맨아래 재사용

    [Header("진행 / 뽑기")]
    [SerializeField] private int totalChunks = 0; // 0이면 무한
    [SerializeField] private bool drawRandom = true;

    private readonly List<GameObject> _pool   = new List<GameObject>();
    private readonly List<GameObject> _active = new List<GameObject>();
    private readonly List<GameObject> _avail  = new List<GameObject>();


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


    // 루트 setactive하면 꺼둔자식도 같이켜짐  sav해둠 왜이래
    private readonly Dictionary<GameObject, List<GameObject>> _inactiveChildren
        = new Dictionary<GameObject, List<GameObject>>();
    private float _topY;
    private int _spawnedCount;
    private int _seqIndex;
    private GameObject _lastDrawn;
    private bool _mainChunksStarted;
    
    [SerializeField] private GameObject cameraTarget;
    [SerializeField] private GameObject startpos;
    [SerializeField] private SlimeLaunchController Slime;
    [SerializeField] private GameObject Tutorialobject;

    private void Awake()
    {
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
            return; // 튜토중에 맵스폰되서 막음

        BeginMainChunks();
    }

    public void BeginMainChunks()
    {
        if (_mainChunksStarted) return;
        _mainChunksStarted = true;

        if (Tutorialobject != null)
            Tutorialobject.SetActive(false);

        if (Slime != null && startpos != null)
            Slime.transform.position = startpos.transform.position;
        if (cameraTarget != null)
            cameraTarget.transform.position = new Vector3(0.49000001f, 14.1948071f, 0f); // 이거 반올림하니까 위치어긋남 그냥복붙

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
        GameObject bottom = _active[0];
        _active.RemoveAt(0);
        bottom.SetActive(false);

        if (totalChunks > 0 && _spawnedCount >= totalChunks)
        {
            if (_active.Count == 0)
                OnAllChunksCleared();
            return;
        }

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
            pick = (pick + 1) % _obstacleAvail.Count; // 같은패턴 두번나와서 +1함
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

    private GameObject DrawCard()
    {
        _avail.Clear();
        foreach (var c in _pool)
            if (!_active.Contains(c)) _avail.Add(c);

        if (_avail.Count == 0) return null;

        if (!drawRandom)
            return _avail[_seqIndex++ % _avail.Count];

        int pick = Random.Range(0, _avail.Count);
        if (_avail.Count > 1 && _avail[pick] == _lastDrawn)
            pick = (pick + 1) % _avail.Count;
        return _avail[pick];
    }

    private void OnAllChunksCleared()
    {
        Debug.Log("[StageLoad] 모든 청크 통과 - 스테이지 클리어");
        // TODO 클리어연결 아직안함
    }
}
