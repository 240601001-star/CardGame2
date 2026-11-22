using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGame.Core;
using CardGame.Data;

namespace CardGame.UI
{
    /// <summary>HUD for the duel state machine. Never reads or displays a committed secret tactic.</summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Card HP (not player life)")]
        [SerializeField] private Slider player1HealthBar;
        [SerializeField] private TMP_Text player1HealthText;
        [SerializeField] private Slider player2HealthBar;
        [SerializeField] private TMP_Text player2HealthText;
        [Header("Match")]
        [SerializeField] private TMP_Text turnIndicatorText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text scanStatusText;
        [SerializeField] private TMP_Text player1DeckText;
        [SerializeField] private TMP_Text player2DeckText;
        [Header("Player 1 card")]
        [SerializeField] private GameObject cardInfoPanel;
        [SerializeField] private Image cardIconImage;
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text cardStatsText;
        [SerializeField] private TMP_Text player1TacticText;
        [Header("Player 2 card")]
        [SerializeField] private Image player2CardImage;
        [SerializeField] private TMP_Text player2CardNameText;
        [SerializeField] private TMP_Text player2CardStatsText;
        [SerializeField] private TMP_Text player2TacticText;
        [Header("Tactics")]
        [SerializeField] private GameObject tacticPanel;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button defenseButton;
        [SerializeField] private Button focusButton;
        [SerializeField] private TMP_Text battleStatusText;
        [Header("Results")]
        [SerializeField] private GameObject roundResultPanel;
        [SerializeField] private TMP_Text roundResultText;
        [SerializeField] private Button endTurnButton; // reused as Next Round
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [Header("Modular state panels")]
        [SerializeField] private GameObject scannerPanel,battlePanel,handoffPanel,player1PrivacyPanel,player2PrivacyPanel;
        [SerializeField] private GameObject scannerPreviewObject;
        [SerializeField] private TMP_Text handoffText,player1ScoreText,player2ScoreText,scannerCaption;
        [SerializeField] private GameObject tacticGuidePanel;
        private GameManager _game;
        private ArcaneUIFeedback feedback;
        private int feedbackRound=-1,feedbackExchange=-1;
        private readonly int[] previousHP=new int[2];
        private void RefreshFeedback()
        {
            if(!Application.isPlaying)return;
            var left=cardIconImage!=null?cardIconImage.GetComponentInParent<ArcaneCardView>():null;
            var right=player2CardImage!=null?player2CardImage.GetComponentInParent<ArcaneCardView>():null;
            if(left==null||right==null)return;
            if(feedback==null)feedback=left.transform.parent.gameObject.AddComponent<ArcaneUIFeedback>();
            if(!_game.CardsRevealed){feedback.Clear();feedbackRound=-1;feedbackExchange=-1;return;}
            int hp1=_game.GetPlayerHP(0),hp2=_game.GetPlayerHP(1);
            if(feedbackRound==_game.CurrentRound&&_game.ExchangeNumber>feedbackExchange)
            {
                Vector3 a=ArcaneUIFeedback.Center(left.artwork.transform),b=ArcaneUIFeedback.Center(right.artwork.transform);
                if(previousHP[0]>hp1){feedback.Strike(b,a);feedback.Damage(previousHP[0]-hp1,a);}
                if(previousHP[1]>hp2){feedback.Strike(a,b);feedback.Damage(previousHP[1]-hp2,b);}
            }
            feedbackRound=_game.CurrentRound;feedbackExchange=_game.ExchangeNumber;previousHP[0]=hp1;previousHP[1]=hp2;
        }

