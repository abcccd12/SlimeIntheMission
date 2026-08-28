using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 슬라임 크기를 '5단계(Tiny/Small/Normal/Big/Huge)'로 관리하고,
/// 각 단계별 배율값(발사 힘/중력지연/눌림/복귀)을 제공하는 스크립트.
///
/// 구조:
/// - slimeAmount(1.0~5.0)가 어느 단계 구간에 드는지로 현재 단계를 정한다.
/// - 각 단계는 SizeTier로 정의되고, Odin 표에서 1~5칸으로 세부조정한다.
/// - SlimeLaunchController / SlimeVisualController가 아래 public 프로퍼티를 읽어 쓴다.
///
/// 주의:
/// - 얼굴 '위치' 자동 이동은 제거했다. (부모 회전/음수스케일 때문에 z만 만져도 이상하게 움직이던 문제 회피)
///   얼굴은 하이어라키에서 직접 배치하고, 여기서는 '크기 유지'만 선택적으로 한다.
/// </summary>
public class SlimeSizeController : MonoBehaviour
{
    public enum SizeState { Tiny, Small, Normal, Big, Huge }

    /// <summary>단계 하나의 설정. Odin 표의 한 칸(1~5)에 해당.</summary>
    [System.Serializable]
    public class SizeTier
    {
        [Tooltip("표시용 이름")] public string label = "Tier";
        [Tooltip("이 단계의 slimeAmount 하한(포함)")] public float minAmount = 1f;
        [Tooltip("이 단계의 slimeAmount 상한(미만; 마지막 단계는 이하)")] public float maxAmount = 1.8f;

        [Tooltip("이 단계일 때 몸 크기 배율")] public float bodyScale = 1f;
        [Tooltip("발사 속도 배율. 1이면 당김 속도와 각도를 그대로 유지한다.")] public float launchForceMultiplier = 1f;
        [Tooltip("비행 중 중력 배율. 작을수록 같은 속도로 더 멀리 간다. 사거리 ∝ 1/이 값.")] public float gravityMultiplier = 1f;
        [Tooltip("바람 가속도 배율. 작을수록 바람에 덜 밀린다.")] public float windInfluence = 1f;
        [Tooltip("직선발사 중력지연 배율 (커질수록 중력이 늦게 켜짐)")] public float straightShotGravityDelayMultiplier = 1f;
        [Tooltip("직선발사 눌림 세기 배율 (SlimeVisualController가 squash에 곱함)")] public float straightShotSquashMultiplier = 1f;
        [Tooltip("직선발사 눌림 복귀시간 배율 (SlimeVisualController가 recover에 곱함)")] public float straightShotRecoverMultiplier = 1f;
    }

    [Header("References")]
    [Tooltip("스킨 Root 본(애니 스케일 클립이 붙는 그 본). 크기는 이 본이 아니라 부모 피벗에 넣는다.")]
    [SerializeField] private Transform sizeRoot;
    [Tooltip("눈/입이 든 얼굴 루트(선택). keepFaceSizeConstant가 켜지면 크기만 유지시킨다.")]
    [SerializeField] private Transform faceRoot;
    [Tooltip("몸이 커져도 얼굴 자체 크기는 유지할지. (얼굴 위치는 건드리지 않음)")]
    [SerializeField] private bool keepFaceSizeConstant = true;

    [Header("Slime Amount")]
    [Tooltip("현재 슬라임 양. 이 값이 커질수록 몸도 커진다. 기본 3 = Normal.")]
    [SerializeField] private float slimeAmount = 3f;
    [SerializeField] private float minSlimeAmount = 1f;
    [SerializeField] private float maxSlimeAmount = 5f;

    [Header("먹기")]
    [Tooltip("푸드에 Food 스크립트가 없으면 한 번에 이 단계만큼 커진다.")]
    [SerializeField] private int defaultTiersPerFood = 1;

    // [Odin] 단계별 값을 표(1~5칸)로 조정. 기본값은 아래에서 세팅해두고, Inspector에서 세부조정하면 된다.
    [Header("Size Tiers (Odin 표에서 1~5칸 조정)")]
    [TableList(ShowIndexLabels = true)]
    [SerializeField]
    private SizeTier[] tiers = DefaultTiers();

    // --- 디버그 ---
    [Header("Debug")]
    [Tooltip("플레이 중 화면 왼쪽 위에 단계 버튼을 띄운다. 숫자키 1~5로도 바꾼다.")]
    [SerializeField] private bool inGameSizeDebug = true;
    [ShowInInspector, ReadOnly] private SizeState currentState;
    [ShowInInspector, ReadOnly] private float size01;
    [ShowInInspector, ReadOnly] private int currentTierIndex;

    // --- 내부 ---
    private Vector3 _baseSizeRootScale;
    private Vector3 _baseFaceLocalScale;
    private Vector3 _basePivotScale = Vector3.one;
    private Transform _sizePivot;
    private SizeTier _current;
    private SlimeStats _stats;

    // ======================================================================
    //  public 프로퍼티 (다른 스크립트가 읽어 쓴다)
    // ======================================================================
    public float SlimeAmount => slimeAmount;
    public float Size01 => size01;
    public SizeState CurrentState => currentState;

