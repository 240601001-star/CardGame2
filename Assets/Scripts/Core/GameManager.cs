using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CardGame.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CardGame.Core
{
    /// <summary>Local two-player duel coordinator. Scanning selects; BattleResolver alone computes damage.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [Header("References")]
        [SerializeField] private WebCamArucoManager arucoManager;
        [Header("Deck ownership — exactly five unique IDs each")]
        [SerializeField] private int[] player1Deck = { 0, 2, 4, 7, 8 };
        [SerializeField] private int[] player2Deck = { 1, 3, 5, 6, 9 };
        [Header("Scanning")]
        [SerializeField, Min(0.5f)] private float spawnDebounce = 0.5f;
        [Tooltip("Fresh, marker-free frames required before the next player / round.")]
        [SerializeField, Min(0.3f)] private float despawnGrace = 1;
        [SerializeField, Range(0, 1)] private float zoneSplitY = 0.5f;
        [SerializeField] private bool enforcePlayerZones = true;
        [Header("Battle presentation (rules do not depend on this timer)")]
        [SerializeField, Min(0.1f)] private float exchangeDisplaySeconds = 0.65f;
        [Header("Debug — optional, disabled by default")]
        [SerializeField] private bool enableKeyboardSimulation;

        // Kept for existing integrations. HP now belongs to selected cards, never to players.
        public event Action<CardData, int> OnCardPlayed;
        public event Action<CardData, int> OnCardRemoved;
        public event Action<int, int> OnHealthChanged;
        public event Action<int> OnTurnSwapped;
        public event Action<int> OnGameOver; // -1 means drawn match
        public event Action OnDuelChanged;
        public event Action<BattleResult> OnRoundResolved;

        public DuelState State { get; private set; }
        public int CurrentTurnPlayer { get; private set; }
        public bool IsGameActive => State != DuelState.GameOver;
        public bool AwaitingCardRemoval { get; private set; }
        public bool CardsRevealed { get; private set; }
        public MatchScore Score { get; private set; } = new MatchScore();
        public int CurrentRound => Math.Min(MatchScore.MaxRounds, Score.CompletedRounds + (State == DuelState.RoundResult || State == DuelState.GameOver ? 0 : 1));
        public BattleResult LastBattle { get; private set; }
        public string StatusMessage { get; private set; }
        public string ConfigurationError { get; private set; }
        public bool CanChooseTactic => (State == DuelState.WaitingForPlayer1Tactic || State == DuelState.WaitingForPlayer2Tactic)
            && !_tactics[CurrentTurnPlayer].HasValue;
        public bool CanContinueRound => State == DuelState.RoundResult && !Score.IsOver;
        public int ExchangeNumber { get; private set; }
        public int ExchangeCount => LastBattle?.Steps.Count ?? 0;

        private readonly HashSet<int> _usedThisMatch = new HashSet<int>();
        private readonly CardData[] _selected = new CardData[2];
        private readonly Tactic?[] _tactics = new Tactic?[2];
        private readonly int[] _cardHP = new int[2];
        private int _candidateID = -1, _candidateZone = -1, _visibleID = -1;
        private float _firstSeen, _lastSeen, _clearStart = -1, _lastClearFrame = -1;
        private bool _freshEvidence;
        private int _generation;
        private Coroutine _battleRoutine;
        private WebCamArucoManager _boundCamera;
        private float EvidenceTimeout => arucoManager != null ? arucoManager.VisibilityTimeout : 0.25f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitGame();
        }
        private void OnEnable() { BindCamera(); }
        private void Start() { BindCamera(); }
        private void BindCamera()
        {
            if (Instance != this) return;
            if (arucoManager == null) arucoManager = FindAnyObjectByType<WebCamArucoManager>();
            if (_boundCamera == arucoManager) return;
            UnbindCamera();
            _boundCamera = arucoManager;
            if (_boundCamera == null) return;
            _boundCamera.OnMarkerDetected += HandleMarkerDetected;
            _boundCamera.OnMarkerLost += HandleMarkerLost;
            _boundCamera.OnScanFrameCompleted += HandleScanFrameCompleted;
        }
        private void UnbindCamera()
        {
            if (_boundCamera == null) return;
            _boundCamera.OnMarkerDetected -= HandleMarkerDetected;
            _boundCamera.OnMarkerLost -= HandleMarkerLost;
            _boundCamera.OnScanFrameCompleted -= HandleScanFrameCompleted;
            _boundCamera = null;
        }
        private void OnDisable()
        {
            UnbindCamera();
            ResetCandidate();
            _clearStart = _lastClearFrame = -1;
        }
        private void OnDestroy()
        {
            UnbindCamera();
            if (Instance == this) Instance = null;
        }
        private void Update()
        {
            if (!IsGameActive) return;
            if (_boundCamera == null) BindCamera();
            if (enableKeyboardSimulation) UpdateKeyboardSimulation();
            ProcessTrackers();
        }

        private void InitGame()
        {
            _generation++;
            if (_battleRoutine != null) StopCoroutine(_battleRoutine);
            _battleRoutine = null;
            Score = new MatchScore();
            _usedThisMatch.Clear();
            ClearRound();
            ConfigurationError = ValidateDecks();
            CurrentTurnPlayer = 0;
            // A restart also requires newly analyzed empty frames; a card held over restart cannot play.
            AwaitingCardRemoval = true;
            _clearStart = _lastClearFrame = -1;
            _visibleID = -1;
            State = string.IsNullOrEmpty(ConfigurationError) ? DuelState.WaitingForPlayer1Card : DuelState.GameOver;
            StatusMessage = string.IsNullOrEmpty(ConfigurationError) ? "Yeni maç: önce kamera alanını boşaltın." : ConfigurationError;
            OnHealthChanged?.Invoke(0, 0);
            OnTurnSwapped?.Invoke(0);
            OnDuelChanged?.Invoke();
        }
        private string ValidateDecks()
        {
            if (player1Deck == null || player2Deck == null || player1Deck.Length != 5 || player2Deck.Length != 5)
                return "Her oyuncunun destesinde tam beş kart olmalı.";
            var all = new HashSet<int>();
            foreach (int[] deck in new[] { player1Deck, player2Deck })
                foreach (int id in deck)
                    if (id < 0 || id > 9 || !all.Add(id)) return "Desteler 0–9 aralığında, birbirinden farklı 10 ID içermeli.";
            return "";
        }
        private void ClearRound()
        {
            Array.Clear(_selected, 0, 2);
            Array.Clear(_tactics, 0, 2);
            Array.Clear(_cardHP, 0, 2);
            LastBattle = null;
            CardsRevealed = false;
            ExchangeNumber = 0;
            ResetCandidate();
        }
        public void RestartGame() { InitGame(); }
        public bool HasUsedCard(int id) => _usedThisMatch.Contains(id);
        public bool OwnsCard(int player, int id) => player >= 0 && player < 2 && Array.IndexOf(player == 0 ? player1Deck : player2Deck, id) >= 0;
        public int GetPlayerHP(int player) => player >= 0 && player < 2 ? _cardHP[player] : 0;
        public CardData GetVisibleCard(int player)
        {
            if (player < 0 || player > 1) return null;
            return CardsRevealed || player == CurrentTurnPlayer ? _selected[player] : null;
        }
        public Tactic? GetRevealedTactic(int player) => CardsRevealed && player >= 0 && player < 2 ? _tactics[player] : null;
        public bool IsTacticLocked(int player) => player >= 0 && player < 2 && _tactics[player].HasValue;
        public bool SelectTactic(Tactic tactic)
        {
            if (!CanChooseTactic || !BattleResolver.IsValid(tactic)) return false;
            _tactics[CurrentTurnPlayer] = tactic;
            ResetCandidate();
            if (CurrentTurnPlayer == 0)
            {
                CurrentTurnPlayer = 1;
                State = DuelState.WaitingForPlayer2Card;
                StatusMessage = AwaitingCardRemoval ? "Oyuncu 1 kartını kameradan kaldırın." : "Oyuncu 2: kartınızı taratın.";
                OnTurnSwapped?.Invoke(1);
            }
            else
            {
                StatusMessage = "Seçimler kilitlendi. Oyuncu 2 kartını kameradan kaldırın.";
                if (!AwaitingCardRemoval) BeginBattle();
            }
            OnDuelChanged?.Invoke();
            return true;
        }
        public bool ContinueRound()
        {
            if (!CanContinueRound) return false;
            ClearRound();
            CurrentTurnPlayer = 0;
            State = DuelState.WaitingForPlayer1Card;
            AwaitingCardRemoval = true;
            _clearStart = _lastClearFrame = -1;
            StatusMessage = "Yeni raund: önce kamera alanını boşaltın.";
            OnTurnSwapped?.Invoke(0);
            OnDuelChanged?.Invoke();
            return true;
        }
        // Existing callers cannot bypass the state machine by swapping a turn.
        public void EndTurn() { ContinueRound(); }

        private bool IsScanningState => State == DuelState.WaitingForPlayer1Card || State == DuelState.WaitingForPlayer2Card;
        public string GetMarkerStatus(int id)
        {
            if (!IsGameActive) return string.IsNullOrEmpty(ConfigurationError) ? "Maç bitti" : ConfigurationError;
            if (AwaitingCardRemoval) return "Önce kartı kaldırın; yeni boş kareler bekleniyor";
            if (!IsScanningState) return StatusMessage;
            if (id < 0) return StatusMessage;
            if (HasUsedCard(id)) return "Bu kart maçta kullanıldı";
            if (!OwnsCard(CurrentTurnPlayer, id)) return "Kart diğer oyuncuya ait";
            return id == _candidateID ? $"Doğrulanıyor: {Mathf.Max(0, _lastSeen - _firstSeen):F2}/{Mathf.Max(0.5f, spawnDebounce):F2} sn" : StatusMessage;
        }
        private void ResetCandidate() { _candidateID = _candidateZone = -1; _freshEvidence = false; }
        private void HandleMarkerDetected(int id, Vector2 position)
        {
            _visibleID = id;
            _clearStart = _lastClearFrame = -1;
            if (!IsGameActive || AwaitingCardRemoval || !IsScanningState) { ResetCandidate(); return; }
            int zone = position.y >= zoneSplitY ? 1 : 0;
            if (id < 0 || id > 9 || HasUsedCard(id) || !OwnsCard(CurrentTurnPlayer, id) || (enforcePlayerZones && zone != CurrentTurnPlayer))
            {
                ResetCandidate();
                StatusMessage = HasUsedCard(id) ? "Bu kart zaten kullanıldı." : !OwnsCard(CurrentTurnPlayer, id) ? "Bu kart diğer oyuncuya ait." : "Kartı aktif oyuncunun bölgesinde gösterin.";
                return;
            }
            float now = Time.unscaledTime;
            if (_candidateID != id || _candidateZone != zone || now - _lastSeen > EvidenceTimeout)
            {
                _candidateID = id; _candidateZone = zone; _firstSeen = now;
            }
            _lastSeen = now;
            _freshEvidence = true;
        }
        private void HandleMarkerLost(int id)
        {
            if (_visibleID == id) _visibleID = -1;
            if (_candidateID == id) ResetCandidate();
            // A lost event may mean a stall or multiple markers. Only empty analyzed frames open the gate.
        }
        private void HandleScanFrameCompleted(int markerCount)
        {
            if (!IsGameActive) return;
            if (markerCount != 0) { _clearStart = _lastClearFrame = -1; return; }
            _visibleID = -1;
            ResetCandidate();
            if (!AwaitingCardRemoval) return;
            float now = Time.unscaledTime;
            if (_clearStart < 0 || now - _lastClearFrame > EvidenceTimeout) _clearStart = now;
            _lastClearFrame = now;
            if (now - _clearStart < Mathf.Max(0.3f, despawnGrace)) return;
            AwaitingCardRemoval = false;
            _clearStart = _lastClearFrame = -1;
            // Only report removal for the last selected card, with its actual owner.
            int owner = _selected[1] != null ? 1 : _selected[0] != null ? 0 : -1;
            if (owner >= 0) OnCardRemoved?.Invoke(_selected[owner], owner);
            StatusMessage = CanChooseTactic ? "Taktiğinizi seçin." : $"Oyuncu {CurrentTurnPlayer + 1}: kartınızı taratın.";
            if (State == DuelState.WaitingForPlayer2Tactic && _tactics[1].HasValue) BeginBattle();
            OnDuelChanged?.Invoke();
        }
        private void ProcessTrackers()
        {
            if (!IsGameActive || !IsScanningState || AwaitingCardRemoval) return;
            bool fresh = _freshEvidence;
            _freshEvidence = false;
            if (_candidateID < 0) return;
            if (Time.unscaledTime - _lastSeen > EvidenceTimeout) { ResetCandidate(); return; }
            if (!fresh || _lastSeen - _firstSeen < Mathf.Max(0.5f, spawnDebounce)) return;
            CardData card = CardDatabaseManager.Instance?.GetCardByID(_candidateID);
            if (card == null) { StatusMessage = "CardData bulunamadı."; ResetCandidate(); return; }
            _selected[CurrentTurnPlayer] = card;
            _cardHP[CurrentTurnPlayer] = card.health;
            _usedThisMatch.Add(card.cardID);
            State = CurrentTurnPlayer == 0 ? DuelState.WaitingForPlayer1Tactic : DuelState.WaitingForPlayer2Tactic;
            AwaitingCardRemoval = true;
            _clearStart = _lastClearFrame = -1;
            ResetCandidate();
            StatusMessage = "Kart seçildi. Gizli taktiğinizi seçin, ardından kartı kaldırın.";
            OnCardPlayed?.Invoke(card, CurrentTurnPlayer);
            OnDuelChanged?.Invoke();
        }
        private void BeginBattle()
        {
            if (State != DuelState.WaitingForPlayer2Tactic || !_tactics[0].HasValue || !_tactics[1].HasValue || AwaitingCardRemoval) return;
            CardsRevealed = true;
            State = DuelState.ResolvingBattle;
            LastBattle = BattleResolver.Resolve(_selected[0].health, _selected[0].attackPower, _tactics[0].Value,
                _selected[1].health, _selected[1].attackPower, _tactics[1].Value);
            StatusMessage = "Kartlar ve taktikler açıldı. Düello başladı.";
            OnHealthChanged?.Invoke(_cardHP[0], _cardHP[1]);
            OnDuelChanged?.Invoke();
            if (Application.isPlaying) _battleRoutine = StartCoroutine(PresentBattle(_generation));
        }
        private IEnumerator PresentBattle(int generation)
        {
            while (State == DuelState.ResolvingBattle && generation == _generation)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, exchangeDisplaySeconds));
                if (generation != _generation) yield break;
                AdvanceBattle();
            }
            _battleRoutine = null;
        }
        private void AdvanceBattle()
        {
            if (State != DuelState.ResolvingBattle || LastBattle == null) return;
            BattleStep step = LastBattle.Steps[ExchangeNumber++];
            _cardHP[0] = step.Player1HP; _cardHP[1] = step.Player2HP;
            OnHealthChanged?.Invoke(_cardHP[0], _cardHP[1]);
            if (ExchangeNumber == LastBattle.Steps.Count)
            {
                Score.RecordRound(LastBattle.Winner);
                State = Score.IsOver ? DuelState.GameOver : DuelState.RoundResult;
                StatusMessage = LastBattle.SafetyLimitReached ? "Güvenlik sınırı: raund berabere." :
                    LastBattle.Winner < 0 ? "Raund berabere." : $"Oyuncu {LastBattle.Winner + 1} raundu kazandı.";
                OnRoundResolved?.Invoke(LastBattle);
                if (Score.IsOver) OnGameOver?.Invoke(Score.Winner);
            }
            OnDuelChanged?.Invoke();
        }
        private void UpdateKeyboardSimulation()
        {
            bool one = false, two = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            one = keyboard.digit1Key.isPressed; two = keyboard.digit2Key.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            one = Input.GetKey(KeyCode.Alpha1); two = Input.GetKey(KeyCode.Alpha2);
#endif
            if (one && two) { ResetCandidate(); HandleScanFrameCompleted(2); return; }
            if (one) HandleMarkerDetected(player1Deck[0], new Vector2(0.5f, 0.2f));
            else if (two) HandleMarkerDetected(player2Deck[0], new Vector2(0.5f, 0.8f));
            else { if (_visibleID >= 0) HandleMarkerLost(_visibleID); HandleScanFrameCompleted(0); }
        }
    }
}
