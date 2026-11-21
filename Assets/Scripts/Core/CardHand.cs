using System.Collections.Generic;
using CardGame.Data;

namespace CardGame.Core
{
    /// <summary>Hand storage for the multi-card mode. Duplicate card types are allowed.</summary>
    public sealed class CardHand
    {
        public const int MaxCards = 10;
        private readonly List<CardData> cards = new List<CardData>();
        public int Count => cards.Count;
        public CardData this[int index] => cards[index];

        public bool TryAdd(CardData card)
        {
            if (card == null || cards.Count >= MaxCards) return false;
            cards.Add(card);
            return true;
        }

        public bool TryTake(int index, out CardData card)
        {
            card = null;
            if (index < 0 || index >= cards.Count) return false;
            card = cards[index];
            cards.RemoveAt(index);
            return true;
        }
    }
}
