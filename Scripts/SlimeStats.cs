using UnityEngine;

public class SlimeStats : MonoBehaviour
{
    [Header("슬라임 양 (체력 겸 자원)")]
    [SerializeField] private int currentSlimeAmount = 10;

    [SerializeField] private int maxSlimeAmount = 10;

    [SerializeField] private int minLaunchAmount = 1; // 일단넣음 발사제한 아직안씀?

    [Header("상태 플래그")]
    [SerializeField] private bool isInvincible = false;

    [SerializeField] private bool isDead = false;

    [SerializeField] private SlimeSizeController size;

    public int CurrentSlimeAmount => currentSlimeAmount;
    public int MaxSlimeAmount => maxSlimeAmount;
    public int MinLaunchAmount => minLaunchAmount;
    public bool IsInvincible => isInvincible;
    public bool IsDead => isDead;

    public bool CanLaunch => !isDead && currentSlimeAmount >= minLaunchAmount;

    public void TakeDamage(int amount)
    {
        if (isDead || isInvincible) return;
        if (amount <= 0) return; // 음수넣으니까 회복됨 뭐야

        currentSlimeAmount -= amount;

        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
        if (currentSlimeAmount <= 0)
            Die();
    }

    private void Start()
    {
        if (size != null)
            size.SetToNormal();
    }

    // 0.3이 0되서 데미지무시됨 ceil로바꿈
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        int damage = Mathf.CeilToInt(amount);
        TakeDamage(damage);
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentSlimeAmount += amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
    }

    public bool UseSlime(int amount)
    {
        if (isDead) return false;
        if (amount <= 0) return false;
        if (currentSlimeAmount < amount) return false;

        currentSlimeAmount -= amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
        return true;
    }

    public void AddSlime(int amount)
    {
        if (amount <= 0) return;

        currentSlimeAmount += amount;
        currentSlimeAmount = Mathf.Clamp(currentSlimeAmount, 0, maxSlimeAmount);
    }

    // 이거안맞추면 먹고커져도 체력때문에 다시normal됨 한참찾음
    public void SyncAmountToGoo(float goo)
    {
        float t = Mathf.InverseLerp(1f, 5f, goo);
        currentSlimeAmount = Mathf.Clamp(Mathf.RoundToInt(t * maxSlimeAmount), 1, maxSlimeAmount);
        isDead = false;
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    public void Die()
    {
        if (isDead) return; // 두번죽음

        isDead = true;
        currentSlimeAmount = 0;
        Debug.Log($"[{name}] 슬라임 사망!");
    }

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
