using UnityEngine;
using Sirenix.OdinInspector;

public class SlimeSizeController : MonoBehaviour
{
    public enum SizeState { Tiny, Small, Normal, Big, Huge }

    [System.Serializable]
    public class SizeTier
    {
        public string label = "Tier";
        public float minAmount = 1f;
        public float maxAmount = 1.8f;

        public float bodyScale = 1f;
        public float launchForceMultiplier = 1f;
        public float gravityMultiplier = 1f; // 힘키우니까 너무멀리감 중력으로조절
        public float windInfluence = 1f;
        public float straightShotGravityDelayMultiplier = 1f;
        public float straightShotSquashMultiplier = 1f;
        public float straightShotRecoverMultiplier = 1f;
    }

    [Header("References")]
    [SerializeField] private Transform sizeRoot; // 여기스케일넣으니까 애니랑싸움 부모에넣음
    [SerializeField] private Transform faceRoot;
    [SerializeField] private bool keepFaceSizeConstant = true;

    [Header("Slime Amount")]
    [SerializeField] private float slimeAmount = 3f;
    [SerializeField] private float minSlimeAmount = 1f;
    [SerializeField] private float maxSlimeAmount = 5f;

    [Header("먹기")]
    [SerializeField] private int defaultTiersPerFood = 1;

    [Header("Size Tiers (Odin 표에서 1~5칸 조정)")]
    [TableList(ShowIndexLabels = true)]
    [SerializeField]
    private SizeTier[] tiers = DefaultTiers();

    [Header("Debug")]
    [SerializeField] private bool inGameSizeDebug = true;
    [ShowInInspector, ReadOnly] private SizeState currentState;
    [ShowInInspector, ReadOnly] private float size01;
    [ShowInInspector, ReadOnly] private int currentTierIndex;

    private Vector3 _baseSizeRootScale;
    private Vector3 _baseFaceLocalScale;
    private Vector3 _basePivotScale = Vector3.one;
    private Transform _sizePivot;
    private SizeTier _current;
    private SlimeStats _stats;

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

    public void SetToNormal()
    {
        SetState(SizeState.Normal);
    }

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

    // root본 건드리면 애니깨짐. 부모에넣음
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

    private void ApplySize()
    {
        if (_sizePivot == null)
        {
            ResolveSizeRoot();
            SaveBaseValues();
            EnsureSizePivot();
        }

        size01 = Mathf.InverseLerp(minSlimeAmount, maxSlimeAmount, slimeAmount);

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

    private int FindTierIndex(float amount)
    {
        if (tiers == null || tiers.Length == 0) return 0;

        for (int i = 0; i < tiers.Length; i++)
        {
            bool isLast = (i == tiers.Length - 1);
            if (amount >= tiers[i].minAmount && (amount < tiers[i].maxAmount || (isLast && amount <= tiers[i].maxAmount)))
                return i;
        }
        return amount < tiers[0].minAmount ? 0 : tiers.Length - 1;
    }

    private static SizeTier[] DefaultTiers()
    {
        return new SizeTier[]
        {
            // 사거리만 건드림. 속도바꾸니까 각도도이상해짐
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
