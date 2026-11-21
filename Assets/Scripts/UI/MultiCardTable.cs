using System;
using System.Collections.Generic;
using CardGame.Core;
using CardGame.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardGame.UI
{
    public sealed class MultiCardTable : MonoBehaviour
    {
        public const string SceneName="MultiCardScene";
        public MultiCardMatch Match { get; private set; }
        private Transform canvas,hand,ownBoard,enemyBoard;
        private Text ownInfo,enemyInfo,turnInfo,status,selection,passText;
        private Button play,draw,endTurn,enemyHero;
        private GameObject passPanel,leavePanel;
        private int selectedHand=-1,selectedAttacker=-1;
        private bool handoff;
        private readonly Dictionary<int,Sprite> portraits=new Dictionary<int,Sprite>();
        private readonly Color blue=new Color(.18f,.40f,.66f),green=new Color(.17f,.42f,.32f);
        private void Awake(){Initialize();}
        public void Initialize()
        {
            if(canvas!=null)return;
            canvas=ArcaneMainMenu.CreateCanvas("MultiCardCanvas",10);canvas.SetParent(transform,false);
            ArcaneMainMenu.Box("Background",canvas,0,0,1,1,ArcaneMainMenu.Ink);
            ArcaneMainMenu.Label("Title",canvas,"ARCANE DUEL  /  ÇOK KARTLI MOD",.025f,.925f,.79f,.99f,25,ArcaneMainMenu.Gold,TextAnchor.MiddleLeft);
            ArcaneMainMenu.Action("TableMenuButton",canvas,"ANA MENÜ",.82f,.935f,.98f,.985f,()=>leavePanel.SetActive(true));
            enemyInfo=ArcaneMainMenu.Label("EnemyInfo",canvas,"",.025f,.84f,.48f,.92f,19,Color.white,TextAnchor.MiddleLeft);
            enemyHero=ArcaneMainMenu.Action("EnemyHeroButton",canvas,"RAKİP KAHRAMAN",.51f,.845f,.79f,.915f,()=>AttackTarget(-1));
            enemyBoard=ArcaneMainMenu.Box("EnemyBoard",canvas,.025f,.59f,.79f,.825f,new Color(.14f,.09f,.14f));
            ArcaneMainMenu.Label("EnemyBoardHeading",canvas,"RAKİP MASASI",.04f,.795f,.3f,.825f,12,new Color(.73f,.57f,.62f),TextAnchor.MiddleLeft);
            ArcaneMainMenu.Box("Divider",canvas,.025f,.558f,.79f,.56f,new Color(.45f,.35f,.18f));
            ownBoard=ArcaneMainMenu.Box("OwnBoard",canvas,.025f,.31f,.79f,.545f,new Color(.075f,.14f,.15f));
            ArcaneMainMenu.Label("OwnBoardHeading",canvas,"SENİN MASAN",.04f,.515f,.3f,.545f,12,new Color(.52f,.74f,.70f),TextAnchor.MiddleLeft);
            ownInfo=ArcaneMainMenu.Label("OwnInfo",canvas,"",.025f,.25f,.79f,.305f,19,ArcaneMainMenu.Gold,TextAnchor.MiddleLeft);
            hand=ArcaneMainMenu.Box("Hand",canvas,.025f,.025f,.79f,.245f,ArcaneMainMenu.Panel);
            var sidebar=ArcaneMainMenu.Box("Controls",canvas,.815f,.025f,.985f,.92f,ArcaneMainMenu.Panel);
            turnInfo=ArcaneMainMenu.Label("TurnInfo",sidebar,"",.06f,.84f,.94f,.98f,23,ArcaneMainMenu.Gold);
            status=ArcaneMainMenu.Label("Status",sidebar,"",.07f,.64f,.93f,.83f,16,Color.white);
            selection=ArcaneMainMenu.Label("Selection",sidebar,"Bir el kartı seç.",.07f,.41f,.93f,.63f,16,Color.white);
            play=ArcaneMainMenu.Action("PlayCardButton",sidebar,"KARTI OYNA",.07f,.32f,.93f,.40f,PlaySelected);
            draw=ArcaneMainMenu.Action("DrawCardButton",sidebar,"KART ÇEK • 1 MANA",.07f,.22f,.93f,.30f,()=>{Match.ExtraDraw();Refresh();});
            endTurn=ArcaneMainMenu.Action("EndTurnButton",sidebar,"TURU BİTİR",.07f,.11f,.93f,.20f,EndTurn);
            ArcaneMainMenu.Action("RestartTableButton",sidebar,"YENİ MAÇ",.07f,.025f,.93f,.095f,Restart);
            foreach(var button in new[]{play,draw,endTurn})button.GetComponentInChildren<Text>().fontSize=15;
            passPanel=Overlay("PassPanel","SIRA DİĞER OYUNCUDA",out passText);
            ArcaneMainMenu.Action("ReadyButton",passPanel.transform,"HAZIRIM • ELİMİ GÖSTER",.31f,.31f,.69f,.40f,ConfirmHandoff);
            leavePanel=Overlay("TableLeavePanel","ANA MENÜYE DÖN?",out var leaveText);
            leaveText.text="Devam eden maç sona erecek.";
            ArcaneMainMenu.Action("CancelTableLeaveButton",leavePanel.transform,"VAZGEÇ",.28f,.31f,.48f,.40f,()=>leavePanel.SetActive(false));
            ArcaneMainMenu.Action("ConfirmTableLeaveButton",leavePanel.transform,"ANA MENÜ",.52f,.31f,.72f,.40f,()=>SceneManager.LoadSceneAsync(ArcaneMainMenu.SceneName));
            leavePanel.SetActive(false);Restart();
        }
        private GameObject Overlay(string name,string title,out Text description)
        {
            var overlay=ArcaneMainMenu.Box(name,canvas,0,0,1,1,new Color(.025f,.03f,.065f,.99f));overlay.GetComponent<Image>().raycastTarget=true;
            ArcaneMainMenu.Label("Title",overlay,title,.15f,.60f,.85f,.73f,35,ArcaneMainMenu.Gold);
            description=ArcaneMainMenu.Label("Description",overlay,"",.18f,.45f,.82f,.59f,22,Color.white);return overlay.gameObject;
        }
        public void Restart()
        {
            Match=new MultiCardMatch(Resources.LoadAll<CardData>("Cards"),Environment.TickCount);
            selectedHand=selectedAttacker=-1;handoff=false;passPanel.SetActive(false);Refresh();
        }
        public void SelectHand(int index)
        {
            if(handoff||Match.IsOver)return;
            selectedHand=index;selectedAttacker=-1;Refresh();
        }
        public void PlaySelected()
        {
            if(handoff)return;
            if(Match.Play(selectedHand))selectedHand=-1;Refresh();
        }
        public void SelectAttacker(int index)
        {
            if(handoff||Match.IsOver)return;
            selectedAttacker=index;selectedHand=-1;Refresh();
        }
        public void AttackTarget(int index)
        {
            if(handoff)return;
            if(Match.Attack(selectedAttacker,index))selectedAttacker=-1;Refresh();
        }
        public void EndTurn()
        {
            if(handoff||!Match.EndTurn())return;
            selectedHand=selectedAttacker=-1;handoff=!Match.IsOver;Refresh();
            passText.text="Ekranı Oyuncu "+(Match.CurrentPlayer+1)+"'e devret.\nHazır olduğunda elini aç.";passPanel.SetActive(handoff);
        }
        public void ConfirmHandoff(){handoff=false;passPanel.SetActive(false);Refresh();}
        public void Refresh()
        {
            var own=Match.GetPlayer(Match.CurrentPlayer);var enemy=Match.GetPlayer(1-Match.CurrentPlayer);
            ownInfo.text=$"OYUNCU {Match.CurrentPlayer+1}   ♥ {own.Health}   •   MANA {own.Mana}/{own.MaxMana}   •   DESTE {own.DeckCount}   •   EL {own.Hand.Count}/10";
            enemyInfo.text=$"OYUNCU {2-Match.CurrentPlayer}   ♥ {enemy.Health}   •   EL {enemy.Hand.Count}   •   DESTE {enemy.DeckCount}";
            turnInfo.text=Match.IsOver?"MAÇ BİTTİ":$"TUR {Match.Turn}\nOYUNCU {Match.CurrentPlayer+1}";
            status.text=Match.Status;selection.text="El kartı → Oyna\n\nHazır masa kartı → Rakip kart / kahraman";
            if(selectedHand>=0&&selectedHand<own.Hand.Count)
            {
                var c=own.Hand[selectedHand];selection.text=$"{c.cardName}\n\n{MultiCardMatch.CostOf(c)} MANA\n{MultiCardMatch.AttackOf(c)} ATK / {MultiCardMatch.HealthOf(c)} CAN";
            }
            if(selectedAttacker>=0&&selectedAttacker<own.Board.Count)selection.text=own.Board[selectedAttacker].Ready?"SALDIRI HEDEFİ SEÇ\n\nRakip karta veya kahramana tıkla.":"Bu kart sonraki tur saldırabilir.";
            Clear(hand);Clear(ownBoard);Clear(enemyBoard);
            if(!handoff)for(int i=0;i<own.Hand.Count;i++)
            {
                int slot=i;var c=own.Hand[i];CardButton("HandCard_"+i,hand,c,i,10,MultiCardMatch.HealthOf(c),MultiCardMatch.AttackOf(c),selectedHand==i,Match.CanPlay(i)?green:ArcaneMainMenu.Panel,()=>SelectHand(slot),true,"");
            }
            for(int i=0;i<own.Board.Count;i++)
            {
                int slot=i;var u=own.Board[i];CardButton("OwnUnit_"+i,ownBoard,u.Card,i,7,u.Health,u.Attack,selectedAttacker==i,u.Ready?green:ArcaneMainMenu.Panel,()=>SelectAttacker(slot),false,u.Ready?"HAZIR":"BEKLİYOR");
            }
            for(int i=0;i<enemy.Board.Count;i++)
            {
                int slot=i;var u=enemy.Board[i];CardButton("EnemyUnit_"+i,enemyBoard,u.Card,i,7,u.Health,u.Attack,false,new Color(.24f,.11f,.16f),()=>AttackTarget(slot),false,"");
            }
            if(own.Board.Count==0)ArcaneMainMenu.Label("Empty",ownBoard,"Elinden bir kart seçip masaya oyna.",.04f,.30f,.96f,.70f,18,new Color(.45f,.60f,.60f));
            if(enemy.Board.Count==0)ArcaneMainMenu.Label("Empty",enemyBoard,"Rakip masası boş",.04f,.30f,.96f,.70f,18,new Color(.60f,.45f,.52f));
            play.interactable=!handoff&&Match.CanPlay(selectedHand);
            draw.interactable=!handoff&&Match.CanExtraDraw;endTurn.interactable=!handoff&&!Match.IsOver;
            enemyHero.interactable=!handoff&&!Match.IsOver&&selectedAttacker>=0&&selectedAttacker<own.Board.Count&&own.Board[selectedAttacker].Ready;
        }
        private void CardButton(string name,Transform parent,CardData card,int index,int slots,int hp,int atk,bool selected,Color color,UnityEngine.Events.UnityAction action,bool isHand,string ready)
        {
            float width=.97f/slots,x=.015f+index*width;
            var button=ArcaneMainMenu.Action(name,parent,"",x,.045f,x+width-.008f,.88f,action);
            button.GetComponent<Image>().color=selected?new Color(.30f,.25f,.13f):color;
            if(selected){var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=ArcaneMainMenu.Gold;outline.effectDistance=new Vector2(2,2);}
            var rect=button.transform;var art=ArcaneMainMenu.Box("Portrait",rect,.06f,.38f,.94f,.96f,Color.white).GetComponent<Image>();
            art.sprite=Portrait(card);art.preserveAspect=true;art.enabled=art.sprite!=null;
            var label=ArcaneMainMenu.Label("Name",rect,card.cardName,.04f,.21f,.96f,.39f,12,Color.white);label.resizeTextForBestFit=true;label.resizeTextMinSize=8;label.resizeTextMaxSize=12;
            ArcaneMainMenu.Label("Stats",rect,$"{atk} ATK  /  {hp} CAN",.02f,.025f,.98f,.18f,isHand?12:15,ArcaneMainMenu.Gold);
            if(isHand)
            {
                var cost=ArcaneMainMenu.Box("Cost",rect,0,.75f,.30f,1,blue);ArcaneMainMenu.Label("Value",cost,MultiCardMatch.CostOf(card).ToString(),0,0,1,1,17,Color.white);
            }
            else if(!string.IsNullOrEmpty(ready))ArcaneMainMenu.Label("Ready",rect,ready,0,.87f,1,1,10,Color.white);
        }
        private Sprite Portrait(CardData card)
        {
            if(card.cardIcon==null)return null;
            if(portraits.TryGetValue(card.cardID,out var result))return result;
            var r=card.cardIcon.rect;result=Sprite.Create(card.cardIcon.texture,new Rect(r.x+r.width*.10f,r.y+r.height*.31f,r.width*.80f,r.height*.54f),new Vector2(.5f,.5f));portraits.Add(card.cardID,result);return result;
        }
        private static void Clear(Transform root)
        {
            for(int i=root.childCount-1;i>=0;i--){var go=root.GetChild(i).gameObject;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        }
        private void OnDestroy(){foreach(var sprite in portraits.Values){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}}
    }
}
