using CardGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>Inspector-wired menu. Its visual modules are reusable prefabs.</summary>
    public sealed class ArcaneMainMenu : MonoBehaviour
    {
        public const string SceneName="MainMenu",DuelSceneName="SampleScene";
        public const string VolumeKey="ArcaneDuel.Volume",FullscreenKey="ArcaneDuel.Fullscreen",QualityKey="ArcaneDuel.Quality";
        [SerializeField] private GameObject modes,settings;
        [SerializeField] private Slider volume;
        [SerializeField] private Toggle fullscreen;
        [SerializeField] private TMP_Dropdown quality;
        [SerializeField] private TMP_Text volumeValue,settingsStatus;
        [SerializeField] private Button modesButton,settingsButton,quitButton,localButton,multiButton,backButton,saveButton;
        private bool loading;
        public static readonly Color Ink=ArcaneUIFactory.Ink,Panel=new Color(.08f,.065f,.055f),Gold=ArcaneUIFactory.Gold;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettings()
        {
            AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));
            if(PlayerPrefs.HasKey(FullscreenKey))Screen.fullScreen=PlayerPrefs.GetInt(FullscreenKey)!=0;
            if(PlayerPrefs.HasKey(QualityKey))QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey),0,QualitySettings.names.Length-1));
        }
        private void Awake()
        {
            modesButton.onClick.AddListener(ShowModes);settingsButton.onClick.AddListener(ShowSettings);quitButton.onClick.AddListener(Quit);
            localButton.onClick.AddListener(PlayLocal);multiButton.onClick.AddListener(()=>Load(MultiCardTable.SceneName));
            backButton.onClick.AddListener(ShowModes);saveButton.onClick.AddListener(SaveSettings);
            volume.onValueChanged.AddListener(value=>volumeValue.text=Mathf.RoundToInt(value*100)+"%");
            ShowModes();
        }
        public void ShowModes(){modes.SetActive(true);settings.SetActive(false);}
        public void ShowSettings()
        {
            volume.value=AudioListener.volume;volumeValue.text=Mathf.RoundToInt(volume.value*100)+"%";fullscreen.isOn=Screen.fullScreen;
            quality.value=QualitySettings.GetQualityLevel();quality.RefreshShownValue();settingsStatus.text="";modes.SetActive(false);settings.SetActive(true);
        }
        public void SaveSettings()
        {
            AudioListener.volume=volume.value;Screen.fullScreen=fullscreen.isOn;QualitySettings.SetQualityLevel(quality.value);
            PlayerPrefs.SetFloat(VolumeKey,volume.value);PlayerPrefs.SetInt(FullscreenKey,fullscreen.isOn?1:0);PlayerPrefs.SetInt(QualityKey,quality.value);PlayerPrefs.Save();settingsStatus.text="Ayarlar kaydedildi.";
        }
        public void PlayLocal(){Load(DuelSceneName);}
        private void Load(string scene)
        {
            if(loading)return;
            if(!Application.CanStreamedLevelBeLoaded(scene)){settingsStatus.text="Oyun sahnesi bulunamadı.";return;}
            loading=true;SceneManager.LoadSceneAsync(scene);
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        // Shared API retained for the independent multi-card mode and navigation.
        public static Transform CreateCanvas(string name,int order)=>ArcaneUIFactory.Canvas(name,order);
        public static void Rect(RectTransform rect,float x0,float y0,float x1,float y1)=>ArcaneUIFactory.Rect(rect,x0,y0,x1,y1);
        public static RectTransform Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)=>ArcaneUIFactory.Panel(name,parent,x0,y0,x1,y1,color);
        public static TMP_Text Label(string name,Transform parent,string value,float x0,float y0,float x1,float y1,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter)=>ArcaneUIFactory.Label(name,parent,value,x0,y0,x1,y1,size,color,align);
        public static Button Action(string name,Transform parent,string value,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action)=>ArcaneUIFactory.Button(name,parent,value,x0,y0,x1,y1,action);
    }
}