        private void OnEnable() { Bind(); Refresh(); }
        private void Start()
        {
            if (attackButton != null) attackButton.onClick.AddListener(OnAttack);
            if (defenseButton != null) defenseButton.onClick.AddListener(OnDefense);
            if (focusButton != null) focusButton.onClick.AddListener(OnFocus);
            if (endTurnButton != null) endTurnButton.onClick.AddListener(OnNextRound);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            Bind();
            Refresh();
        }
        private void Bind()
        {
            if (_game == GameManager.Instance) return;
            Unbind();
            _game = GameManager.Instance;
            if (_game != null) _game.OnDuelChanged += Refresh;
        }
        private void Unbind()
        {
            if (_game != null) _game.OnDuelChanged -= Refresh;
            _game = null;
        }
        private void OnDisable() { if(feedback!=null)feedback.Clear();Unbind(); }
        private void OnDestroy()
        {
            Unbind();
            if (attackButton != null) attackButton.onClick.RemoveListener(OnAttack);
            if (defenseButton != null) defenseButton.onClick.RemoveListener(OnDefense);
            if (focusButton != null) focusButton.onClick.RemoveListener(OnFocus);
            if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnNextRound);
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenu);
        }
        private void Update()
        {
            Bind();
            // Confirmation/camera diagnostics can change on every fresh frame, not just state transitions.
            if (_game != null)
            {
                var camera = FindCamera();
                Set(scanStatusText, _game.GetMarkerStatus(camera != null ? camera.DetectedMarkerID : -1) +
                    (camera == null || !camera.IsCameraRunning ? "\nKamera bekleniyor / kullanılamıyor." : ""));
            }
        }
        private WebCamArucoManager _camera;
        private WebCamArucoManager FindCamera()
        {
            if (_camera == null) _camera = FindAnyObjectByType<WebCamArucoManager>();
            return _camera;
        }
        public void Refresh()
        {
            if (_game == null) return;
            Set(roundText, $"RAUND {_game.CurrentRound} / {MatchScore.MaxRounds}");
            Set(scoreText, $"OYUNCU 1   {_game.Score.Player1}  :  {_game.Score.Player2}   OYUNCU 2");
            Set(turnIndicatorText, _game.State == DuelState.GameOver ? "MAÇ TAMAMLANDI" :
                _game.State == DuelState.ResolvingBattle ? "DÜELLO" : _game.State == DuelState.RoundResult ? "RAUND SONUCU" : $"OYUNCU {_game.CurrentTurnPlayer + 1}");
            ShowCard(0, cardIconImage, cardNameText, cardStatsText, player1TacticText, player1HealthBar, player1HealthText);
            ShowCard(1, player2CardImage, player2CardNameText, player2CardStatsText, player2TacticText, player2HealthBar, player2HealthText);
            Set(player1ScoreText,_game.Score.Player1.ToString());Set(player2ScoreText,_game.Score.Player2.ToString());
            bool scanning=_game.State==DuelState.WaitingForPlayer1Card||_game.State==DuelState.WaitingForPlayer2Card;
            bool tactics=_game.State==DuelState.WaitingForPlayer1Tactic||_game.State==DuelState.WaitingForPlayer2Tactic;
            bool privacy=scanning&&_game.AwaitingCardRemoval;
            Set(scannerCaption,tactics?"TAKTİĞİNİ SEÇ":privacy?"OYUNCU DEĞİŞİMİ":"KARTI TARA");
            if(tacticGuidePanel!=null)tacticGuidePanel.SetActive(tactics);
            if(scannerPanel!=null)scannerPanel.SetActive(scanning||tactics);
            if(scannerPreviewObject!=null)scannerPreviewObject.SetActive(scanning&&!privacy);
            if(battlePanel!=null)battlePanel.SetActive(_game.State==DuelState.ResolvingBattle);
            if(handoffPanel!=null)handoffPanel.SetActive(privacy);
            Set(handoffText,$"OYUNCU {_game.CurrentTurnPlayer+1}\nKartları kaldır ve kamerayı boşalt.");
            if(player1PrivacyPanel!=null)player1PrivacyPanel.SetActive(_game.GetVisibleCard(0)==null);
            if(player2PrivacyPanel!=null)player2PrivacyPanel.SetActive(_game.GetVisibleCard(1)==null);
            if (tacticPanel != null) tacticPanel.SetActive(_game.CanChooseTactic);
            if (attackButton != null) attackButton.interactable = _game.CanChooseTactic;
            if (defenseButton != null) defenseButton.interactable = _game.CanChooseTactic;
            if (focusButton != null) focusButton.interactable = _game.CanChooseTactic;
            Set(battleStatusText, _game.State == DuelState.ResolvingBattle ?
                $"Eşzamanlı saldırı\n{_game.ExchangeNumber} / {_game.ExchangeCount}\n\nHasar: {_game.LastBattle.Player1Damage} / {_game.LastBattle.Player2Damage}" :
                _game.CardsRevealed ? $"Düello tamamlandı\n{_game.ExchangeNumber} eşzamanlı saldırı\n\nHasar: {_game.LastBattle.Player1Damage} / {_game.LastBattle.Player2Damage}" :
                _game.CanChooseTactic ? "Taktiğinizi gizlice seçin\n\nSaldırı > Odak\nOdak > Savunma\nSavunma > Saldırı" : "ARCANE DUEL\n\nKartlar hazır olduğunda\ndüello başlayacak.");
            if (roundResultPanel != null) roundResultPanel.SetActive(_game.State == DuelState.RoundResult);
            Set(roundResultText, _game.StatusMessage);
            if (endTurnButton != null) endTurnButton.interactable = _game.CanContinueRound;
            if (gameOverPanel != null) gameOverPanel.SetActive(_game.State == DuelState.GameOver);
            Set(gameOverText, !string.IsNullOrEmpty(_game.ConfigurationError) ? _game.ConfigurationError :
                (_game.Score.Winner == -1 ? "MAÇ BERABERE" : $"OYUNCU {_game.Score.Winner + 1} MAÇI KAZANDI") +
                $"   •   {_game.Score.Player1} – {_game.Score.Player2}\n{_game.StatusMessage}");
            Set(player1DeckText, DeckSummary(0));
            Set(player2DeckText, DeckSummary(1));
            RefreshFeedback();
        }
        private void ShowCard(int player, Image image, TMP_Text nameText, TMP_Text stats, TMP_Text tacticText, Slider bar, TMP_Text hpText)
        {
            CardData card = _game.GetVisibleCard(player);
            bool reveal = _game.CardsRevealed;
            Set(nameText, card != null ? card.cardName : "KART GİZLİ / BEKLENİYOR");
            Set(stats, card != null ? $"ATK {card.attackPower}   •   HP {card.health}" : "Seçimler tamamlanınca açıklanır");
            if (image != null) { image.sprite = card != null ? card.cardIcon : null; image.enabled = image.sprite != null; }
            Set(tacticText, reveal ? TacticLabel(_game.GetRevealedTactic(player).Value) : "TAKTİK GİZLİ");
            if (bar != null)
            {
                bar.gameObject.SetActive(reveal);
                bar.minValue = 0; bar.maxValue = card != null ? card.health : 1;
                bar.value = reveal ? _game.GetPlayerHP(player) : 0;
            }
            Set(hpText, reveal ? $"{_game.GetPlayerHP(player)} / {card.health} HP" : "");
        }
        private string DeckSummary(int player)
        {
            string text = $"OYUNCU {player + 1} • DESTE\n";
            for (int id = 0; id < 10; id++)
                if (_game.OwnsCard(player, id)) text += $"{id}{(_game.HasUsedCard(id) ? " ×" : "")}   ";
            return text;
        }
        public static string TacticLabel(Tactic tactic) => tactic == Tactic.Attack ? "SALDIRI" : tactic == Tactic.Defense ? "SAVUNMA" : "ODAK";
        private static void Set(TMP_Text text, string value) { if (text != null) text.text = value; }
        private void OnAttack() { _game?.SelectTactic(Tactic.Attack); }
        private void OnDefense() { _game?.SelectTactic(Tactic.Defense); }
        private void OnFocus() { _game?.SelectTactic(Tactic.Focus); }
        private void OnNextRound() { _game?.ContinueRound(); }
        private void OnRestart() { _game?.RestartGame(); }
        private void OnMainMenu() { FindAnyObjectByType<ArcaneSceneNavigation>()?.ReturnToMenu(); }
    }
}
