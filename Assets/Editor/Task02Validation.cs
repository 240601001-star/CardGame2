using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CardGame.Core;
using CardGame.Data;
using CardGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Task02Validation
{
    private static int _checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value,string message) { _checks++;if(!value)throw new Exception(message); }
    private static object Get(object obj,string field)=>obj.GetType().GetField(field,Private).GetValue(obj);
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
    private static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);

    [MenuItem("CardGame/Validate Task 02 + Task 01 regression")]
    public static void Run()
    {
        try
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before running validation.");
            _checks=0;
            int regression=Task01Validation.RunRegressionChecks();
            RulesTests();
            GameplayTests();
            SceneTests();
            string report=$"PASS: {_checks} Task 02 checks; {regression} Task 01 regression checks.\nBattle/tactic rules, simultaneous death, bounds, ownership, all states, camera-clear gate, visibility loss, one-use cards, hidden tactics, 3-win ending, 5-round draw, restart, persisted HUD/icons verified.\nNo physical webcam or interactive Play Mode test performed. Editor preview-scene tests only.\n";
            File.WriteAllText("Task02-test-results.txt",report);
            File.WriteAllText("Task01-test-results.txt",$"PASS: {regression} Task 01 regression checks under Task 02 duel rules. Original detector/PNG tests unchanged; obsolete direct-player-damage expectations replaced.\nNo physical webcam or Play Mode test performed.\n");
            Debug.Log(report);
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            File.WriteAllText("Task02-test-results.txt",$"FAIL after {_checks} checks:\n{error}\n");
            Debug.LogException(error);
            if(Application.isBatchMode)EditorApplication.Exit(1);else throw;
        }
    }
    private static void RulesTests()
    {
        foreach(Tactic a in Enum.GetValues(typeof(Tactic))) foreach(Tactic b in Enum.GetValues(typeof(Tactic)))
        {
            bool wins=(a==Tactic.Attack&&b==Tactic.Focus)||(a==Tactic.Focus&&b==Tactic.Defense)||(a==Tactic.Defense&&b==Tactic.Attack);
            Check(BattleResolver.Beats(a,b)==wins,"Tactic counter incorrect");
            Check(BattleResolver.Damage(35,a,b)==(wins?42:35),"20% bonus incorrect");
        }
        Check(BattleResolver.Damage(0,Tactic.Attack,Tactic.Attack)==1,"Minimum damage failed");
        Check(BattleResolver.Damage(7,Tactic.Attack,Tactic.Focus)==8,"Integer bonus rounding failed");
        var tie=BattleResolver.Resolve(10,10,Tactic.Attack,10,10,Tactic.Attack);
        Check(tie.Winner==-1 && tie.Steps.Count==1 && tie.Steps[0].Player1HP==0 && tie.Steps[0].Player2HP==0,"Simultaneous death incorrect");
        var result=BattleResolver.Resolve(120,35,Tactic.Attack,95,55,Tactic.Attack);
        Check(result.Winner==-1 && result.Steps.Count==3,"Card HP/ATK calculation incorrect");
        result=BattleResolver.Resolve(120,35,Tactic.Attack,160,25,Tactic.Focus);
        Check(result.Winner==0 && result.Player1Damage==42 && result.Player2Damage==25 && result.Steps.Count==4 && result.Steps[3].Player1HP==20,"Tactic bonus not applied every exchange");
        foreach(var step in result.Steps)Check(step.Player1HP>=0 && step.Player2HP>=0,"Negative HP");
        var limit=BattleResolver.Resolve(int.MaxValue,0,Tactic.Attack,int.MaxValue,0,Tactic.Defense);
        Check(limit.SafetyLimitReached && limit.Winner==-1 && limit.Steps.Count==BattleResolver.MaxExchanges,"Battle safety limit failed");
        Check(BattleResolver.Damage(int.MaxValue,Tactic.Attack,Tactic.Focus)==int.MaxValue,"Attack overflow");
        bool rejected=false;try{BattleResolver.Resolve(0,10,Tactic.Attack,1,1,Tactic.Focus);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"Invalid HP accepted");
        rejected=false;try{BattleResolver.Damage(1,(Tactic)99,Tactic.Focus);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"Invalid tactic accepted");
        var score=new MatchScore();
        foreach(int winner in new[]{0,1,-1,0,1})score.RecordRound(winner);
        Check(score.IsOver && score.CompletedRounds==5 && score.Player1==2 && score.Player2==2 && score.Winner==-1,"Five-round tied match failed");
        rejected=false;try{score.RecordRound(0);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Sixth round accepted");
        score=new MatchScore();for(int i=0;i<3;i++)score.RecordRound(1);
        Check(score.IsOver && score.Winner==1 && score.CompletedRounds==3,"Early 3 wins failed");
    }
    private sealed class Fixture:IDisposable
    {
        public readonly Scene Scene;
        public readonly GameManager Game;
        public readonly CardDatabaseManager Database;
        private readonly GameManager _oldGame;
        private readonly CardDatabaseManager _oldDb;
        public Fixture()
        {
            _oldGame=GameManager.Instance;_oldDb=CardDatabaseManager.Instance;
            Scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/SampleScene.unity");
            foreach(var root in Scene.GetRootGameObjects())
            {
                if(Game==null)Game=root.GetComponentInChildren<GameManager>();
                if(Database==null)Database=root.GetComponentInChildren<CardDatabaseManager>();
            }
            typeof(GameManager).GetProperty("Instance").SetValue(null,Game);
            typeof(CardDatabaseManager).GetProperty("Instance").SetValue(null,Database);
            Call(Database,"LoadCards");Call(Game,"InitGame");
        }
        public void Dispose()
        {
            typeof(GameManager).GetProperty("Instance").SetValue(null,_oldGame);
            typeof(CardDatabaseManager).GetProperty("Instance").SetValue(null,_oldDb);
            EditorSceneManager.ClosePreviewScene(Scene);
        }
        public void Scan(int id,float y)
        {
            Call(Game,"HandleMarkerDetected",id,new Vector2(.5f,y));
            if((int)Get(Game,"_candidateID")==id)
            {
                Set(Game,"_firstSeen",Time.unscaledTime-.6f);
                Set(Game,"_lastSeen",Time.unscaledTime);
            }
            Call(Game,"ProcessTrackers");
        }
        public void Clear()
        {
            Call(Game,"HandleScanFrameCompleted",0);
            Set(Game,"_clearStart",Time.unscaledTime-1.1f);
            Set(Game,"_lastClearFrame",Time.unscaledTime);
            Call(Game,"HandleScanFrameCompleted",0);
        }
        public void Round(int id1,Tactic tactic1,int id2,Tactic tactic2)
        {
            Clear();Scan(id1,.2f);Check(Game.SelectTactic(tactic1),"P1 tactic not accepted");
            Clear();Scan(id2,.8f);Check(Game.SelectTactic(tactic2),"P2 tactic not accepted");Clear();
            Check(Game.State==DuelState.ResolvingBattle && Game.CardsRevealed,"Reveal/battle did not start");
            for(int n=0;Game.State==DuelState.ResolvingBattle && n<BattleResolver.MaxExchanges+1;n++)Call(Game,"AdvanceBattle");
            Check(Game.State==DuelState.RoundResult||Game.State==DuelState.GameOver,"Battle did not finish");
        }
    }
    private static void GameplayTests()
    {
        using(var f=new Fixture())
        {
            var g=f.Game;
            Check(g.State==DuelState.WaitingForPlayer1Card && g.CurrentRound==1 && g.AwaitingCardRemoval,"Initial state");
            Check(!g.SelectTactic(Tactic.Attack) && !g.ContinueRound(),"Wrong-state action accepted");
            f.Scan(0,.2f);Check(!g.HasUsedCard(0),"Held-over restart card accepted");
            Call(g,"HandleMarkerLost",0);Call(g,"ProcessTrackers");
            Check(g.AwaitingCardRemoval,"Lost event alone cleared camera gate");
            Call(g,"HandleScanFrameCompleted",0);
            Set(g,"_clearStart",Time.unscaledTime-2);Set(g,"_lastClearFrame",Time.unscaledTime-.4f);
            Call(g,"HandleScanFrameCompleted",0);Check(g.AwaitingCardRemoval,"Stalled frames cleared removal gate");
            Set(g,"_clearStart",Time.unscaledTime-2);Call(g,"HandleScanFrameCompleted",2);
            Check((float)Get(g,"_clearStart")==-1 && g.AwaitingCardRemoval,"Multiple markers cleared gate");
            f.Clear();Check(!g.AwaitingCardRemoval,"Fresh clear frames did not open gate");
            f.Scan(1,.2f);Check(g.State==DuelState.WaitingForPlayer1Card&&!g.HasUsedCard(1),"P1 accepted P2 card");
            f.Scan(0,.8f);Check(!g.HasUsedCard(0),"Wrong zone accepted");
            Call(g,"HandleMarkerDetected",0,new Vector2(.5f,.2f));Call(g,"ProcessTrackers");
            Check(!g.HasUsedCard(0),"Early confirmation");
            Set(g,"_firstSeen",Time.unscaledTime-1);Call(g,"HandleMarkerLost",0);
            Call(g,"HandleMarkerDetected",0,new Vector2(.5f,.2f));Call(g,"ProcessTrackers");
            Check(!g.HasUsedCard(0),"Short detection loss didn't reset timer");
            Set(g,"_firstSeen",Time.unscaledTime-1);Call(g,"ProcessTrackers");
            Check(!g.HasUsedCard(0),"No fresh observation confirmed card");
            Set(g,"_freshEvidence",true);Set(g,"_lastSeen",Time.unscaledTime-.4f);Call(g,"ProcessTrackers");
            Check(!g.HasUsedCard(0),"Stale observation confirmed card");
            int plays=0;g.OnCardPlayed+=(card,player)=>plays++;
            f.Scan(0,.2f);
            Check(g.State==DuelState.WaitingForPlayer1Tactic&&g.HasUsedCard(0)&&plays==1,"P1 correct scan failed");
            Check(g.GetPlayerHP(0)==120 && g.GetPlayerHP(1)==0,"Scan caused player damage / wrong HP");
            Check(g.GetVisibleCard(0).cardID==0 && g.GetRevealedTactic(0)==null,"Card/tactic preview wrong");
            f.Scan(0,.2f);Check(plays==1,"Held card selected twice");
            Check(!g.ContinueRound(),"Continue bypassed tactic state");
            Check(!g.SelectTactic((Tactic)99),"Invalid tactic accepted by manager");
            Check(g.SelectTactic(Tactic.Attack)&&g.State==DuelState.WaitingForPlayer2Card,"P1 tactic transition");
            Check(!g.SelectTactic(Tactic.Defense)&&g.GetRevealedTactic(0)==null&&g.GetVisibleCard(0)==null,"Secret tactic/card leaked or reselected");
            f.Scan(1,.8f);Check(!g.HasUsedCard(1),"P2 scanned before P1 removed");
            Call(g,"HandleMarkerLost",0);f.Scan(1,.8f);Check(!g.HasUsedCard(1),"New ID incorrectly proved card removal");
            f.Clear();f.Scan(2,.8f);Check(!g.HasUsedCard(2),"P2 accepted P1 card");
            f.Scan(1,.8f);Check(g.State==DuelState.WaitingForPlayer2Tactic&&g.HasUsedCard(1)&&plays==2,"P2 correct scan failed");
            Check(g.SelectTactic(Tactic.Focus)&&g.State==DuelState.WaitingForPlayer2Tactic&&!g.CardsRevealed,"P2 card still present but battle started");
            f.Clear();Check(g.State==DuelState.ResolvingBattle&&g.GetRevealedTactic(0)==Tactic.Attack&&g.GetRevealedTactic(1)==Tactic.Focus,"Tactics not revealed together");
            Check(g.GetPlayerHP(0)==120&&g.GetPlayerHP(1)==95,"Reveal HP incorrect");
            f.Scan(2,.2f);Check(!g.HasUsedCard(2)&&!g.SelectTactic(Tactic.Defense),"Scan/tactic accepted during battle");
            while(g.State==DuelState.ResolvingBattle)Call(g,"AdvanceBattle");
            Check(g.State==DuelState.RoundResult&&g.Score.CompletedRounds==1,"Round result transition");
            int score1=g.Score.Player1,score2=g.Score.Player2;Call(g,"AdvanceBattle");
            Check(g.Score.Player1==score1&&g.Score.Player2==score2,"Round scored twice");
            Check(g.ContinueRound()&&g.CurrentRound==2&&!g.CardsRevealed&&g.AwaitingCardRemoval,"Next round reset");
            f.Scan(0,.2f);Check(g.State==DuelState.WaitingForPlayer1Card,"Old event played next round");
            f.Clear();f.Scan(0,.2f);Check(g.State==DuelState.WaitingForPlayer1Card,"Used card replayed");
            g.RestartGame();
            Check(g.CurrentRound==1&&g.Score.CompletedRounds==0&&!g.HasUsedCard(0)&&!g.HasUsedCard(1)&&g.GetPlayerHP(0)==0&&g.GetVisibleCard(0)==null&&g.LastBattle==null&&g.AwaitingCardRemoval,"Restart incomplete");
            f.Clear();f.Scan(0,.2f);Check(g.State==DuelState.WaitingForPlayer1Tactic,"Restart didn't reset used cards");
            g.RestartGame();
            // Use the real shipped stats: these three pairings/tactics are all P1 wins.
            f.Round(0,Tactic.Attack,6,Tactic.Focus);Check(g.Score.Player1==1,"First round score");g.ContinueRound();
            f.Round(2,Tactic.Attack,5,Tactic.Focus);Check(g.Score.Player1==2,"Second round score");g.ContinueRound();
            f.Round(4,Tactic.Attack,9,Tactic.Focus);
            Check(g.State==DuelState.GameOver&&g.Score.Winner==0&&g.Score.CompletedRounds==3&&!g.IsGameActive,"Three wins didn't end match");
            Check(!g.ContinueRound()&&!g.SelectTactic(Tactic.Defense),"Game over accepted action");
            f.Clear();f.Scan(7,.2f);Check(!g.HasUsedCard(7),"Game over accepted card");
            g.RestartGame();Check(g.Score.Player1==0&&g.State==DuelState.WaitingForPlayer1Card,"Game-over restart failed");
            // Force equal fixture stats, restore asset values afterwards. ScriptableObject assets are not saved.
            var cards=new List<CardData>();var health=new List<int>();var attack=new List<int>();
            for(int id=0;id<10;id++){var c=f.Database.GetCardByID(id);cards.Add(c);health.Add(c.health);attack.Add(c.attackPower);c.health=10;c.attackPower=10;}
            try
            {
                int[] p1={0,2,4,7,8},p2={1,3,5,6,9};
                for(int round=0;round<5;round++)
                {
                    f.Round(p1[round],Tactic.Defense,p2[round],Tactic.Defense);
                    Check(g.Score.Player1==0&&g.Score.Player2==0,"Draw awarded point");
                    if(round<4)Check(g.ContinueRound(),"Premature five-round ending");
                }
                Check(g.State==DuelState.GameOver&&g.Score.Winner==-1&&g.CurrentRound==5,"Five-round draw didn't end match");
            }
            finally{for(int id=0;id<10;id++){cards[id].health=health[id];cards[id].attackPower=attack[id];}}
            Set(g,"player1Deck",new[]{0,0,4,7,8});g.RestartGame();
            Check(g.State==DuelState.GameOver&&!string.IsNullOrEmpty(g.ConfigurationError),"Invalid deck not rejected");
        }
    }
    private static void SceneTests()
    {
        using(var f=new Fixture())
        {
            UIManager ui=null;
            foreach(var root in f.Scene.GetRootGameObjects())if(ui==null)ui=root.GetComponentInChildren<UIManager>();
            Check(ui!=null,"UIManager missing");
            var so=new SerializedObject(ui);
            foreach(string field in new[]{"player1HealthBar","player1HealthText","player2HealthBar","player2HealthText","turnIndicatorText","roundText","scoreText","scanStatusText","cardIconImage","cardNameText","cardStatsText","player1TacticText","player2CardImage","player2CardNameText","player2CardStatsText","player2TacticText","tacticPanel","attackButton","defenseButton","focusButton","battleStatusText","roundResultPanel","roundResultText","endTurnButton","gameOverPanel","gameOverText","restartButton","player1DeckText","player2DeckText"})
                Check(so.FindProperty(field)?.objectReferenceValue!=null,"HUD reference missing: "+field);
            for(int id=0;id<10;id++)Check(f.Database.GetCardByID(id).cardIcon!=null,"Card icon missing: "+id);
            Call(ui,"Bind");ui.Refresh();
            f.Clear();f.Scan(0,.2f);ui.Refresh();
            Check(((Text)so.FindProperty("cardNameText").objectReferenceValue).text=="Kızıl Öfke","Selected card not shown");
            Check(((GameObject)so.FindProperty("tacticPanel").objectReferenceValue).activeSelf,"Tactic buttons hidden");
            // Bind runtime button listeners and actually invoke their onClick actions in an isolated preview scene.
            Call(ui,"Start");
            ((Button)so.FindProperty("attackButton").objectReferenceValue).onClick.Invoke();
            Check(f.Game.State==DuelState.WaitingForPlayer2Card,"Attack button not wired");
            Check(((Text)so.FindProperty("player1TacticText").objectReferenceValue).text=="TAKTİK GİZLİ","UI leaked first tactic");
            Check(((Text)so.FindProperty("cardNameText").objectReferenceValue).text=="KART GİZLİ / BEKLENİYOR","P1 card remained visible to P2");
            f.Clear();f.Scan(1,.8f);
            ((Button)so.FindProperty("focusButton").objectReferenceValue).onClick.Invoke();f.Clear();
            Check(f.Game.State==DuelState.ResolvingBattle,"Focus button not wired");
            Check(((Image)so.FindProperty("cardIconImage").objectReferenceValue).enabled &&
                ((Image)so.FindProperty("player2CardImage").objectReferenceValue).enabled,"Revealed card art hidden");
            Check(((Text)so.FindProperty("player1TacticText").objectReferenceValue).text=="SALDIRI" && ((Text)so.FindProperty("player2TacticText").objectReferenceValue).text=="ODAK","UI reveal incorrect");
            while(f.Game.State==DuelState.ResolvingBattle)Call(f.Game,"AdvanceBattle");
            Check(((GameObject)so.FindProperty("roundResultPanel").objectReferenceValue).activeSelf,"Round panel hidden");
            ((Button)so.FindProperty("endTurnButton").objectReferenceValue).onClick.Invoke();
            Check(f.Game.CurrentRound==2&&f.Game.State==DuelState.WaitingForPlayer1Card,"Next round button not wired");
            ((Button)so.FindProperty("restartButton").objectReferenceValue).onClick.Invoke();
            Check(f.Game.CurrentRound==1&&f.Game.Score.CompletedRounds==0,"Restart button not wired");
            f.Clear();f.Scan(0,.2f);
            ((Button)so.FindProperty("defenseButton").objectReferenceValue).onClick.Invoke();
            Check(f.Game.State==DuelState.WaitingForPlayer2Card && f.Game.GetRevealedTactic(0)==null,"Defense button not wired / secret leaked");
            f.Clear();f.Scan(6,.8f);f.Game.SelectTactic(Tactic.Attack);f.Clear();
            Check(f.Game.State==DuelState.ResolvingBattle,"Restart-during-battle fixture failed");
            f.Game.RestartGame();Call(f.Game,"AdvanceBattle");
            Check(f.Game.State==DuelState.WaitingForPlayer1Card && f.Game.Score.CompletedRounds==0 && f.Game.LastBattle==null,"Old battle resumed after restart");
        }
    }
}
