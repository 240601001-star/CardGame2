using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Data;

namespace CardGame.Core
{
    /// <summary>Independent local prototype; never edits CardData or the ArUco duel.</summary>
    public sealed class MultiCardMatch
    {
        public const int BoardLimit = 7;
        public sealed class Unit
        {
            public readonly CardData Card;
            public readonly int Attack;
            public int Health { get; internal set; }
            public bool Ready { get; internal set; }
            internal Unit(CardData card) { Card=card; Attack=AttackOf(card); Health=HealthOf(card); }
        }
        public sealed class Player
        {
            public int Health { get; internal set; } = MultiCardRules.HeroHealth;
            public int Mana { get; internal set; }
            public int MaxMana { get; internal set; } = MultiCardRules.StartingMana-1;
            public int Fatigue { get; internal set; }
            public CardHand Hand { get; } = new CardHand();
            internal readonly Queue<CardData> Deck = new Queue<CardData>();
            internal readonly List<Unit> Units = new List<Unit>();
            internal readonly Dictionary<int,CardData> RemainingPhysical = new Dictionary<int,CardData>();
            internal readonly List<CardData> Discarded = new List<CardData>();
            internal bool Physical;
            public IReadOnlyList<CardData> Discard => Discarded.AsReadOnly();
            public int DeckCount => Physical?RemainingPhysical.Count:Deck.Count;
            public IReadOnlyList<Unit> Board => Units.AsReadOnly();
        }
        private readonly Player[] players = { new Player(), new Player() };
        private bool extraDrawUsed;
        public bool PhysicalMode { get; private set; }
        public bool OpeningHands { get; private set; }
        public int PendingDrawCount { get; private set; }
        public CardData LastPhysicalDraw { get; private set; }
        public bool LastDrawBurned { get; private set; }
        public bool CanAct => !IsOver&&!OpeningHands&&PendingDrawCount==0;
        public int CurrentPlayer { get; private set; }
        public int Turn { get; private set; }
        public bool IsOver { get; private set; }
        public int Winner { get; private set; } = -1;
        public string Status { get; private set; }
        public Player GetPlayer(int index) => players[index];
        public static int AttackOf(CardData card) => card.attackPower;
        public static int HealthOf(CardData card) => card.health;
        public static int CostOf(CardData card) => MultiCardRules.ManaCost(card);

