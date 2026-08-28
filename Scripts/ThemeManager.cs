using UnityEngine;
using MoreMountains.Feedbacks;

public class ThemeManager : MonoBehaviour
{

[SerializeField] private SlimeLaunchController slime;
[SerializeField] private float step = 360f;
int lastTheme = -1;
int index = 0;






public enum Theme{
    normal, 
    rain, 
    wind, 
    gravity
};

    public Theme Current => current;

    public static Theme FromY(float y, float step = 360f)
    {
        int band = Mathf.FloorToInt(y / step);
        if (band < 0) band = 0;
        return (Theme)(band % 4);
    }



    // Start is called once before the first execution of Update after the MonoBehaviour is created
[SerializeField] private MMF_Player normalTheme;
[SerializeField] private MMF_Player rainTheme;
[SerializeField] private MMF_Player windTheme;
[SerializeField] private MMF_Player gravityTheme;

[SerializeField] private GameObject Raincloud;
private GameObject _rainCloudInstance;
[SerializeField] private GameObject Windcloud;
private GameObject _windCloudInstance; 

[SerializeField] private GameObject normalcloud;
private GameObject _normalCloudInstance; 

[SerializeField] private GameObject gravitycloud;
private GameObject _gravityCloudInstance; 

[SerializeField] private GameObject WindLR;
[SerializeField] private GameObject WindRL;
private float height = 180f;
private GameObject _windLRInstance;
private GameObject _windRLInstance;

Theme current = Theme.normal;



    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
               int band = Mathf.FloorToInt(slime.transform.position.y / step);
               if(band != lastTheme)
               {
                lastTheme = band;
                index = band % 4;
                switch(index){
                    case 0:
                    current = Theme.normal;
                    StopAllThemes();
                    normalTheme.PlayFeedbacks();
                    
                    SpawnNormalCloud(band);
                    break;
                    
                    case 1:
                    current = Theme.rain;
                 
                    StopAllThemes();
                    rainTheme.PlayFeedbacks();
                    SpawnRainCloud(band);
                    break;

                    case 2:
                    current = Theme.wind;
                    Wallsurface.GlobalSlippery = false;
                    slime.StopSliding();
                  

                    StopAllThemes();
                    SpawnWind(band);
                    windTheme.PlayFeedbacks();
                    SpawnWindCloud(band);
                    break;

                    case 3:
                    current = Theme.gravity;
              
                    StopAllThemes();
                    gravityTheme.PlayFeedbacks();
                    SpawnGravityCloud(band);
                    break;
                    
                }
                }
    }

    void StopAllThemes()
    {
        if (normalTheme != null) normalTheme.StopFeedbacks();
        if (rainTheme != null) rainTheme.StopFeedbacks();
        if (windTheme != null) windTheme.StopFeedbacks();
        if (gravityTheme != null) gravityTheme.StopFeedbacks();
    }

    void SpawnWind(int band){

        if (WindLR == null) return;
        if (WindRL == null) return;

        float RLposition = (band) * step;
        float LRposition = RLposition + height;
        float y = slime.transform.position.y;

        _windRLInstance = Instantiate(WindRL, new Vector3(15f, RLposition, 0f), Quaternion.identity);
        _windLRInstance = Instantiate(WindLR, new Vector3(-15f, LRposition, 0f), Quaternion.Euler(0f, 180f, 0f));
        
        // if( y> RLposition && y < LRposition){
        //    slime.SetWind(Vector3.left * 15f);
        //    Debug.Log($"{Vector3.left * 7f}WindLeft");

        // }else if( y > LRposition && y < RLposition + step ){
        //     slime.SetWind(Vector3.right * 15f);
        // }
    }


    void SpawnRainCloud(int band)
    {
        if (Raincloud == null) return;

        if (_rainCloudInstance != null)
            Destroy(_rainCloudInstance);

        float y = (band + 1) * step;
         _rainCloudInstance = Instantiate(Raincloud, new Vector3(0f, y, 0f), Quaternion.identity);

         if(  slime.transform.position.y < y && slime.transform.position.y > y - step ){
         Wallsurface.GlobalSlippery = true;
         Debug.Log($"{Wallsurface.GlobalSlippery}Rain");
         }
         else Wallsurface.GlobalSlippery = false;

    }






    void SpawnWindCloud(int band)
    {
        if (Windcloud == null) return;
        if (_windCloudInstance != null)
        Destroy(_windCloudInstance);

        float y = (band + 1) * step;
        _windCloudInstance = Instantiate(Windcloud, new Vector3(0f, y, 0f), Quaternion.identity);
    }
    void SpawnNormalCloud(int band)
    {
        if (normalcloud == null) return;
        if (_normalCloudInstance != null)
        Destroy(_normalCloudInstance);

        float y = (band + 1) * step;
        _normalCloudInstance = Instantiate(normalcloud, new Vector3(0f, y, 0f), Quaternion.identity);
    }
    void SpawnGravityCloud(int band)
    {

        if (gravitycloud == null) return;
        if (_gravityCloudInstance != null)
        Destroy(_gravityCloudInstance);

        float y = (band + 1) * step;
        _gravityCloudInstance = Instantiate(gravitycloud, new Vector3(0f, y, 0f), Quaternion.identity);
    }

}

