using System;
using System.IO;
using System.Reflection;
using CardGame.Core;
using CardGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Real Play Mode smoke test in a scene copy, with webcam disabled before entering Play.</summary>
[InitializeOnLoad]
public static class Task02PlaySmoke
{
    private const string Key="ArcaneDuel.Task02Smoke";
    private const string ScenePath="Assets/Editor/Task02Smoke.unity";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int _stage;
    private static double _next;
    static Task02PlaySmoke()
    {
        EditorApplication.playModeStateChanged+=StateChanged;
        EditorApplication.update+=Tick;
    }
    [MenuItem("CardGame/Run Task 02 Play Mode smoke (camera disabled)")]
    public static void Run()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open saved SampleScene, outside Play Mode, first.");
        EditorSceneManager.SaveScene(scene,ScenePath,true);
        var copy=EditorSceneManager.OpenScene(ScenePath);
        var camera=UnityEngine.Object.FindAnyObjectByType<WebCamArucoManager>();
        camera.enabled=false;
        var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var config=new SerializedObject(gm);config.FindProperty("enableKeyboardSimulation").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(copy);
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){_stage=0;_next=EditorApplication.timeSinceStartup+2;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }
    }
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);}
    private static void Call(GameManager game,string method,params object[] args)=>typeof(GameManager).GetMethod(method,Private).Invoke(game,args);
    private static void Set(GameManager game,string field,object value)=>typeof(GameManager).GetField(field,Private).SetValue(game,value);
    private static void Clear(GameManager game)
    {
        Call(game,"HandleScanFrameCompleted",0);Set(game,"_clearStart",Time.unscaledTime-1.1f);Set(game,"_lastClearFrame",Time.unscaledTime);Call(game,"HandleScanFrameCompleted",0);
    }
    private static void Scan(GameManager game,int id,float y)
    {
        Call(game,"HandleMarkerDetected",id,new Vector2(.5f,y));Set(game,"_firstSeen",Time.unscaledTime-.6f);Set(game,"_lastSeen",Time.unscaledTime);Call(game,"ProcessTrackers");
    }
    private static Button Button(UIManager ui,string field)=>(Button)typeof(UIManager).GetField(field,Private).GetValue(ui);
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.timeSinceStartup<_next)return;
        try
        {
            var game=GameManager.Instance;
            var ui=UnityEngine.Object.FindAnyObjectByType<UIManager>();
            Check(game!=null&&ui!=null,"Runtime managers unavailable");
            Check(!UnityEngine.Object.FindAnyObjectByType<WebCamArucoManager>().IsCameraRunning,"Smoke test unexpectedly opened webcam");
            if(_stage==0)
            {
                Check(game.State==DuelState.WaitingForPlayer1Card,"Initial Play Mode state");
                Canvas.ForceUpdateCanvases();
                var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
                Check(canvas.enabled&&((RectTransform)canvas.transform).rect.width>0,"Canvas not rendering");
                Clear(game);Scan(game,0,.2f);
                Check(game.State==DuelState.WaitingForPlayer1Tactic,"P1 runtime scan");
                _stage=1;_next=EditorApplication.timeSinceStartup+7;
                Debug.Log("TASK02 SMOKE: Player 1 card/tactic preview ready (simulated scan, camera disabled).");
            }
            else if(_stage==1)
            {
                Button(ui,"attackButton").onClick.Invoke();Clear(game);Scan(game,6,.8f);
                Check(game.State==DuelState.WaitingForPlayer2Tactic,"P2 runtime scan");
                _stage=2;_next=EditorApplication.timeSinceStartup+7;
                Debug.Log("TASK02 SMOKE: Player 2 card/tactic preview ready; P1 tactic hidden.");
            }
            else if(_stage==2)
            {
                Button(ui,"focusButton").onClick.Invoke();Clear(game);
                Check(game.State==DuelState.ResolvingBattle,"Runtime battle coroutine did not start");
                _stage=3;_next=EditorApplication.timeSinceStartup+5;
            }
            else
            {
                Check(game.State==DuelState.RoundResult&&game.Score.Player1==1&&game.GetPlayerHP(0)==20&&game.GetPlayerHP(1)==0,"Runtime coroutine battle result incorrect");
                Button(ui,"endTurnButton").onClick.Invoke();Check(game.CurrentRound==2,"Runtime next-round button");
                Button(ui,"restartButton").onClick.Invoke();Check(game.CurrentRound==1&&game.Score.Player1==0&&!game.HasUsedCard(0),"Runtime restart button");
                File.WriteAllText("Task02-play-smoke-results.txt","PASS: real Play Mode initialization, simulated P1/P2 scans, hidden tactics, actual button handlers, coroutine-driven simultaneous HP steps (20/0 result), next round and restart. Webcam component disabled in a scene copy; no physical webcam test.\n");
                Debug.Log("TASK02 Play Mode smoke PASS (camera disabled).");
                EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception error)
        {
            File.WriteAllText("Task02-play-smoke-results.txt","FAIL: "+error);
            Debug.LogException(error);EditorApplication.ExitPlaymode();
        }
    }
}