        public MultiCardMatch(IEnumerable<CardData> cards,int seed)
        {
            var pool=cards.Where(c=>c!=null).OrderBy(c=>c.cardID).ToList();
            if(pool.Count<2)throw new ArgumentException("En az iki kart gerekli.");
            var random=new Random(seed);
            for(int i=pool.Count-1;i>0;i--){int j=random.Next(i+1);var temp=pool[i];pool[i]=pool[j];pool[j]=temp;}
            for(int p=0;p<2;p++)
            {
                players[p].MaxMana=(p==0?MultiCardRules.StartingMana:MultiCardRules.SecondStartingMana)-1;
                for(int i=0;i<20;i++)players[p].Deck.Enqueue(pool[(i*2+p)%pool.Count]);
                for(int i=0;i<(p==0?MultiCardRules.FirstOpeningCards-1:MultiCardRules.SecondOpeningCards);i++)Draw(p);
            }
            BeginTurn();
        }
        private MultiCardMatch(){ }
        public static MultiCardMatch CreatePhysical(IEnumerable<CardData> first,IEnumerable<CardData> second)
        {
            var match=new MultiCardMatch();var used=new HashSet<int>();var decks=new[]{first,second};
            for(int p=0;p<2;p++)
            {
                var cards=decks[p].ToList();
                if(cards.Count!=PhysicalDeckRegistry.DeckSize)throw new ArgumentException("Her deste 20 kart içermeli.");
                match.players[p].Physical=true;match.players[p].MaxMana=(p==0?MultiCardRules.StartingMana:MultiCardRules.SecondStartingMana)-1;
                foreach(var card in cards)
                {
                    if(card==null||card.cardID<0||card.cardID>49||!used.Add(card.cardID))throw new ArgumentException("Fiziksel kart ID'leri iki destede de tekil olmalı.");
                    match.players[p].RemainingPhysical.Add(card.cardID,card);
                }
            }
            // Opening draws are physical. Player 2 gets a normal turn draw after the shared opening.
            match.PhysicalMode=true;match.OpeningHands=true;match.CurrentPlayer=0;match.PendingDrawCount=MultiCardRules.FirstOpeningCards;
            match.Status="Oyuncu 1: fiziksel destenden "+MultiCardRules.FirstOpeningCards+" kart çek; tek tek okut.";return match;
        }
        public bool AcceptPhysicalDraw(int id)
        {
            if(!PhysicalMode||IsOver||PendingDrawCount<=0)return Reject("Şu anda kart çekme adımı yok.");
            var player=players[CurrentPlayer];
            if(!player.RemainingPhysical.TryGetValue(id,out var card))return Reject("Bu kart çekilebilir destende değil; başka oyuncuya ait veya zaten çekilmiş.");
            player.RemainingPhysical.Remove(id);PendingDrawCount--;LastPhysicalDraw=card;
            LastDrawBurned=!player.Hand.TryAdd(card);
            if(LastDrawBurned)player.Discarded.Add(card);
            Status=LastDrawBurned?card.cardName+": el dolu, kartı kullanılmış yığınına ayır.":card.cardName+" eline eklendi. Kartı kameradan kaldır.";
            if(OpeningHands&&PendingDrawCount==0)
            {
                if(CurrentPlayer==0){CurrentPlayer=1;PendingDrawCount=MultiCardRules.SecondOpeningCards;Status="Oyuncu 2: fiziksel destenden "+MultiCardRules.SecondOpeningCards+" kart çek; tek tek okut.";}
                else{OpeningHands=false;CurrentPlayer=0;BeginTurn(false);}
            }
            return true;
        }
        public bool CanPlay(int handIndex)
        {
            var p=players[CurrentPlayer];
            return CanAct&&handIndex>=0&&handIndex<p.Hand.Count&&p.Units.Count<BoardLimit&&CostOf(p.Hand[handIndex])<=p.Mana;
        }
        public bool Play(int handIndex)
        {
            if(!CanPlay(handIndex))return Reject("Yeterli mana veya boş masa yeri yok.");
            var p=players[CurrentPlayer];p.Hand.TryTake(handIndex,out var card);
            p.Mana-=CostOf(card);p.Units.Add(new Unit(card));Status=card.cardName+" masaya geldi. Sonraki tur saldırabilir.";return true;
        }
        public bool ExtraDraw()
        {
            if(!CanAct||extraDrawUsed||players[CurrentPlayer].Mana<1)return Reject("Ek çekim turda bir kez, 1 mana karşılığında kullanılabilir.");
            players[CurrentPlayer].Mana--;extraDrawUsed=true;Draw(CurrentPlayer);return true;
        }
        public bool CanExtraDraw => CanAct&&!extraDrawUsed&&players[CurrentPlayer].Mana>0;
        public bool CanAttackHero(int attackerIndex)
        {
            var own=players[CurrentPlayer];
            return CanAct&&attackerIndex>=0&&attackerIndex<own.Units.Count&&own.Units[attackerIndex].Ready;
        }
        public bool Attack(int attackerIndex,int targetIndex)
        {
            var own=players[CurrentPlayer];var enemy=players[1-CurrentPlayer];
            if(!CanAct||attackerIndex<0||attackerIndex>=own.Units.Count||!own.Units[attackerIndex].Ready||targetIndex< -1||targetIndex>=enemy.Units.Count)
                return Reject("Hazır bir kart ve geçerli bir hedef seç.");
            var attacker=own.Units[attackerIndex];attacker.Ready=false;
            if(targetIndex==-1){enemy.Health=Math.Max(0,enemy.Health-attacker.Attack);Status="Rakip kahramana "+attacker.Attack+" hasar.";}
            else
            {
                var target=enemy.Units[targetIndex];target.Health-=attacker.Attack;attacker.Health-=target.Attack;
                if(target.Health<=0){enemy.Units.Remove(target);enemy.Discarded.Add(target.Card);}
                if(attacker.Health<=0){own.Units.Remove(attacker);own.Discarded.Add(attacker.Card);}
                Status="Kartlar aynı anda hasar verdi.";
            }
            CheckWinner();return true;
        }
        public bool EndTurn()
        {
            if(IsOver)return false;
            if(!CanAct)return Reject("Önce bekleyen fiziksel kartları okut.");
            CurrentPlayer=1-CurrentPlayer;BeginTurn();return true;
        }
        private void BeginTurn(bool drawCard=true)
        {
            Turn++;var p=players[CurrentPlayer];p.MaxMana=Math.Min(MultiCardRules.MaximumMana,p.MaxMana+1);p.Mana=p.MaxMana;
            foreach(var unit in p.Units)unit.Ready=true;
            extraDrawUsed=false;Status="Oyuncu "+(CurrentPlayer+1)+": kart oyna veya saldır.";if(drawCard)Draw(CurrentPlayer);
        }
        private void Draw(int player)
        {
            var p=players[player];
            if(p.DeckCount==0){p.Fatigue++;int damage=p.Fatigue*MultiCardRules.FatigueStep;p.Health=Math.Max(0,p.Health-damage);Status="Deste boş: "+damage+" yorgunluk hasarı.";CheckWinner();return;}
            if(PhysicalMode){PendingDrawCount=1;LastPhysicalDraw=null;LastDrawBurned=false;Status="Destenden 1 fiziksel kart çek ve okut.";return;}
            if(!p.Hand.TryAdd(p.Deck.Dequeue()))Status="El dolu: çekilen kart yakıldı.";
        }
        private void CheckWinner()
        {
            if(players[0].Health>0&&players[1].Health>0)return;
            IsOver=true;Winner=players[0].Health==0&&players[1].Health==0?-1:players[0].Health>0?0:1;
            Status=Winner<0?"Berabere!":"Oyuncu "+(Winner+1)+" kazandı!";
        }
        private bool Reject(string message){Status=message;return false;}
    }
}
