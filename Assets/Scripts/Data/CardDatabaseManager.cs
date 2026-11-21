using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Data
{
    /// <summary>
    /// Loads every CardData asset from Resources and provides O(1) lookup by ArUco marker ID.
    /// Falls back to parsing Resources/Data/Cards.csv when no ScriptableObject assets exist,
    /// creating runtime-only CardData instances so the game can still function.
    /// </summary>
    public class CardDatabaseManager : MonoBehaviour
    {
        public static CardDatabaseManager Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool logLoadedCards = true;

        // ID → CardData lookup
        private readonly Dictionary<int, CardData> _cardLookup = new Dictionary<int, CardData>();

        /// <summary>Number of cards loaded into the database.</summary>
        public int CardCount => _cardLookup.Count;

        // ─────────────────────── Unity Lifecycle ───────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCards();
        }

        // ─────────────────────── Public API ────────────────────────────

        /// <summary>
        /// Returns the CardData for the given ArUco marker ID, or null if not found.
        /// </summary>
        public CardData GetCardByID(int markerID)
        {
            _cardLookup.TryGetValue(markerID, out CardData card);
            return card;
        }

        /// <summary>Returns all loaded CardData instances.</summary>
        public IEnumerable<CardData> GetAllCards()
        {
            return _cardLookup.Values;
        }

        // ─────────────────────── Loading ───────────────────────────────

        private void LoadCards()
        {
            // 1. Try ScriptableObject assets first (folder: Resources/Cards/)
            CardData[] assets = Resources.LoadAll<CardData>("Cards");
            if (assets != null && assets.Length > 0)
            {
                foreach (CardData card in assets)
                {
                    RegisterCard(card);
                }

                if (logLoadedCards)
                    Debug.Log($"[CardDB] Loaded {_cardLookup.Count} card(s) from ScriptableObject assets.");

                return;
            }

            // 2. Fallback – parse CSV
            LoadFromCSV();
        }

        private void LoadFromCSV()
        {
            TextAsset csvAsset = Resources.Load<TextAsset>("Data/Cards");
            if (csvAsset == null)
            {
                Debug.LogError("[CardDB] No card assets found and Resources/Data/Cards.csv is missing!");
                return;
            }

            string[] lines = csvAsset.text.Split('\n');
            bool headerSkipped = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Skip header row
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue;
                }

                string[] cols = line.Split(',');
                if (cols.Length < 5)
                {
                    Debug.LogWarning($"[CardDB] Skipping malformed CSV line: {line}");
                    continue;
                }

                if (!int.TryParse(cols[0].Trim(), out int id) || id < 0 || id > 49 ||
                    !int.TryParse(cols[2].Trim(), out int mana) || mana < 0 ||
                    !int.TryParse(cols[3].Trim(), out int attack) || attack < 0 ||
                    !int.TryParse(cols[4].Trim(), out int hp) || hp < 1)
                {
                    Debug.LogWarning($"[CardDB] Skipping invalid card row: {line}");
                    continue;
                }
                CardData card = ScriptableObject.CreateInstance<CardData>();
                card.cardID      = id;
                card.cardName    = cols[1].Trim();
                card.manaCost    = mana;
                card.attackPower = attack;
                card.health      = hp;
                // cardPrefab and cardIcon left null for CSV-only mode
                card.name = $"Card_{card.cardID}_{card.cardName}";

                RegisterCard(card);
            }

            if (logLoadedCards)
                Debug.Log($"[CardDB] Loaded {_cardLookup.Count} card(s) from CSV fallback.");
        }

        private void RegisterCard(CardData card)
        {
            if (card == null || card.cardID < 0 || card.cardID > 49 || card.attackPower < 0 || card.health < 1)
            {
                Debug.LogWarning("[CardDB] Invalid CardData asset; expected ID 0-49 and valid stats.");
                return;
            }
            if (_cardLookup.ContainsKey(card.cardID))
            {
                Debug.LogWarning($"[CardDB] Duplicate card ID {card.cardID} — keeping first instance.");
                return;
            }

            _cardLookup[card.cardID] = card;

            if (logLoadedCards)
                Debug.Log($"[CardDB] Registered {card}");
        }
    }
}
