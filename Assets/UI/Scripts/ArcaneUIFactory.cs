using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CardGame.UI
{
    public static class ArcaneUIFactory
    {
        public static readonly Color Gold=new Color(.93f,.76f,.43f);
        public static readonly Color Ink=new Color(.025f,.022f,.025f);
        private static ArcaneUITheme Theme=>ArcaneUITheme.Current!=null?ArcaneUITheme.Current:throw new System.InvalidOperationException("Arcane UI Theme asset is missing.");
        private static GameObject Clone(GameObject prefab,Transform parent)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying)return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,parent);
#endif
            return Object.Instantiate(prefab,parent,false);
        }
        public static void Rect(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public static Transform Canvas(string name,int order)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=order;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)>=16f/9f?1:0;go.AddComponent<ArcaneCanvasScale>();
            if(Object.FindAnyObjectByType<EventSystem>()==null)
            {
                var events=new GameObject("UIEventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            return go.transform;
        }
        public static Transform SafeContent(Transform parent)
        {
            var go=new GameObject("AspectSafeContent",typeof(RectTransform),typeof(AspectRatioFitter));go.transform.SetParent(parent,false);
            Rect(go.GetComponent<RectTransform>(),0,0,1,1);var fit=go.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9f;return go.transform;
        }
        public static RectTransform Panel(string name,Transform parent,float x0,float y0,float x1,float y1,Color tint)
        {
            var go=Clone(Theme.panelPrefab,parent);go.name=name;var rect=go.GetComponent<RectTransform>();Rect(rect,x0,y0,x1,y1);
            var image=go.GetComponent<Image>();image.raycastTarget=false;
            bool primitive=name=="Divider"||name=="GoldLine"||name=="Fill"||name=="Cost"||name=="Checkmark"||name=="Portrait"||name=="Checkbox";
            if(tint.a==0){image.sprite=null;image.color=Color.clear;}
            else if(primitive){image.sprite=null;image.color=tint;}
            else if(name.Contains("Background")){image.sprite=Theme.Get("Leather");image.type=Image.Type.Simple;image.color=new Color(.22f,.21f,.21f,tint.a);}
            else{image.color=new Color(1,1,1,tint.a);}
            return rect;
        }
        public static TMP_Text Label(string name,Transform parent,string value,float x0,float y0,float x1,float y1,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var go=Clone(Theme.textPrefab,parent);go.name=name;Rect(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
            var text=go.GetComponent<TMP_Text>();text.text=value;text.font=size>=24?Theme.titleFont:Theme.bodyFont;text.fontSize=size*1.5f;text.color=color;text.alignment=Alignment(align);text.raycastTarget=false;text.enableAutoSizing=true;text.fontSizeMax=size*1.5f;text.fontSizeMin=size*.85f;text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }
        public static TextAlignmentOptions Alignment(TextAnchor anchor)
        {
            switch(anchor){case TextAnchor.MiddleLeft:return TextAlignmentOptions.MidlineLeft;case TextAnchor.MiddleRight:return TextAlignmentOptions.MidlineRight;case TextAnchor.UpperLeft:return TextAlignmentOptions.TopLeft;case TextAnchor.UpperCenter:return TextAlignmentOptions.Top;default:return TextAlignmentOptions.Center;}
        }
        public static Button Button(string name,Transform parent,string value,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action)
        {
            var go=Clone(Theme.buttonPrefab,parent);go.name=name;Rect(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
            go.GetComponentInChildren<TMP_Text>(true).text=value;var button=go.GetComponent<Button>();button.onClick.RemoveAllListeners();button.onClick.AddListener(action);StyleButton(button);if(Application.isPlaying)ArcaneUIAudio.Bind(button);return button;
        }
        public static void StyleButton(Button button)
        {
            var text=button.GetComponentInChildren<TMP_Text>(true);if(text==null)return;
            text.font=Theme.bodyFont;text.fontStyle=FontStyles.Normal;text.enableAutoSizing=true;
            text.fontSize=26;text.fontSizeMax=26;text.fontSizeMin=16;
            text.margin=new Vector4(10,3,10,3);text.alignment=TextAlignmentOptions.Center;
        }
        public static GameObject Module(GameObject prefab,string name,Transform parent,float x0,float y0,float x1,float y1)
        {var go=Clone(prefab,parent);go.name=name;Rect(go.GetComponent<RectTransform>(),x0,y0,x1,y1);return go;}
    }
}
