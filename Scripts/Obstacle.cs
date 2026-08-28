using UnityEngine;

public class Obstacle : MonoBehaviour
{
    private GameObject tiny;
    private GameObject foodNormal;
    private GameObject big;
    private GameObject huge;

    private GameObject easy;
    private GameObject normal;
    private GameObject difficult;
    private GameObject gravity;

    [SerializeField] private float easyBelowY = 360f;
    [SerializeField] private float normalBelowY = 1080f;
    [SerializeField] private float foodStep = 90f;

    private int _lastFoodBand = int.MinValue;

    private void Awake()
    {
        BindByName();
    }

    public void BindByName()
    {
        tiny = foodNormal = big = huge = null;
        easy = normal = difficult = gravity = null;

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == transform) continue;

            GameObject go = all[i].gameObject;
            string n = CleanName(go.name);

            if (tiny == null && n == "tiny") tiny = go;
            else if (foodNormal == null && n == "normal") foodNormal = go;
            else if (big == null && n == "big") big = go;
            else if (huge == null && n == "huge") huge = go;
            else if (easy == null && n == "frogspatterneasy") easy = go;
            else if (normal == null && n == "frogspatternnormal") normal = go;
            else if (difficult == null && n == "frogspatterndifficult") difficult = go;
            else if (gravity == null && n == "frogspatterngravity") gravity = go;
        }
    }

    public void Apply(float worldY, SlimeSizeController.SizeState size)
    {
        BindByName(); // 풀에서 다시키면 참조 null되더라
        ApplyDifficulty(worldY);
        ApplyFood(size);
        _lastFoodBand = FoodBand(worldY);
    }

    public void RefreshFood(float slimeY, SlimeSizeController.SizeState size)
    {
        int band = FoodBand(slimeY);
        if (band == _lastFoodBand) return; // 매프레임바꾸니까 깜빡임
        _lastFoodBand = band;
        ApplyFood(size);
    }

    private void ApplyDifficulty(float worldY)
    {
        GameObject pick = gravity;
        if (ThemeManager.FromY(worldY) != ThemeManager.Theme.gravity)
        {
            if (worldY < easyBelowY) pick = easy;
            else if (worldY < normalBelowY) pick = normal;
            else pick = difficult;
        }

        SetExclusive(pick, easy, normal, difficult, gravity);
    }

    private void ApplyFood(SlimeSizeController.SizeState size)
    {
        GameObject pick;
        switch (size)
        {
            case SlimeSizeController.SizeState.Tiny:
            case SlimeSizeController.SizeState.Small:
                pick = tiny;
                break;
            case SlimeSizeController.SizeState.Normal:
                pick = foodNormal;
                break;
            case SlimeSizeController.SizeState.Big:
                pick = big;
                break;
            default:
                pick = huge;
                break;
        }

        SetExclusive(pick, tiny, foodNormal, big, huge);
    }

    private int FoodBand(float y) => Mathf.FloorToInt(y / Mathf.Max(0.01f, foodStep)); // 0나누기터짐

    private static string CleanName(string name)
    {
        int p = name.IndexOf('(');
        if (p > 0) name = name.Substring(0, p); // Clone (1) 이래서 이름매칭안됨
        return name.Trim().ToLowerInvariant();
    }

    private static void SetExclusive(GameObject on, params GameObject[] group)
    {
        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] == null) continue; // 예외처리 안했더니오류생김
            group[i].SetActive(group[i] == on);
        }
    }
}
