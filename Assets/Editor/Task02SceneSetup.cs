using System;
using System.IO;
using System.Linq;
using CardGame.Core;
using CardGame.Data;
using CardGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds and serializes the duel HUD using the existing Canvas/UIManager/camera.</summary>
public static class Task02SceneSetup
{
    private static readonly Color Ink = new Color(0.035f,0.045f,0.09f,1);
    private static readonly Color PanelColor = new Color(0.075f,0.09f,0.16f,1);
    private static readonly Color Gold = new Color(0.94f,0.76f,0.42f,1);
    private static Font _font;
    private static SerializedObject _ui;
    [MenuItem("CardGame/Setup Task 02 HUD")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before scene setup.");
        var scene=EditorSceneManager.GetActiveScene();
        if (scene.path!="Assets/Scenes/SampleScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved SampleScene first. Setup will not discard unsaved edits.");
        var ui=UnityEngine.Object.FindAnyObjectByType<UIManager>();
        var game=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var camera=UnityEngine.Object.FindAnyObjectByType<WebCamArucoManager>();
        var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if(ui==null||game==null||camera==null||canvas==null)throw new InvalidOperationException("Expected existing managers and Canvas.");
        _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _ui=new SerializedObject(ui);
        var previous=canvas.transform.Find("ArcaneDuelHUD");
        if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
        var scaler=canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,800);
        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight=0.5f;
        var root=Panel("ArcaneDuelHUD",canvas.transform,0,0,1,1,Ink);
        Label("Title",root,"ARCANE DUEL",.04f,.91f,.4f,.985f,32,Gold,TextAnchor.MiddleLeft);
        Bind("roundText",Label("Round",root,"RAUND 1 / 5",.43f,.91f,.6f,.985f,23,Gold));
        Bind("scoreText",Label("Score",root,"OYUNCU 1   0 : 0   OYUNCU 2",.61f,.91f,.96f,.985f,23,Color.white));
        Bind("turnIndicatorText",Label("Turn",root,"OYUNCU 1",.04f,.85f,.3f,.91f,24,Gold,TextAnchor.MiddleLeft));
        Bind("scanStatusText",Label("ScanStatus",root,"Kamera bekleniyor",.31f,.815f,.96f,.91f,19,Color.white));
        BuildCard(root,0,.04f,.345f);
        BuildCard(root,1,.655f,.96f);
        var center=Panel("ScannerAndTactics",root,.37f,.22f,.63f,.80f,PanelColor);
        Label("ScannerHeading",center,"TEK KART • TEK KAMERA",.04f,.91f,.96f,1,15,Gold);
        var cameraSO=new SerializedObject(camera);
        cameraSO.FindProperty("showDebugPanel").boolValue=false;
        cameraSO.ApplyModifiedPropertiesWithoutUndo();
        var raw=(RawImage)cameraSO.FindProperty("displayImage").objectReferenceValue;
        if(raw==null)throw new InvalidOperationException("Existing webcam RawImage missing.");
        // Keep the camera feed outside the generated subtree so re-running setup never deletes it.
        raw.transform.SetParent(canvas.transform,false);
        SetRect(raw.rectTransform,.395f,.575f,.605f,.735f);
        raw.raycastTarget=false;
        raw.enabled=false;
        raw.transform.SetAsLastSibling();
        var aspect=raw.GetComponent<AspectRatioFitter>();
        if(aspect==null)aspect=raw.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode=AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspect.aspectRatio=16f/9f;
        SetRect(raw.rectTransform,.395f,.575f,.605f,.735f);
        raw.texture=Texture2D.blackTexture;
        Bind("battleStatusText",Label("BattleStatus",center,"ARCANE DUEL",.06f,.36f,.94f,.60f,19,Color.white));
        var tactics=Panel("TacticPanel",center,.06f,.02f,.94f,.34f,new Color(0,0,0,0));
        Bind("tacticPanel",tactics.gameObject);
        Bind("attackButton",Button("Attack",tactics,"SALDIRI",0,.68f,1,1,new Color(.5f,.16f,.18f)));
        Bind("defenseButton",Button("Defense",tactics,"SAVUNMA",0,.34f,1,.66f,new Color(.13f,.30f,.46f)));
        Bind("focusButton",Button("Focus",tactics,"ODAK",0,0,1,.32f,new Color(.32f,.20f,.48f)));
        Bind("player1DeckText",Label("Player1Deck",root,"",.04f,.12f,.45f,.215f,19,Gold,TextAnchor.MiddleLeft));
        Bind("player2DeckText",Label("Player2Deck",root,"",.55f,.12f,.96f,.215f,19,Gold,TextAnchor.MiddleRight));
        Label("Help",root,"Saldırı > Odak  •  Odak > Savunma  •  Savunma > Saldırı     |     Üstün taktik: +%20 ATK",.04f,.035f,.96f,.10f,18,new Color(.65f,.70f,.82f));
        var round=Panel("RoundResult",root,.03f,.01f,.97f,.21f,new Color(.12f,.15f,.24f));
        Bind("roundResultPanel",round.gameObject);
        Bind("roundResultText",Label("Result",round,"",.03f,.18f,.69f,.85f,27,Gold,TextAnchor.MiddleLeft));
        Bind("endTurnButton",Button("NextRound",round,"SONRAKİ RAUND",.72f,.20f,.97f,.80f,new Color(.27f,.23f,.12f)));
        var over=Panel("GameOver",root,.03f,.01f,.97f,.21f,new Color(.17f,.13f,.25f));
        Bind("gameOverPanel",over.gameObject);
        Bind("gameOverText",Label("MatchResult",over,"",.03f,.10f,.69f,.90f,24,Gold,TextAnchor.MiddleLeft));
        Bind("restartButton",Button("Restart",over,"YENİ MAÇ",.72f,.20f,.97f,.80f,new Color(.27f,.23f,.12f)));
        _ui.ApplyModifiedPropertiesWithoutUndo();
        round.gameObject.SetActive(false);over.gameObject.SetActive(false);tactics.gameObject.SetActive(false);
        foreach(string path in Directory.GetFiles("Assets/Resources/Cards","*.asset").OrderBy(p=>p))
        {
            CardData card=AssetDatabase.LoadAssetAtPath<CardData>(path);
            string artwork=Directory.GetFiles("Assets/Cards",card.cardID.ToString("00")+"_*.png").Single();
            Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(artwork);
            if(sprite==null)throw new InvalidOperationException("Sprite import missing: "+artwork);
            card.cardIcon=sprite;
            EditorUtility.SetDirty(card);AssetDatabase.SaveAssetIfDirty(card);
        }
        EditorUtility.SetDirty(ui);EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Task02-scene-setup.txt","PASS: existing Canvas, webcam and UIManager preserved; full duel HUD serialized; 10 card sprites linked.\n");
        Debug.Log("TASK 02 HUD and card icons saved.");
    }
    private static void BuildCard(RectTransform root,int player,float left,float right)
    {
        var panel=Panel("Player"+(player+1)+"Card",root,left,.24f,right,.80f,PanelColor);
        if(player==0)Bind("cardInfoPanel",panel.gameObject);
        Label("Owner",panel,"OYUNCU "+(player+1),.04f,.88f,.96f,.98f,19,Gold);
        var name=Label("CardName",panel,"KART BEKLENİYOR",.04f,.79f,.96f,.89f,22,Color.white);
        var art=new GameObject("CardArtwork",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        art.transform.SetParent(panel,false);SetRect((RectTransform)art.transform,.23f,.26f,.77f,.78f);
        var image=art.GetComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;image.enabled=false;
        var stats=Label("Stats",panel,"",.04f,.18f,.96f,.26f,18,Gold);
        var tactic=Label("Tactic",panel,"TAKTİK GİZLİ",.04f,.11f,.96f,.18f,17,Color.white);
        var hp=HealthBar(panel);
        var hpText=Label("HPText",panel,"",.1f,0,.9f,.06f,18,Color.white);
        Bind(player==0?"cardNameText":"player2CardNameText",name);
        Bind(player==0?"cardIconImage":"player2CardImage",image);
        Bind(player==0?"cardStatsText":"player2CardStatsText",stats);
        Bind(player==0?"player1TacticText":"player2TacticText",tactic);
        Bind(player==0?"player1HealthBar":"player2HealthBar",hp);
        Bind(player==0?"player1HealthText":"player2HealthText",hpText);
    }
    private static Slider HealthBar(RectTransform parent)
    {
        var panel=Panel("CardHP",parent,.06f,.06f,.94f,.09f,new Color(.17f,.19f,.25f));
        var fill=Panel("Fill",panel,0,0,1,1,new Color(.20f,.72f,.52f));
        var slider=panel.gameObject.AddComponent<Slider>();
        slider.fillRect=fill;slider.direction=Slider.Direction.LeftToRight;slider.interactable=false;
        slider.transition=Selectable.Transition.None;slider.maxValue=160;slider.value=160;
        return slider;
    }
    private static RectTransform Panel(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;SetRect(rect,x0,y0,x1,y1);
        var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        return rect;
    }
    private static Text Label(string name,Transform parent,string value,float x0,float y0,float x1,float y1,int size,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
        go.transform.SetParent(parent,false);SetRect((RectTransform)go.transform,x0,y0,x1,y1);
        var text=go.GetComponent<Text>();text.font=_font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;
        text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=size;text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        return text;
    }
    private static Button Button(string name,Transform parent,string label,float x0,float y0,float x1,float y1,Color color)
    {
        var panel=Panel(name,parent,x0,y0,x1,y1,color);panel.GetComponent<Image>().raycastTarget=true;
        var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel.GetComponent<Image>();
        var colors=button.colors;colors.highlightedColor=new Color(1.2f,1.2f,1.2f);colors.pressedColor=new Color(.7f,.7f,.7f);
        colors.selectedColor=colors.normalColor;button.colors=colors;
        Label("Label",panel,label,.02f,.02f,.98f,.98f,20,Color.white);
        var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
        return button;
    }
    private static void SetRect(RectTransform rect,float x0,float y0,float x1,float y1)
    {
        rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
    }
    private static void Bind(string field,UnityEngine.Object value)
    {
        var property=_ui.FindProperty(field);if(property==null)throw new InvalidOperationException("UI field missing: "+field);
        property.objectReferenceValue=value;
    }
}
