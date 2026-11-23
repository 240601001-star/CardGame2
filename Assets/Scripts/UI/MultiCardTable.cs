using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core;
using CardGame.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace CardGame.UI
{
    public sealed class MultiCardTable : MonoBehaviour
    {
        public const string SceneName="MultiCardScene";
        public MultiCardMatch Match { get; private set; }
        private Transform canvas,hand,ownBoard,enemyBoard;
        private TMP_Text ownInfo,enemyInfo,turnInfo,status,selection,passText,scanStatus;
        private WebCamArucoManager scanner;
        private readonly MultiCardScanGate scanGate=new MultiCardScanGate();
        private bool scannerBound;
        private string scanMessage="Önce kamera alanını boşalt.";
        private Button play,draw,endTurn,enemyHero;
        private GameObject passPanel,leavePanel;
        private int selectedHand=-1,selectedAttacker=-1;
        private bool handoff;
        private PhysicalDeckRegistry deckRegistry;
        private GameObject setupPanel,discardPanel,deckResetPanel,rulesPanel;
        private TMP_Text setupHeading,setupCount,setupLeft,setupRight,setupMessage,discardLeft,discardRight,setupHelp;
        private Button confirmDeck,undoDeck,discardButton;
        public PhysicalDeckRegistry DeckRegistration => deckRegistry;
        private bool ModalOpen => (leavePanel!=null&&leavePanel.activeSelf)||(discardPanel!=null&&discardPanel.activeSelf)||(deckResetPanel!=null&&deckResetPanel.activeSelf)||(rulesPanel!=null&&rulesPanel.activeSelf);
        private bool CanInteract => Match!=null&&!handoff&&!ModalOpen;
        private ArcaneUIFeedback feedback;
        private readonly Dictionary<int,RectTransform> ownViews=new Dictionary<int,RectTransform>(),enemyViews=new Dictionary<int,RectTransform>();
        private bool CanAttackTarget => CanInteract&&Match.CanAct&&selectedAttacker>=0&&selectedAttacker<Match.GetPlayer(Match.CurrentPlayer).Board.Count&&Match.GetPlayer(Match.CurrentPlayer).Board[selectedAttacker].Ready;
        private readonly Dictionary<int,Sprite> portraits=new Dictionary<int,Sprite>();
        private readonly Color blue=new Color(.18f,.40f,.66f),green=new Color(.17f,.42f,.32f);
        private void Awake(){Initialize();}
        public void Initialize()
        {
            if(canvas!=null)return;
            canvas=ArcaneMainMenu.CreateCanvas("MultiCardCanvas",10);canvas.SetParent(transform,false);canvas=ArcaneUIFactory.SafeContent(canvas);
            ArcaneMainMenu.Box("Background",canvas,0,0,1,1,ArcaneMainMenu.Ink);
            ArcaneMainMenu.Label("Title",canvas,"ARCANE DUEL  /  FİZİKSEL DESTE",.025f,.925f,.65f,.99f,25,ArcaneMainMenu.Gold,TextAnchor.MiddleLeft);
            ArcaneMainMenu.Action("TableMenuButton",canvas,"MENÜ",.91f,.935f,.985f,.985f,()=>{feedback.Clear();scanGate.Lock();leavePanel.SetActive(true);});
            ArcaneMainMenu.Action("DeckSetupButton",canvas,"DESTE",.82f,.935f,.90f,.985f,RequestDeckReset);
            ArcaneMainMenu.Action("RulesButton",canvas,"KURALLAR",.68f,.935f,.79f,.985f,ShowRules);
            enemyInfo=ArcaneMainMenu.Label("EnemyInfo",canvas,"",.025f,.84f,.48f,.92f,19,Color.white,TextAnchor.MiddleLeft);
            enemyInfo.rectTransform.anchorMax=new Vector2(.44f,.92f);
            enemyHero=ArcaneMainMenu.Action("EnemyHeroButton",canvas,"RAKİP KAHRAMAN",.45f,.845f,.655f,.915f,()=>AttackTarget(-1));
            discardButton=ArcaneMainMenu.Action("DiscardButton",canvas,"KULLANILMIŞ",.67f,.845f,.79f,.915f,ShowDiscard);
            enemyBoard=ArcaneMainMenu.Box("EnemyBoard",canvas,.025f,.59f,.79f,.825f,new Color(.14f,.09f,.14f));
            ArcaneMainMenu.Label("EnemyBoardHeading",canvas,"RAKİP MASASI",.04f,.784f,.3f,.814f,12,new Color(.73f,.57f,.62f),TextAnchor.MiddleLeft);
            ArcaneMainMenu.Box("Divider",canvas,.025f,.558f,.79f,.56f,new Color(.45f,.35f,.18f));
            ownBoard=ArcaneMainMenu.Box("OwnBoard",canvas,.025f,.31f,.79f,.545f,new Color(.075f,.14f,.15f));
            ArcaneMainMenu.Label("OwnBoardHeading",canvas,"SENİN MASAN",.04f,.504f,.3f,.534f,12,new Color(.52f,.74f,.70f),TextAnchor.MiddleLeft);
            ownInfo=ArcaneMainMenu.Label("OwnInfo",canvas,"",.025f,.25f,.79f,.305f,19,ArcaneMainMenu.Gold,TextAnchor.MiddleLeft);
            hand=ArcaneMainMenu.Box("Hand",canvas,.025f,.025f,.79f,.245f,ArcaneMainMenu.Panel);
            var sidebar=ArcaneMainMenu.Box("Controls",canvas,.815f,.025f,.985f,.92f,ArcaneMainMenu.Panel);
            turnInfo=ArcaneMainMenu.Label("TurnInfo",sidebar,"",.06f,.90f,.94f,.99f,20,ArcaneMainMenu.Gold);
            status=ArcaneMainMenu.Label("Status",sidebar,"",.07f,.44f,.93f,.56f,13,Color.white);
            selection=ArcaneMainMenu.Label("Selection",sidebar,"Bir el kartı seç.",.07f,.30f,.93f,.44f,13,Color.white);
            play=ArcaneMainMenu.Action("PlayCardButton",sidebar,"KARTI OYNA",.07f,.235f,.93f,.30f,PlaySelected);
            draw=ArcaneMainMenu.Action("DrawCardButton",sidebar,"KART ÇEK • 1 MANA",.07f,.16f,.93f,.225f,RequestExtraDraw);
            endTurn=ArcaneMainMenu.Action("EndTurnButton",sidebar,"TURU BİTİR",.07f,.085f,.93f,.15f,EndTurn);
            ArcaneMainMenu.Action("RestartTableButton",sidebar,"YENİ MAÇ",.07f,.015f,.93f,.075f,Restart);
            foreach(var button in new[]{play,draw,endTurn})button.GetComponentInChildren<TMP_Text>().fontSize=22;
            var cameraBox=ArcaneMainMenu.Box("CameraPreviewBox",sidebar,.07f,.69f,.93f,.89f,Color.black);
            var previewObject=new GameObject("MultiCardCameraPreview",typeof(RectTransform),typeof(RawImage));
            var cameraViewport=ArcaneMainMenu.Box("CameraViewport",cameraBox,.09f,.12f,.91f,.88f,Color.clear);
            previewObject.transform.SetParent(cameraViewport,false);
            var preview=previewObject.GetComponent<RawImage>();preview.raycastTarget=false;preview.enabled=false;
            ArcaneMainMenu.Rect(preview.rectTransform,0,0,1,1);
            var aspect=previewObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=16f/9f;
            scanStatus=ArcaneMainMenu.Label("CameraScanStatus",sidebar,"Kamera başlatılıyor…",.07f,.56f,.93f,.69f,12,ArcaneMainMenu.Gold);
            var cameraObject=new GameObject("MultiCardWebcam");cameraObject.transform.SetParent(transform,false);
            scanner=cameraObject.AddComponent<WebCamArucoManager>();scanner.ConfigureDisplay(preview);BindScanner();BuildPhysicalPanels();
            passPanel=Overlay("PassPanel","SIRA DİĞER OYUNCUDA",out passText);
            ArcaneMainMenu.Action("ReadyButton",passPanel.transform,"HAZIRIM • ELİMİ GÖSTER",.31f,.31f,.69f,.40f,ConfirmHandoff);
            leavePanel=Overlay("TableLeavePanel","ANA MENÜYE DÖN?",out var leaveText);
            leaveText.text="Devam eden maç sona erecek.";
            ArcaneMainMenu.Action("CancelTableLeaveButton",leavePanel.transform,"VAZGEÇ",.28f,.31f,.48f,.40f,()=>leavePanel.SetActive(false));
            ArcaneMainMenu.Action("ConfirmTableLeaveButton",leavePanel.transform,"ANA MENÜ",.52f,.31f,.72f,.40f,()=>SceneManager.LoadSceneAsync(ArcaneMainMenu.SceneName));
            feedback=canvas.gameObject.AddComponent<ArcaneUIFeedback>();leavePanel.SetActive(false);ResetDecks();
        }
        private GameObject Overlay(string name,string title,out TMP_Text description)
        {
            var overlay=ArcaneMainMenu.Box(name,canvas,0,0,1,1,new Color(.025f,.03f,.065f,.99f));overlay.GetComponent<Image>().raycastTarget=true;
            ArcaneMainMenu.Label("Title",overlay,title,.15f,.60f,.85f,.73f,35,ArcaneMainMenu.Gold);
            description=ArcaneMainMenu.Label("Description",overlay,"",.18f,.45f,.82f,.59f,22,Color.white);return overlay.gameObject;
        }
        private void BuildPhysicalPanels()
        {
            setupPanel=ArcaneMainMenu.Box("PhysicalDeckSetup",canvas,.025f,.11f,.79f,.915f,ArcaneMainMenu.Panel).gameObject;
            var root=setupPanel.transform;
            setupHeading=ArcaneMainMenu.Label("SetupHeading",root,"DESTE KAYDI",.055f,.82f,.75f,.96f,30,ArcaneMainMenu.Gold);
            setupCount=ArcaneMainMenu.Label("SetupCount",root,"0 / 20",.76f,.82f,.945f,.96f,28,ArcaneMainMenu.Gold);
            setupHelp=ArcaneMainMenu.Label("SetupHelp",root,"Her oyuncu 20 farklı kart kaydeder. Kamera alanında tek kart olsun.\nAynı kart iki desteye eklenemez. Kayıttan sonra fiziksel olarak karıştırın.",.055f,.69f,.945f,.81f,17,Color.white);
            setupLeft=ArcaneMainMenu.Label("SetupCardsLeft",root,"",.055f,.24f,.48f,.68f,20,Color.white,TextAnchor.UpperLeft);
            setupRight=ArcaneMainMenu.Label("SetupCardsRight",root,"",.52f,.24f,.945f,.68f,20,Color.white,TextAnchor.UpperLeft);
            setupMessage=ArcaneMainMenu.Label("SetupMessage",root,"",.055f,.15f,.945f,.23f,18,ArcaneMainMenu.Gold);
            undoDeck=ArcaneMainMenu.Action("UndoDeckCardButton",root,"SON KAYDI GERİ AL",.055f,.035f,.40f,.13f,UndoRegisteredCard);
            confirmDeck=ArcaneMainMenu.Action("ConfirmDeckButton",root,"DESTENİ KAYDET",.45f,.035f,.945f,.13f,ConfirmRegisteredDeck);
            discardPanel=ArcaneMainMenu.Box("DiscardPanel",canvas,0,0,1,1,new Color(.025f,.03f,.065f,.99f)).gameObject;discardPanel.GetComponent<Image>().raycastTarget=true;
            ArcaneMainMenu.Label("Title",discardPanel.transform,"KULLANILMIŞ KARTLAR",.10f,.82f,.90f,.95f,34,ArcaneMainMenu.Gold);
            discardLeft=ArcaneMainMenu.Label("Player1Discard",discardPanel.transform,"",.10f,.24f,.47f,.80f,17,Color.white,TextAnchor.UpperLeft);
            discardRight=ArcaneMainMenu.Label("Player2Discard",discardPanel.transform,"",.53f,.24f,.90f,.80f,17,Color.white,TextAnchor.UpperLeft);
            ArcaneMainMenu.Label("Help",discardPanel.transform,"Ölen ve el doluyken yakılan fiziksel kartları\nkendi kullanılmış kart yığınınıza ayırın.",.10f,.15f,.90f,.23f,17,Color.white);
            ArcaneMainMenu.Action("CloseDiscardButton",discardPanel.transform,"MASAYA DÖN",.38f,.045f,.62f,.135f,()=>{discardPanel.SetActive(false);scanGate.Lock();});discardPanel.SetActive(false);
            deckResetPanel=Overlay("DeckResetConfirmation","DESTELERİ YENİDEN KAYDET?",out var warning);
            warning.text="Mevcut maç ve deste kayıtları sıfırlanacak.\nAynı destelerle tekrar oynamak için masadaki YENİ MAÇ düğmesini kullan.";
            ArcaneMainMenu.Action("CancelDeckResetButton",deckResetPanel.transform,"VAZGEÇ",.25f,.25f,.48f,.35f,()=>deckResetPanel.SetActive(false));
            ArcaneMainMenu.Action("ConfirmDeckResetButton",deckResetPanel.transform,"YENİ DESTE",.52f,.25f,.75f,.35f,ResetDecks);deckResetPanel.SetActive(false);BuildRulesPanel();
        }
        private static string CardNames(IReadOnlyList<CardData> cards,int offset,int count)
        {return string.Join("\n",cards.Skip(offset).Take(count).Select((c,i)=>(offset+i+1)+". "+c.cardName));}
        private void RefreshRegistration()
        {
            setupPanel.SetActive(!handoff);setupHeading.text="OYUNCU "+(deckRegistry.CurrentPlayer+1)+" · DESTE KAYDI";
            setupCount.text=deckRegistry.Count+" / "+PhysicalDeckRegistry.DeckSize;
            var cards=deckRegistry.GetDeck(deckRegistry.CurrentPlayer);
            setupLeft.text=cards.Count==0?"Henüz kart kaydedilmedi.":DeckNames(cards,0,10);setupRight.text=DeckNames(cards,10,10);setupMessage.text=deckRegistry.Status;
            double average=cards.Count==0?0:cards.Average(c=>MultiCardMatch.CostOf(c));int cheap=cards.Count(c=>MultiCardMatch.CostOf(c)<=MultiCardRules.StartingMana);
            setupHelp.text="20 farklı kartı tek tek okut. Aynı kart iki desteye eklenemez.\nKayıttan sonra fiziksel olarak karıştır.\nMana ortalaması: "+average.ToString("F1")+" · "+MultiCardRules.StartingMana+" ve altı: "+cheap+" kart. Ucuz ve pahalı kartları karıştır.";
            confirmDeck.interactable=deckRegistry.CanConfirm&&!handoff;undoDeck.interactable=cards.Count>0&&!handoff;
            ownInfo.text="Destendeki kartları tek tek okut. Her karttan sonra kamera boş kalsın.";enemyInfo.text="DESTE KAYDI · 20 FARKLI KART";
            turnInfo.text="OYUNCU "+(deckRegistry.CurrentPlayer+1);status.text="Fiziksel desteni kaydet.";selection.text="Kartları kendi seçtiğin sırayla kaydet.\nKaydın ardından fiziksel olarak karıştır.";
            play.interactable=draw.interactable=endTurn.interactable=enemyHero.interactable=discardButton.interactable=false;
            ArcaneUIHighlight.Show(enemyHero.gameObject,ArcaneUIHighlight.Kind.None);
            Clear(hand);Clear(ownBoard);Clear(enemyBoard);ownViews.Clear();enemyViews.Clear();
        }
        private static string DeckNames(IReadOnlyList<CardData> cards,int offset,int count)
        {return string.Join("\n",cards.Skip(offset).Take(count).Select((c,i)=>(offset+i+1)+". "+c.cardName+" · "+MultiCardMatch.CostOf(c)+" MANA"));}
        private void BuildRulesPanel()
        {
            rulesPanel=ArcaneMainMenu.Box("MultiCardRules",canvas,0,0,1,1,new Color(.025f,.03f,.065f,.99f)).gameObject;rulesPanel.GetComponent<Image>().raycastTarget=true;rulesPanel.GetComponent<Image>().sprite=null;rulesPanel.GetComponent<Image>().color=new Color(.025f,.02f,.018f,1);
            ArcaneMainMenu.Box("RulesFrame",rulesPanel.transform,.04f,.035f,.96f,.965f,ArcaneMainMenu.Panel);
            ArcaneMainMenu.Label("Title",rulesPanel.transform,"FİZİKSEL MASA KURALLARI",.10f,.83f,.90f,.95f,32,ArcaneMainMenu.Gold);
            string first="BASILI DEĞERLER\nKart üzerindeki HP ve ATK aynen kullanılır.\nMavi köşedeki sayı bu modun mana bedelidir.\n\nKAHRAMAN\nHer oyuncu "+MultiCardRules.HeroHealth+" canla başlar.\nRakip kartı veya kahramanı hedef seçebilirsin.\n\nSAVAŞ\nYeni oynanan kart o tur saldıramaz.\nHer birlik kendi turunda bir kez saldırır.\nKartlar karşılıklı ve aynı anda hasar verir.\nÖlen kartı kullanılmış yığınına ayır.";
            string second="MANA\nİlk oyuncu "+MultiCardRules.StartingMana+", ikinci oyuncu "+MultiCardRules.SecondStartingMana+" mana ile başlar.\nHer kendi turunda +1; en fazla "+MultiCardRules.MaximumMana+" mana.\nEk çekim 1 mana ve turda bir kezdir.\n\nEL VE DESTE\nİki oyuncu da "+MultiCardRules.FirstOpeningCards+" başlangıç kartı okutur.\nİkinci oyuncu ilk turunda ayrıca 1 kart çeker.\nHer tur çektiğin kartı oynamadan önce okut.\n20 kartlık deste · 10 kartlık el · 7 masa kartı.\n\nDESTE BİTİNCE\nYorgunluk hasarı "+MultiCardRules.FatigueStep+", "+(MultiCardRules.FatigueStep*2)+", "+(MultiCardRules.FatigueStep*3)+" diye artar.";
            ArcaneMainMenu.Label("PrintedStatsRules",rulesPanel.transform,first,.08f,.22f,.48f,.80f,19,Color.white,TextAnchor.UpperLeft);
            ArcaneMainMenu.Label("ManaDrawRules",rulesPanel.transform,second,.53f,.22f,.93f,.80f,19,Color.white,TextAnchor.UpperLeft);
            ArcaneMainMenu.Action("CloseRulesButton",rulesPanel.transform,"MASAYA DÖN",.38f,.055f,.62f,.15f,()=>{rulesPanel.SetActive(false);scanGate.Lock();});rulesPanel.SetActive(false);
        }
        public void ShowRules()
        {if(handoff||ModalOpen)return;if(feedback!=null)feedback.Clear();scanGate.Lock();rulesPanel.SetActive(true);}
        public void RequestDeckReset()
        {if(handoff||ModalOpen)return;if(feedback!=null)feedback.Clear();scanGate.Lock();deckResetPanel.SetActive(true);}
        public void ShowDiscard()
        {
            if(!CanInteract)return;if(feedback!=null)feedback.Clear();scanGate.Lock();
            discardLeft.text="OYUNCU 1 · "+Match.GetPlayer(0).Discard.Count+" KART\n\n"+CardNames(Match.GetPlayer(0).Discard,0,20);
            discardRight.text="OYUNCU 2 · "+Match.GetPlayer(1).Discard.Count+" KART\n\n"+CardNames(Match.GetPlayer(1).Discard,0,20);
            discardPanel.SetActive(true);
        }
        public void ResetDecks()
        {
            if(feedback!=null)feedback.Clear();
            deckRegistry=new PhysicalDeckRegistry(Resources.LoadAll<CardData>("Cards"));Match=null;
            selectedHand=selectedAttacker=-1;handoff=false;scanGate.Lock();scanMessage="Önce kamerayı boşalt; sonra destendeki ilk kartı okut.";
            passPanel.SetActive(false);discardPanel.SetActive(false);deckResetPanel.SetActive(false);rulesPanel.SetActive(false);Refresh();
        }
        public void Restart()
        {
            if(deckRegistry==null||!deckRegistry.IsComplete){ResetDecks();return;}
            if(feedback!=null)feedback.Clear();
            Match=MultiCardMatch.CreatePhysical(deckRegistry.GetDeck(0),deckRegistry.GetDeck(1));
            selectedHand=selectedAttacker=-1;scanGate.Lock();handoff=true;setupPanel.SetActive(false);discardPanel.SetActive(false);
            passText.text="Tüm kartları kendi destelerinize geri alın ve fiziksel olarak karıştırın.\nOyuncu 1, destenden "+MultiCardRules.FirstOpeningCards+" kart çek; hazır olduğunda tek tek okut.";
            passPanel.SetActive(true);Refresh();
        }
        public void ConfirmRegisteredDeck()
        {
            if(Match!=null||handoff||ModalOpen||!deckRegistry.Confirm()){Refresh();return;}
            scanGate.Lock();
            if(deckRegistry.IsComplete){Restart();return;}
            handoff=true;passText.text="Oyuncu 1: desteni kenara ayır.\nEkranı Oyuncu 2'ye devret; kendi 20 kartını kaydetsin.";passPanel.SetActive(true);Refresh();
        }
        public void UndoRegisteredCard()
        {
            if(Match!=null||handoff||ModalOpen)return;
            deckRegistry.UndoLast();scanGate.Lock();Refresh();
        }
        public void RequestExtraDraw()
        {
            if(!CanInteract||!Match.CanExtraDraw)return;
            if(Match.ExtraDraw()){selectedHand=selectedAttacker=-1;scanGate.Lock();}Refresh();
        }
        public void SelectHand(int index)
        {
            if(!CanInteract||!Match.CanAct)return;
            selectedHand=index;selectedAttacker=-1;Refresh();
        }
        public void PlaySelected()
        {
            if(!CanInteract)return;
            bool played=Match.Play(selectedHand);
            if(played){selectedHand=-1;scanGate.Lock();}Refresh();
            if(played&&ownViews.TryGetValue(Match.GetPlayer(Match.CurrentPlayer).Board.Count-1,out var view))feedback.Played(ArcaneUIFeedback.Center(view));
        }
        public void SelectAttacker(int index)
        {
            if(!CanInteract||!Match.CanAct)return;
            selectedAttacker=index;selectedHand=-1;Refresh();
        }
        public void AttackTarget(int index)
        {
            if(!CanInteract)return;
            var own=Match.GetPlayer(Match.CurrentPlayer);var enemy=Match.GetPlayer(1-Match.CurrentPlayer);
            // Snapshot the visible actors before the rules remove lethal units / rebuild the UI.
            var attacker=selectedAttacker>=0&&selectedAttacker<own.Board.Count?own.Board[selectedAttacker]:null;
            var target=index>=0&&index<enemy.Board.Count?enemy.Board[index]:null;
            Vector3 from=ownViews.TryGetValue(selectedAttacker,out var sourceView)?ArcaneUIFeedback.Center(sourceView):ArcaneUIFeedback.Center(ownBoard);
            Vector3 to=index<0?ArcaneUIFeedback.Center(enemyHero.transform):enemyViews.TryGetValue(index,out var targetView)?ArcaneUIFeedback.Center(targetView):ArcaneUIFeedback.Center(enemyBoard);
            int oldAttackerHP=attacker!=null?attacker.Health:0,oldTargetHP=target!=null?target.Health:enemy.Health;
            bool accepted=Match.Attack(selectedAttacker,index);
            if(accepted){selectedAttacker=-1;scanGate.Lock();}Refresh();
            if(!accepted)return;
            feedback.Strike(from,to);
            feedback.Damage(oldTargetHP-Mathf.Max(0,target!=null?target.Health:enemy.Health),to,target!=null?target.Card.cardName:"RAKİP KAHRAMAN");
            if(target!=null){feedback.Strike(to,from);feedback.Damage(oldAttackerHP-Mathf.Max(0,attacker.Health),from,attacker.Card.cardName);}
        }
        public void EndTurn()
        {
            if(!CanInteract||!Match.EndTurn())return;
            feedback.Clear();selectedHand=selectedAttacker=-1;scanGate.Lock();handoff=!Match.IsOver;Refresh();
            passText.text="Ekranı Oyuncu "+(Match.CurrentPlayer+1)+"'e devret.\nDestenden 1 kart çek; hazır olduğunda okut.";passPanel.SetActive(handoff);
        }
        public void ConfirmHandoff(){handoff=false;scanGate.Lock();scanMessage="Önce kamera alanını boşalt; sonra tek kart okut.";passPanel.SetActive(false);Refresh();}
        public void Refresh()
        {
            if(Match==null){RefreshRegistration();return;}
            setupPanel.SetActive(false);discardButton.interactable=!handoff;
            if(Match.IsOver)selectedHand=selectedAttacker=-1;
            var own=Match.GetPlayer(Match.CurrentPlayer);var enemy=Match.GetPlayer(1-Match.CurrentPlayer);
            ownInfo.text=$"OYUNCU {Match.CurrentPlayer+1}   ♥ {own.Health}   •   MANA {own.Mana}/{own.MaxMana}   •   DESTE {own.DeckCount}   •   EL {own.Hand.Count}/10   •   KULLANILMIŞ {own.Discard.Count}";
            enemyInfo.text=$"OYUNCU {2-Match.CurrentPlayer}   ♥ {enemy.Health}   •   EL {enemy.Hand.Count}   •   DESTE {enemy.DeckCount}";
            turnInfo.text=Match.IsOver?"MAÇ BİTTİ":Match.OpeningHands?$"BAŞLANGIÇ ELİ\nOYUNCU {Match.CurrentPlayer+1}":$"TUR {Match.Turn}\nOYUNCU {Match.CurrentPlayer+1}";
            status.text=Match.Status;selection.text=Match.PendingDrawCount>0?$"ÇEKME BEKLİYOR\n{Match.PendingDrawCount} kartı tek tek okut.\n\nKartları fiziksel destenin üstünden çek.":"El kartını okut → Oyna\n\nSaldırganı okut → hedefe tıkla";
            if(selectedHand>=0&&selectedHand<own.Hand.Count)
            {
                var c=own.Hand[selectedHand];selection.text=$"{c.cardName}\n\n{MultiCardMatch.CostOf(c)} MANA\n{MultiCardMatch.AttackOf(c)} ATK / {MultiCardMatch.HealthOf(c)} CAN";
            }
            if(selectedAttacker>=0&&selectedAttacker<own.Board.Count)selection.text=own.Board[selectedAttacker].Ready?"SALDIRI HEDEFİ SEÇ\n\nRakip karta veya kahramana tıkla.":"Bu kart sonraki tur saldırabilir.";
            Clear(hand);Clear(ownBoard);Clear(enemyBoard);ownViews.Clear();enemyViews.Clear();
            if(!handoff)for(int i=0;i<own.Hand.Count;i++)
            {
                int slot=i;var c=own.Hand[i];var view=CardButton("HandCard_"+i,hand,c,i,10,MultiCardMatch.HealthOf(c),MultiCardMatch.AttackOf(c),selectedHand==i,Match.CanPlay(i)?green:ArcaneMainMenu.Panel,()=>SelectHand(slot),true,"");
                view.interactable=CanInteract&&Match.CanAct;
                ArcaneUIHighlight.Show(view.gameObject,selectedHand==i?ArcaneUIHighlight.Kind.Selected:Match.CanPlay(i)&&!Match.IsOver?ArcaneUIHighlight.Kind.Playable:ArcaneUIHighlight.Kind.None);
            }
            for(int i=0;i<own.Board.Count;i++)
            {
                int slot=i;var u=own.Board[i];var view=CardButton("OwnUnit_"+i,ownBoard,u.Card,i,7,u.Health,u.Attack,selectedAttacker==i,u.Ready?green:ArcaneMainMenu.Panel,()=>SelectAttacker(slot),false,selectedAttacker==i?"SEÇİLİ":u.Ready?"HAZIR":"BEKLİYOR");
                ownViews[i]=view.GetComponent<RectTransform>();view.interactable=CanInteract&&Match.CanAct;
                ArcaneUIHighlight.Show(view.gameObject,handoff||!Match.CanAct?ArcaneUIHighlight.Kind.None:selectedAttacker==i?ArcaneUIHighlight.Kind.Selected:u.Ready?ArcaneUIHighlight.Kind.Ready:ArcaneUIHighlight.Kind.None);
            }
            for(int i=0;i<enemy.Board.Count;i++)
            {
                int slot=i;var u=enemy.Board[i];var view=CardButton("EnemyUnit_"+i,enemyBoard,u.Card,i,7,u.Health,u.Attack,false,new Color(.24f,.11f,.16f),()=>AttackTarget(slot),false,"");
                enemyViews[i]=view.GetComponent<RectTransform>();view.interactable=CanAttackTarget;
                ArcaneUIHighlight.Show(view.gameObject,CanAttackTarget?ArcaneUIHighlight.Kind.Target:ArcaneUIHighlight.Kind.None);
            }
            if(!handoff&&own.Hand.Count==0)ArcaneMainMenu.Label("EmptyHand",hand,Match.PendingDrawCount>0?"Destenden çektiğin kartları tek tek okut.":"Elinde kart yok.",.04f,.30f,.96f,.70f,18,new Color(.65f,.6f,.5f));
            if(own.Board.Count==0)ArcaneMainMenu.Label("Empty",ownBoard,Match.PendingDrawCount>0?"Önce çekilen kartları okut; sonra hamle yap.":"Elinden bir kart seçip masaya oyna.",.04f,.30f,.96f,.70f,18,new Color(.45f,.60f,.60f));
            if(enemy.Board.Count==0)ArcaneMainMenu.Label("Empty",enemyBoard,"Rakip masası boş",.04f,.30f,.96f,.70f,18,new Color(.60f,.45f,.52f));
            play.interactable=CanInteract&&Match.CanPlay(selectedHand);
            draw.interactable=CanInteract&&Match.CanExtraDraw;endTurn.interactable=CanInteract&&Match.CanAct;
            draw.GetComponentInChildren<TMP_Text>().text=Match.PendingDrawCount>0?"ÇEKİLENİ OKUT":"EK ÇEKİM · 1 MANA";
            enemyHero.interactable=CanAttackTarget;
            enemyHero.GetComponentInChildren<TMP_Text>().text=CanAttackTarget?"HEDEF: RAKİP KAHRAMAN":"RAKİP KAHRAMAN";
            ArcaneUIHighlight.Show(enemyHero.gameObject,CanAttackTarget?ArcaneUIHighlight.Kind.Target:ArcaneUIHighlight.Kind.None);
        }
        private bool CanScan => !handoff&&!ModalOpen&&((Match==null&&deckRegistry!=null)||(Match!=null&&!Match.IsOver));
        private void OnEnable(){BindScanner();}
        private void BindScanner()
        {
            if(scanner==null||scannerBound)return;
            scanner.OnMarkerDetected+=CameraDetected;scanner.OnMarkerLost+=CameraLost;scanner.OnScanFrameCompleted+=CameraFrame;scannerBound=true;
        }
        private void OnDisable()
        {
            if(feedback!=null)feedback.Clear();scanGate.Lock();
            if(scanner==null||!scannerBound)return;
            scanner.OnMarkerDetected-=CameraDetected;scanner.OnMarkerLost-=CameraLost;scanner.OnScanFrameCompleted-=CameraFrame;scannerBound=false;
        }
        private void CameraDetected(int id,Vector2 position)
        {
            if(scanGate.Observe(id,Time.unscaledTime,CanScan))SelectPhysicalCard(id);
        }
        private void CameraLost(int id){scanGate.Lost();}
        private void CameraFrame(int count)
        {
            if(!CanScan){scanGate.Lock();return;}
            bool wasWaiting=scanGate.WaitingForClear;scanGate.Frame(count,Time.unscaledTime);
            if(wasWaiting&&!scanGate.WaitingForClear)scanMessage=Match==null?"Hazır: destene kaydedeceğin bir kartı okut.":Match.PendingDrawCount>0?"Hazır: desteden çektiğin kartı okut.":"Hazır: elindeki veya hazır masa kartını okut.";
            if(count>1)scanMessage="Aynı anda yalnızca bir kart göster.";
        }
        /// <summary>Selects an owned hand card or board attacker; scanning never spends mana or attacks.</summary>
        public bool SelectPhysicalCard(int id)
        {
            if(!CanScan||id<0||id>49)return false;
            if(Match==null)
            {
                bool registered=deckRegistry.Register(id);scanMessage=deckRegistry.Status;Refresh();return registered;
            }
            if(Match.PendingDrawCount>0)
            {
                int before=Match.CurrentPlayer;bool accepted=Match.AcceptPhysicalDraw(id);scanMessage=Match.Status;
                if(accepted)
                {
                    selectedHand=selectedAttacker=-1;scanGate.Lock();
                    if(Match.CurrentPlayer!=before)
                    {
                        feedback.Clear();handoff=true;passText.text=Match.OpeningHands?"Ekranı Oyuncu 2'ye devret.\nDestenden "+MultiCardRules.SecondOpeningCards+" kart çek ve tek tek okut.":"Başlangıç elleri hazır. Ekranı Oyuncu 1'e devret.\nİlk tur başlayacak.";passPanel.SetActive(true);
                    }
                }
                Refresh();return accepted;
            }
            var own=Match.GetPlayer(Match.CurrentPlayer);
            for(int i=0;i<own.Hand.Count;i++)if(own.Hand[i].cardID==id)
            {
                SelectHand(i);scanMessage="Kart seçildi. Oynamak için KARTI OYNA.";return true;
            }
            for(int i=0;i<own.Board.Count;i++)if(own.Board[i].Card.cardID==id&&own.Board[i].Ready)
            {
                SelectAttacker(i);scanMessage="Saldırgan seçildi. Rakip kartı veya kahramanı seç.";return true;
            }
            scanMessage="Kart aktif oyuncunun elinde değil veya masada hazır değil.";return false;
        }
        private void Update()
        {
            if(scanner==null||scanStatus==null)return;
            if(!CanScan){scanGate.Lock();return;}
            if(!scanner.IsCameraRunning)
            {
                scanGate.Lock();scanStatus.text=string.IsNullOrEmpty(scanner.LastError)?"Kamera bekleniyor. Deste kaydı ve kart çekimi için webcam gerekli.":"Kamera kullanılamıyor: "+scanner.LastError;return;
            }
            if(scanner.LastAnalysisTime<0||Time.unscaledTime-scanner.LastAnalysisTime>scanner.VisibilityTimeout)
            {scanGate.Lock();scanStatus.text="Kameradan yeni görüntü bekleniyor.";return;}
            scanStatus.text=(scanGate.WaitingForClear?"Kartı kaldır; kamera boş kalsın.\n":"Bir kartı sabit göster.\n")+scanMessage;
        }
        private Button CardButton(string name,Transform parent,CardData card,int index,int slots,int hp,int atk,bool selected,Color color,UnityEngine.Events.UnityAction action,bool isHand,string ready)
        {
            float width=.97f/slots,x=.015f+index*width;
            var button=ArcaneMainMenu.Action(name,parent,"",x,.045f,x+width-.008f,isHand?.88f:.84f,action);
            button.GetComponent<Image>().color=selected?new Color(1f,.88f,.62f):Color.white;
            var rect=button.transform;var art=ArcaneMainMenu.Box("Portrait",rect,.06f,.43f,.94f,.96f,Color.white).GetComponent<Image>();
            art.sprite=Portrait(card);art.preserveAspect=true;art.enabled=art.sprite!=null;
            var label=ArcaneMainMenu.Label("Name",rect,card.cardName,.04f,.25f,.96f,.43f,12,Color.white);label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=18;
            ArcaneMainMenu.Label("Stats",rect,$"{atk} ATK  /  {hp} CAN",.04f,.065f,.96f,.22f,isHand?11:13,ArcaneMainMenu.Gold);
            if(isHand)
            {
                var cost=ArcaneMainMenu.Box("Cost",rect,0,.75f,.30f,1,blue);ArcaneMainMenu.Label("Value",cost,MultiCardMatch.CostOf(card).ToString(),0,0,1,1,17,Color.white);
            }
            else if(!string.IsNullOrEmpty(ready))ArcaneMainMenu.Label("Ready",rect,ready,0,.87f,1,1,10,Color.white);
            return button;
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
        private void OnDestroy(){OnDisable();foreach(var sprite in portraits.Values){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}}
    }
}
