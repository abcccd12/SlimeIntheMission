using UnityEngine;

/// <summary>
/// 슬라임의 "상태 값"(체력 겸 자원)을 관리하는 스크립트.
///
/// 핵심 개념:
/// - currentSlimeAmount 를 체력이자 발사 자원처럼 쓴다.
/// - 데미지를 받으면 줄고, 회복하면 늘고, 0이 되면 죽는다.
///
/// 설계 규칙:
/// - 값(변수)은 전부 private 으로 숨긴다. (외부가 마음대로 바꾸지 못하게)
/// - 외부는 "읽기"만 프로퍼티로 하고, "바꾸기"는 반드시 함수(TakeDamage 등)로만 한다.
///   => 이렇게 하면 "0 아래로 내려가지 않게" 같은 규칙을 함수 한 곳에서만 지키면 된다.
/// </summary>
public class SlimeStats : MonoBehaviour
{
    [Header("슬라임 양 (체력 겸 자원)")]
    [Tooltip("현재 슬라임 양. 체력이자 발사 자원 역할을 한다.")]
    [SerializeField] private int currentSlimeAmount = 10;

    [Tooltip("가질 수 있는 최대 슬라임 양. 회복해도 이 값을 넘지 않는다.")]
    [SerializeField] private int maxSlimeAmount = 10;

    [Tooltip("발사가 가능한 최소 슬라임 양. 이보다 적으면 발사할 수 없다.")]
    [SerializeField] private int minLaunchAmount = 1;

    [Header("상태 플래그")]
    [Tooltip("무적 상태인지. true면 데미지를 무시한다.")]
    [SerializeField] private bool isInvincible = false;

    [Tooltip("죽었는지. true면 대부분의 동작이 막힌다.")]
    [SerializeField] private bool isDead = false;

    [SerializeField] private SlimeSizeController size;

    // ======================================================================
    //  읽기 전용 프로퍼티 (외부는 값을 '읽기만' 가능, 바꾸는 건 함수로)
    // ======================================================================

    public int CurrentSlimeAmount => currentSlimeAmount;
    public int MaxSlimeAmount => maxSlimeAmount;
    public int MinLaunchAmount => minLaunchAmount;
    public bool IsInvincible => isInvincible;
    public bool IsDead => isDead;

    /// <summary>죽지 않았고, 슬라임 양이 최소 발사량 이상이면 발사 가능.</summary>
    public bool CanLaunch => !isDead && currentSlimeAmount >= minLaunchAmount;

    // ======================================================================
    //  데미지 / 회복 관련 함수
    // ======================================================================

    /// <summary>
    /// 데미지를 받는다. (int 버전)
    /// - 무적이거나 이미 죽었으면 무시.
    /// - amount가 0 이하면 무시. (음수 데미지로 오히려 회복되는 버그 방지)
    /// - 슬라임 양이 0 이하가 되면 Die() 호출.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (isDead || isInvincible) return; // 죽었거나 무적이면 데미지 없음
        if (amount <= 0) return;            // 0 이하 데미지는 무의미하므로 무시

        currentSlimeAmount -= amount;

        // 0 아래로는 내려가지 않게 막는다.
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
        if (currentSlimeAmount <= 0)
            Die();
    }

    private void Start()
    {
        if (size != null)
            size.SetToNormal();
    }

    /// <summary>
    /// 데미지를 받는다. (float 버전 - 오버로드)
    /// WallSurface.Slimedamage 처럼 데미지가 float일 때 사용한다.
    ///
    /// 변환 방식으로 CeilToInt(올림)를 쓴다.
    /// - 이유: "소수점이라도 조금이라도 데미지가 있으면 최소 1은 들어가야 한다"는 요구사항.
    ///   예) 0.3 데미지 → 1, 1.2 데미지 → 2. (RoundToInt였다면 0.3이 0이 되어 무시됨)
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return; // 0 이하 float 데미지는 무시

        int damage = Mathf.CeilToInt(amount); // 올림: 0보다 크면 최소 1 보장
        TakeDamage(damage);                   // 실제 처리는 int 버전에 넘긴다 (규칙 한 곳에 모음)
    }

    /// <summary>
    /// 슬라임 양을 회복한다.
    /// - 죽었으면 회복하지 않는다. (부활은 Revive로만)
    /// - amount 0 이하면 무시.
    /// - 최대치를 넘지 않게 한다.
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentSlimeAmount += amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
    }

    /// <summary>
    /// 슬라임 양을 소비한다. (발사, 스킬 사용 등)
    /// - 성공하면 true, 실패하면 false.
    /// - 죽었거나, amount가 0 이하거나, 가진 양보다 많이 쓰려 하면 실패(false).
    /// </summary>
    public bool UseSlime(int amount)
    {
        if (isDead) return false;
        if (amount <= 0) return false;
        if (currentSlimeAmount < amount) return false; // 가진 양보다 많이 쓸 수 없음

        currentSlimeAmount -= amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
        return true;
    }

    /// <summary>
    /// 슬라임 양을 늘린다. (아이템 획득 등)
    /// - amount 0 이하면 무시.
    /// - 최대치를 넘지 않게 한다.
    /// - Heal과 비슷하지만, 이쪽은 '죽었어도' 자원 증가 용도로 쓸 수 있게 열어둔다.
    ///   (죽은 상태에서 자원만 채우고 싶은 경우가 있을 수 있으므로 분리했다)
    /// </summary>
    public void AddSlime(int amount)
    {
        if (amount <= 0) return;

        currentSlimeAmount += amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
    }

    /// <summary>
    /// SizeController의 goo(1~5)에 맞춰 체력을 맞춤.
    /// 안 맞추면 먹어서 커져도 발사/데미지 때 SetGoo가 체력 비율로 다시 Normal로 돌아간다.
    /// </summary>
    public void SyncAmountToGoo(float goo)
    {
        float t = Mathf.InverseLerp(1f, 5f, goo);
        currentSlimeAmount = Mathf.Clamp(Mathf.RoundToInt(t * maxSlimeAmount), 1, maxSlimeAmount);
        isDead = false;
    }

    // ======================================================================
    //  상태 전환 함수
    // ======================================================================

    /// <summary>무적 상태를 켜거나 끈다.</summary>
    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    /// <summary>
    /// 죽는다.
    /// - isDead = true
    /// - 슬라임 양 0
    /// - 로그 출력 (나중에 여기서 사망 연출/게임오버 처리를 연결하면 된다)
    /// </summary>
    public void Die()
    {
        if (isDead) return; // 이미 죽었으면 중복 처리 방지

        isDead = true;
        currentSlimeAmount = 0;
        Debug.Log($"[{name}] 슬라임 사망!");
    }

    /// <summary>
    /// 되살린다.
    /// - isDead = false
    /// - 슬라임 양을 최대치로 회복.
    /// </summary>
    public void Revive()
    {
        isDead = false;
        currentSlimeAmount = maxSlimeAmount;
        if (size != null) size.SetToNormal();
    }

    public void ResetStats()
    {
        isDead = false;
        isInvincible = false;
        currentSlimeAmount = maxSlimeAmount;
        if (size != null) size.SetToNormal();
    }
}
