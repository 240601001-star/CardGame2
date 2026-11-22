using System.Collections.Generic;
using System.Linq;
using CardGame.Data;

namespace CardGame.Core
{
    /// <summary>Registers the available physical IDs, not their shuffled order.</summary>
    public sealed class PhysicalDeckRegistry
    {
        public const int DeckSize=20;
        private readonly Dictionary<int,CardData> catalog;
        private readonly List<CardData>[] decks={new List<CardData>(),new List<CardData>()};
        public int CurrentPlayer { get; private set; }
        public bool IsComplete { get; private set; }
        public string Status { get; private set; }="Oyuncu 1: destendeki kartları tek tek okut.";
        public int Count => decks[CurrentPlayer].Count;
        public bool CanConfirm => !IsComplete&&Count==DeckSize;
        public IReadOnlyList<CardData> GetDeck(int player)=>decks[player].AsReadOnly();
        public PhysicalDeckRegistry(IEnumerable<CardData> cards)
        {catalog=cards.Where(c=>c!=null&&c.cardID>=0&&c.cardID<50).ToDictionary(c=>c.cardID);}
        public bool Register(int id)
        {
            if(IsComplete)return Reject("Desteler zaten kaydedildi.");
            if(!catalog.TryGetValue(id,out var card))return Reject("Kart 0–49 koleksiyonunda bulunamadı.");
            if(decks[CurrentPlayer].Any(c=>c.cardID==id))return Reject("Bu kart zaten destende kayıtlı.");
            if(decks[1-CurrentPlayer].Any(c=>c.cardID==id))return Reject("Bu kart diğer oyuncunun destesinde kayıtlı.");
            if(Count>=DeckSize)return Reject("20 kart tamamlandı. Desteyi kaydet düğmesine bas.");
            decks[CurrentPlayer].Add(card);Status=card.cardName+" kaydedildi. Kartı kaldır, sıradakini okut.";return true;
        }
        public bool UndoLast()
        {
            if(IsComplete||Count==0)return false;
            decks[CurrentPlayer].RemoveAt(Count-1);Status="Son kayıt kaldırıldı. Doğru kartı okut.";return true;
        }
        public bool Confirm()
        {
            if(!CanConfirm)return Reject("Her oyuncu 20 farklı kart kaydetmeli.");
            if(CurrentPlayer==0){CurrentPlayer=1;Status="Oyuncu 2: kendi 20 kartını tek tek okut.";}
            else{IsComplete=true;Status="Desteleri fiziksel olarak karıştırın.";}
            return true;
        }
        private bool Reject(string value){Status=value;return false;}
    }
}
