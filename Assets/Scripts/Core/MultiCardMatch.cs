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
            public int Health { get; internal set; } = 30;
            public int Mana { get; internal set; }
            public int MaxMana { get; internal set; } = 2;
            public int Fatigue { get; internal set; }
            public CardHand Hand { get; } = new CardHand();
            internal readonly Queue<CardData> Deck = new Queue<CardData>();
            internal readonly List<Unit> Units = new List<Unit>();
            public int DeckCount => Deck.Count;
            public IReadOnlyList<Unit> Board => Units.AsReadOnly();
        }
        private readonly Player[] players = { new Player(), new Player() };
        private bool extraDrawUsed;
        public int CurrentPlayer { get; private set; }
        public int Turn { get; private set; }
        public bool IsOver { get; private set; }
        public int Winner { get; private set; } = -1;
        public string Status { get; private set; }
        public Player GetPlayer(int index) => players[index];
        public static int AttackOf(CardData card) => Math.Max(1,(card.attackPower+19)/20);
        public static int HealthOf(CardData card) => Math.Max(1,(card.health+29)/30);
        public static int CostOf(CardData card) => card.manaCost>0 ? Math.Min(10,card.manaCost) : Math.Min(10,(AttackOf(card)+HealthOf(card)+1)/3);

        public MultiCardMatch(IEnumerable<CardData> cards,int seed)
        {
            var pool=cards.Where(c=>c!=null).OrderBy(c=>c.cardID).ToList();
            if(pool.Count<2)throw new ArgumentException("En az iki kart gerekli.");
            var random=new Random(seed);
            for(int i=pool.Count-1;i>0;i--){int j=random.Next(i+1);var temp=pool[i];pool[i]=pool[j];pool[j]=temp;}
            for(int p=0;p<2;p++)
            {
                for(int i=0;i<20;i++)players[p].Deck.Enqueue(pool[(i*2+p)%pool.Count]);
                for(int i=0;i<3;i++)Draw(p);
            }
            BeginTurn();
        }
        public bool CanPlay(int handIndex)
        {
            var p=players[CurrentPlayer];
            return !IsOver&&handIndex>=0&&handIndex<p.Hand.Count&&p.Units.Count<BoardLimit&&CostOf(p.Hand[handIndex])<=p.Mana;
        }
        public bool Play(int handIndex)
        {
            if(!CanPlay(handIndex))return Reject("Yeterli mana veya boş masa yeri yok.");
            var p=players[CurrentPlayer];p.Hand.TryTake(handIndex,out var card);
            p.Mana-=CostOf(card);p.Units.Add(new Unit(card));Status=card.cardName+" masaya geldi. Sonraki tur saldırabilir.";return true;
        }
        public bool ExtraDraw()
        {
            if(IsOver||extraDrawUsed||players[CurrentPlayer].Mana<1)return Reject("Ek çekim turda bir kez, 1 mana karşılığında kullanılabilir.");
            players[CurrentPlayer].Mana--;extraDrawUsed=true;Draw(CurrentPlayer);return true;
        }
        public bool CanExtraDraw => !IsOver&&!extraDrawUsed&&players[CurrentPlayer].Mana>0;
        public bool Attack(int attackerIndex,int targetIndex)
        {
            var own=players[CurrentPlayer];var enemy=players[1-CurrentPlayer];
            if(IsOver||attackerIndex<0||attackerIndex>=own.Units.Count||!own.Units[attackerIndex].Ready||targetIndex< -1||targetIndex>=enemy.Units.Count)
                return Reject("Hazır bir kart ve geçerli bir hedef seç.");
            var attacker=own.Units[attackerIndex];attacker.Ready=false;
            if(targetIndex==-1){enemy.Health=Math.Max(0,enemy.Health-attacker.Attack);Status="Rakip kahramana "+attacker.Attack+" hasar.";}
            else
            {
                var target=enemy.Units[targetIndex];target.Health-=attacker.Attack;attacker.Health-=target.Attack;
                if(target.Health<=0)enemy.Units.Remove(target);
                if(attacker.Health<=0)own.Units.Remove(attacker);
                Status="Kartlar aynı anda hasar verdi.";
            }
            CheckWinner();return true;
        }
        public bool EndTurn()
        {
            if(IsOver)return false;
            CurrentPlayer=1-CurrentPlayer;BeginTurn();return true;
        }
        private void BeginTurn()
        {
            Turn++;var p=players[CurrentPlayer];p.MaxMana=Math.Min(10,p.MaxMana+1);p.Mana=p.MaxMana;
            foreach(var unit in p.Units)unit.Ready=true;
            extraDrawUsed=false;Status="Oyuncu "+(CurrentPlayer+1)+": kart oyna veya saldır.";Draw(CurrentPlayer);
        }
        private void Draw(int player)
        {
            var p=players[player];
            if(p.Deck.Count==0){p.Health=Math.Max(0,p.Health-++p.Fatigue);Status="Deste boş: "+p.Fatigue+" yorgunluk hasarı.";CheckWinner();return;}
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