    public float SizeMultiplier => _current != null ? _current.bodyScale : 1f;
    public float LaunchForceMultiplier => _current != null ? _current.launchForceMultiplier : 1f;
    public float GravityMultiplier => _current != null ? _current.gravityMultiplier : 1f;
    public float WindInfluence => _current != null ? _current.windInfluence : 1f;
    public float StraightShotGravityDelayMultiplier => _current != null ? _current.straightShotGravityDelayMultiplier : 1f;
    public float StraightShotSquashMultiplier => _current != null ? _current.straightShotSquashMultiplier : 1f;
    public float StraightShotRecoverMultiplier => _current != null ? _current.straightShotRecoverMultiplier : 1f;

    private void Awake()
    {
        ResolveSizeRoot();
        SaveBaseValues();
        EnsureSizePivot();
        _stats = GetComponent<SlimeStats>();
        ApplySize();
    }

    /// <summary>시작/리셋용. Normal 구간 하한으로 맞춘다.</summary>
    public void SetToNormal()
    {
        SetState(SizeState.Normal);
    }

    /// <summary>디버그/치트용. 해당 단계 하한 goo로 맞추고 체력도 동기화한다.</summary>
    public void SetState(SizeState state)
    {
        if (tiers == null || tiers.Length == 0) return;

        int i = Mathf.Clamp((int)state, 0, tiers.Length - 1);
        SetGoo(tiers[i].minAmount);
        if (_stats != null)
            _stats.SyncAmountToGoo(slimeAmount);
    }

    private void Update()
    {
        if (!inGameSizeDebug) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetState(SizeState.Tiny);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetState(SizeState.Small);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetState(SizeState.Normal);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SetState(SizeState.Big);
        else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SetState(SizeState.Huge);
    }

    private void OnGUI()
    {
        if (!inGameSizeDebug) return;

        const float w = 88f;
        const float h = 28f;
        float x = 12f;
        float y = 12f;
        GUI.Label(new Rect(x, y, 280f, 22f), $"Size: {currentState}  goo={slimeAmount:0.0}");
        y += 24f;

        string[] labels = { "Tiny", "Small", "Normal", "Big", "Huge" };
        for (int i = 0; i < labels.Length; i++)
        {
            if (GUI.Button(new Rect(x + i * (w + 6f), y, w, h), $"{i + 1} {labels[i]}"))
                SetState((SizeState)i);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.IsChildOf(transform) || other.transform == transform)
            return;
        if (!other.CompareTag("Slime")) return;

        int steps = defaultTiersPerFood;
        Food food = other.GetComponentInParent<Food>();
        if (food != null)
            steps = food.TiersToGrow;

        GrowTiers(steps);
        Destroy(other.gameObject);
    }

    /// <summary>현재 단계에서 n단계 위로. 이미 Huge면 그대로.</summary>
    public void GrowTiers(int steps)
    {
        if (tiers == null || tiers.Length == 0 || steps <= 0) return;

        int from = FindTierIndex(slimeAmount);
        int next = Mathf.Min(from + steps, tiers.Length - 1);
        SetGoo(tiers[next].minAmount);

        if (_stats != null)
            _stats.SyncAmountToGoo(slimeAmount);
    }

    private void SaveBaseValues()
    {
        if (sizeRoot != null) _baseSizeRootScale = sizeRoot.localScale;
        if (faceRoot != null) _baseFaceLocalScale = faceRoot.localScale;
    }

    private void ResolveSizeRoot()
    {
        if (sizeRoot != null) return;

        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null && smr.rootBone != null)
            sizeRoot = smr.rootBone;

        if (sizeRoot == null)
            sizeRoot = transform.Find("Slime/Armature/Root");

