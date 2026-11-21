using System.Collections.Generic;
using CardGame.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CardGame.UI
{
    public sealed class ArcaneMainMenu : MonoBehaviour
    {
        public const string SceneName = "MainMenu";
        public const string DuelSceneName = "SampleScene";
        public const string VolumeKey = "ArcaneDuel.Volume";
        public const string FullscreenKey = "ArcaneDuel.Fullscreen";
        public const string QualityKey = "ArcaneDuel.Quality";
        private GameObject modes, settings;
        private Slider volume;
        private Toggle fullscreen;
        private Dropdown quality;
        private Text volumeValue, settingsStatus;
        private Button play;
        private static Font font;
        public static readonly Color Ink = new Color(.035f,.045f,.09f,1);
        public static readonly Color Panel = new Color(.075f,.09f,.16f,1);
        public static readonly Color Gold = new Color(.94f,.76f,.42f,1);
        private static readonly Color Muted = new Color(.62f,.67f,.77f,1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettings()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));
            if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) != 0;
            if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey),0,QualitySettings.names.Length-1));
        }

        private void Awake()
        {
            Transform canvas = CreateCanvas("MainMenuCanvas",10);
            Box("Background",canvas,0,0,1,1,Ink);
            Box("Sidebar",canvas,0,0,.285f,1,new Color(.045f,.055f,.105f));
            Box("GoldLine",canvas,.282f,.065f,.284f,.935f,new Color(.48f,.37f,.19f));
            Label("Brand",canvas,"ARCANE\nDUEL",.045f,.71f,.26f,.92f,44,Gold,TextAnchor.MiddleLeft);
            Label("Subtitle",canvas,"KARTLARINI OKUT.\nTAKTİĞİNİ SEÇ. DÜELLOYA GİR.",.047f,.63f,.25f,.71f,15,Muted,TextAnchor.MiddleLeft);
            Action("ModesButton",canvas,"OYUN MODLARI",.045f,.46f,.245f,.535f,ShowModes);
            Action("SettingsButton",canvas,"AYARLAR",.045f,.365f,.245f,.44f,ShowSettings);
            Action("QuitButton",canvas,"ÇIKIŞ",.045f,.27f,.245f,.345f,Quit);
            Label("Footer",canvas,"50 KART  /  ARUCO\nYEREL DÜELLO PROTOTİPİ",.047f,.07f,.25f,.16f,14,Muted,TextAnchor.MiddleLeft);

            modes = Box("ModeSelection",canvas,.32f,.065f,.96f,.94f,Color.clear).gameObject;
            Label("Eyebrow",modes.transform,"MASAYA HOŞ GELDİN",.025f,.87f,.95f,.94f,16,Gold,TextAnchor.MiddleLeft);
            Label("Heading",modes.transform,"OYUN MODUNU SEÇ",.025f,.77f,.98f,.88f,35,Color.white,TextAnchor.MiddleLeft);
            Label("Intro",modes.transform,"Bir kamera, iki oyuncu ve kartların arasında bir düello.",.025f,.70f,.95f,.78f,18,Muted,TextAnchor.MiddleLeft);
            var local = Box("LocalMode",modes.transform,.025f,.235f,.64f,.68f,Panel);
            Label("LocalBadge",local,"OYUN HAZIR",.06f,.79f,.94f,.94f,14,Gold,TextAnchor.MiddleLeft);
            Label("LocalHeading",local,"YEREL DÜELLO",.06f,.58f,.94f,.80f,29,Color.white,TextAnchor.MiddleLeft);
            Label("LocalDescription",local,"2 oyuncu • Aynı bilgisayar\nArUco kartları ve webcam ile oyna.\nMevcut desteler ve 5 raundluk kurallar.",.06f,.27f,.94f,.59f,18,Muted,TextAnchor.MiddleLeft);
            play = Action("PlayLocalButton",local,"DÜELLOYU BAŞLAT  →",.06f,.07f,.94f,.25f,PlayLocal);
            var art = Box("CardShowcase",modes.transform,.68f,.235f,.975f,.68f,new Color(.06f,.07f,.13f));
            var artwork = new GameObject("FeaturedCard",typeof(RectTransform),typeof(Image));
            artwork.transform.SetParent(art,false); Rect(artwork.GetComponent<RectTransform>(),.07f,.08f,.93f,.94f);
            var image = artwork.GetComponent<Image>();
            var card = Resources.Load<CardData>("Cards/Card_26");
            image.sprite = card != null ? card.cardIcon : null; image.preserveAspect=true; image.raycastTarget=false; image.enabled=image.sprite!=null;
            var online = Box("OnlineMode",modes.transform,.025f,.075f,.975f,.195f,new Color(.065f,.075f,.12f));
            Label("OnlineHeading",online,"ÇEVRİMİÇİ DÜELLO",.04f,.48f,.66f,.91f,20,Muted,TextAnchor.MiddleLeft);
            Label("OnlineDescription",online,"Bu prototipte henüz kullanılamıyor.",.04f,.05f,.72f,.48f,15,Muted,TextAnchor.MiddleLeft);
            var soon=Action("OnlineButton",online,"YAKINDA",.77f,.20f,.96f,.80f,()=>{}); soon.interactable=false;

            settings = Box("SettingsPanel",canvas,.335f,.13f,.94f,.92f,Panel).gameObject;
            Label("SettingsHeading",settings.transform,"AYARLAR",.06f,.81f,.94f,.95f,34,Gold,TextAnchor.MiddleLeft);
            Label("SettingsIntro",settings.transform,"Tercihlerin bu bilgisayarda kaydedilir.",.06f,.73f,.94f,.82f,17,Muted,TextAnchor.MiddleLeft);
            Label("VolumeLabel",settings.transform,"GENEL SES",.06f,.62f,.65f,.71f,18,Color.white,TextAnchor.MiddleLeft);
            volumeValue=Label("VolumeValue",settings.transform,"",.79f,.62f,.94f,.71f,18,Gold,TextAnchor.MiddleRight);
            volume=CreateSlider(settings.transform,.06f,.55f,.94f,.61f);
            volume.onValueChanged.AddListener(value=>volumeValue.text=Mathf.RoundToInt(value*100)+"%");
            fullscreen=CreateToggle(settings.transform,"Tam ekran",.06f,.40f,.94f,.49f);
            Label("QualityLabel",settings.transform,"GÖRÜNTÜ KALİTESİ",.06f,.29f,.94f,.37f,18,Color.white,TextAnchor.MiddleLeft);
            quality=CreateDropdown(settings.transform,.06f,.19f,.94f,.28f);
            settingsStatus=Label("SettingsStatus",settings.transform,"",.06f,.105f,.94f,.18f,15,Gold,TextAnchor.MiddleLeft);
            Action("ApplySettingsButton",settings.transform,"KAYDET",.55f,.025f,.94f,.105f,SaveSettings);
            Action("BackButton",settings.transform,"GERİ",.06f,.025f,.45f,.105f,ShowModes);
            ShowModes();
        }

        public void ShowModes() { modes.SetActive(true); settings.SetActive(false); }
        public void ShowSettings()
        {
            volume.value=AudioListener.volume;
            volumeValue.text=Mathf.RoundToInt(volume.value*100)+"%";
            fullscreen.isOn=Screen.fullScreen;
            quality.value=QualitySettings.GetQualityLevel(); quality.RefreshShownValue();
            settingsStatus.text=""; modes.SetActive(false); settings.SetActive(true);
        }
        public void SaveSettings()
        {
            AudioListener.volume=volume.value;
            Screen.fullScreen=fullscreen.isOn;
            QualitySettings.SetQualityLevel(quality.value);
            PlayerPrefs.SetFloat(VolumeKey,volume.value);
            PlayerPrefs.SetInt(FullscreenKey,fullscreen.isOn?1:0);
            PlayerPrefs.SetInt(QualityKey,quality.value);
            PlayerPrefs.Save(); settingsStatus.text="Ayarlar kaydedildi.";
        }
        public void PlayLocal()
        {
            if(!Application.CanStreamedLevelBeLoaded(DuelSceneName)) { Debug.LogError("Duel scene is missing from Build Settings."); return; }
            play.interactable=false; SceneManager.LoadSceneAsync(DuelSceneName);
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }

        public static Transform CreateCanvas(string name,int order)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=order;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,800); scaler.matchWidthOrHeight=.5f;
            if(Object.FindAnyObjectByType<EventSystem>()==null)
            {
                var events=new GameObject("MenuEventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            return go.transform;
        }
        public static void Rect(RectTransform rect,float x0,float y0,float x1,float y1)
        { rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.offsetMin=rect.offsetMax=Vector2.zero; }
        public static RectTransform Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            Rect(go.GetComponent<RectTransform>(),x0,y0,x1,y1);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return go.GetComponent<RectTransform>();
        }
        public static Text Label(string name,Transform parent,string text,float x0,float y0,float x1,float y1,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter)
        {
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Rect(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
            var label=go.GetComponent<Text>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=align;label.raycastTarget=false;return label;
        }
        public static Button Action(string name,Transform parent,string text,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action)
        {
            var rect=Box(name,parent,x0,y0,x1,y1,new Color(.20f,.18f,.12f));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.targetGraphic.raycastTarget=true;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.22f,1.05f);colors.pressedColor=new Color(.7f,.65f,.5f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.55f,.55f,.55f,.65f);button.colors=colors;
            Label("Label",rect,text,.03f,.05f,.97f,.95f,18,Gold);button.onClick.AddListener(action);return button;
        }
        private static Slider CreateSlider(Transform parent,float x0,float y0,float x1,float y1)
        {
            var rect=Box("VolumeSlider",parent,x0,y0,x1,y1,new Color(.12f,.14f,.20f));rect.GetComponent<Image>().raycastTarget=true;
            var slider=rect.gameObject.AddComponent<Slider>();var fill=Box("Fill",rect,0,.25f,1,.75f,Gold);var handle=Box("Handle",rect,0,0,0,1,Color.white);handle.sizeDelta=new Vector2(14,0);handle.GetComponent<Image>().raycastTarget=true;
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;slider.value=1;return slider;
        }
        private static Toggle CreateToggle(Transform parent,string text,float x0,float y0,float x1,float y1)
        {
            var rect=Box("FullscreenToggle",parent,x0,y0,x1,y1,Color.clear);rect.GetComponent<Image>().raycastTarget=true;
            var toggle=rect.gameObject.AddComponent<Toggle>();var bg=Box("Checkbox",rect,0,.2f,.055f,.8f,new Color(.18f,.20f,.27f));var mark=Box("Checkmark",bg,.18f,.18f,.82f,.82f,Gold);
            toggle.targetGraphic=bg.GetComponent<Image>();toggle.graphic=mark.GetComponent<Image>();Label("Label",rect,text,.09f,0,1,1,20,Color.white,TextAnchor.MiddleLeft);return toggle;
        }
        private static Dropdown CreateDropdown(Transform parent,float x0,float y0,float x1,float y1)
        {
            var rect=Box("QualityDropdown",parent,x0,y0,x1,y1,new Color(.14f,.16f,.23f));rect.GetComponent<Image>().raycastTarget=true;var dropdown=rect.gameObject.AddComponent<Dropdown>();dropdown.targetGraphic=rect.GetComponent<Image>();
            dropdown.captionText=Label("Label",rect,"",.04f,0,.90f,1,20,Color.white,TextAnchor.MiddleLeft);Label("Arrow",rect,"▼",.9f,0,.99f,1,18,Gold);
            var template=Box("Template",rect,0,0,1,0,Panel);template.pivot=new Vector2(.5f,1);template.sizeDelta=new Vector2(0,220);
            var scroll=template.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;var viewport=Box("Viewport",template,0,0,1,1,Panel);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Box("Content",viewport,0,1,1,1,Color.clear);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,40);
            var item=CreateToggle(content,"",0,0,1,1);item.name="Item";item.GetComponent<RectTransform>().sizeDelta=new Vector2(0,40);item.GetComponent<RectTransform>().anchorMin=new Vector2(0,.5f);item.GetComponent<RectTransform>().anchorMax=new Vector2(1,.5f);
            dropdown.itemText=item.transform.Find("Label").GetComponent<Text>();dropdown.template=template;scroll.viewport=viewport;scroll.content=content;dropdown.AddOptions(new List<string>(QualitySettings.names));template.gameObject.SetActive(false);return dropdown;
        }
    }
}