        if (sizeRoot == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Root" && t.parent != null && t.parent.name == "Armature")
                {
                    sizeRoot = t;
                    break;
                }
            }
        }

        if (faceRoot == null && sizeRoot != null)
        {
            Transform face = sizeRoot.Find("Face");
            if (face == null)
            {
                foreach (Transform t in sizeRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("Face", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        faceRoot = t;
                        break;
                    }
                }
            }
            else
                faceRoot = face;
        }

        if (sizeRoot == null)
            Debug.LogError("[Size] sizeRoot가 비어 있고 자동 탐색도 실패했습니다. Inspector에 Root 본을 연결하세요.", this);
    }

    /// <summary>
    /// 애니 클립이 키잉하는 Root 본은 건드리지 않고, 그 부모(보통 Armature)에 크기를 넣는다.
    /// 부모 체인을 바꾸지 않아서 maxjumpAbstract 경로(Slime/Armature/Root)가 유지된다.
    /// </summary>
    private void EnsureSizePivot()
    {
        if (sizeRoot == null) return;

        Transform parent = sizeRoot.parent;
        if (parent != null && parent != transform)
        {
            _sizePivot = parent;
        }
        else
        {
            var go = new GameObject("SizePivot");
            _sizePivot = go.transform;
            _sizePivot.SetParent(transform, false);
            _sizePivot.SetPositionAndRotation(sizeRoot.position, sizeRoot.rotation);
            _sizePivot.localScale = Vector3.one;
            sizeRoot.SetParent(_sizePivot, true);
        }

        _basePivotScale = _sizePivot.localScale;
        sizeRoot.localScale = _baseSizeRootScale;
    }

    public void AddGoo(float amount)
    {
        slimeAmount = Mathf.Clamp(slimeAmount + amount, minSlimeAmount, maxSlimeAmount);
        ApplySize();
    }

    public void UseGoo(float amount)
    {
        slimeAmount = Mathf.Clamp(slimeAmount - amount, minSlimeAmount, maxSlimeAmount);
        ApplySize();
    }

    public void SetGoo(float amount)
    {
        slimeAmount = Mathf.Clamp(amount, minSlimeAmount, maxSlimeAmount);
        ApplySize();
    }

    /// <summary>slimeAmount로부터 단계/크기/배율을 다시 계산한다.</summary>
    private void ApplySize()
    {
        if (_sizePivot == null)
        {
            ResolveSizeRoot();
            SaveBaseValues();
            EnsureSizePivot();
        }

        // 0~1 (전체 범위 기준 연속값)
        size01 = Mathf.InverseLerp(minSlimeAmount, maxSlimeAmount, slimeAmount);

        // 현재 단계 찾기
        currentTierIndex = FindTierIndex(slimeAmount);
        _current = (tiers != null && tiers.Length > 0) ? tiers[currentTierIndex] : null;
        currentState = (SizeState)Mathf.Clamp(currentTierIndex, 0, 4);

        if (_sizePivot != null)
            _sizePivot.localScale = _basePivotScale * SizeMultiplier;

        if (faceRoot != null)
        {
            faceRoot.localScale = keepFaceSizeConstant
                ? _baseFaceLocalScale / Mathf.Max(0.0001f, SizeMultiplier)
                : _baseFaceLocalScale;
        }
    }

    /// <summary>slimeAmount가 속하는 단계 index를 찾는다. (범위 밖이면 클램프)</summary>
    private int FindTierIndex(float amount)
    {
        if (tiers == null || tiers.Length == 0) return 0;

        for (int i = 0; i < tiers.Length; i++)
        {
            // 마지막 단계는 상한 '이하'까지 포함해서 끝을 놓치지 않게 한다.
            bool isLast = (i == tiers.Length - 1);
            if (amount >= tiers[i].minAmount && (amount < tiers[i].maxAmount || (isLast && amount <= tiers[i].maxAmount)))
                return i;
        }
        // 범위 밑이면 0, 위면 마지막.
        return amount < tiers[0].minAmount ? 0 : tiers.Length - 1;
    }

    /// <summary>기본 5단계 값. Inspector에서 세부조정하면 그 값이 유지된다.</summary>
    private static SizeTier[] DefaultTiers()
    {
        return new SizeTier[]
        {
            // 속도·각도는 1.0 유지. 사거리만 gravity로 144 / 122 / 100 / 90 / 80%.
            new SizeTier { label = "Tiny",   minAmount = 1.0f, maxAmount = 1.8f, bodyScale = 0.8f, launchForceMultiplier = 1.0f, gravityMultiplier = 0.694f, windInfluence = 1.00f, straightShotGravityDelayMultiplier = 1.0f, straightShotSquashMultiplier = 0.8f, straightShotRecoverMultiplier = 1.0f },
            new SizeTier { label = "Small",  minAmount = 1.8f, maxAmount = 2.6f, bodyScale = 1.0f, launchForceMultiplier = 1.0f, gravityMultiplier = 0.820f, windInfluence = 0.80f, straightShotGravityDelayMultiplier = 1.0f, straightShotSquashMultiplier = 0.9f, straightShotRecoverMultiplier = 1.0f },
            new SizeTier { label = "Normal", minAmount = 2.6f, maxAmount = 3.4f, bodyScale = 1.2f, launchForceMultiplier = 1.0f, gravityMultiplier = 1.000f, windInfluence = 0.60f, straightShotGravityDelayMultiplier = 1.0f, straightShotSquashMultiplier = 1.0f, straightShotRecoverMultiplier = 1.0f },
            new SizeTier { label = "Big",    minAmount = 3.4f, maxAmount = 4.2f, bodyScale = 1.4f, launchForceMultiplier = 1.0f, gravityMultiplier = 1.111f, windInfluence = 0.35f, straightShotGravityDelayMultiplier = 1.0f, straightShotSquashMultiplier = 1.2f, straightShotRecoverMultiplier = 1.1f },
            new SizeTier { label = "Huge",   minAmount = 4.2f, maxAmount = 5.0f, bodyScale = 1.6f, launchForceMultiplier = 1.0f, gravityMultiplier = 1.250f, windInfluence = 0.20f, straightShotGravityDelayMultiplier = 1.0f, straightShotSquashMultiplier = 1.4f, straightShotRecoverMultiplier = 1.2f },
        };
    }

    [ContextMenu("Apply Size")]
    private void DebugApplySize()
    {
        SaveBaseValues();
        ApplySize();
    }
}
