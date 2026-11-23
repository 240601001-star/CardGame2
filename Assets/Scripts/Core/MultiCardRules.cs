using System;
using CardGame.Data;

namespace CardGame.Core
{
    /// <summary>Mode-specific costs and rules; printed CardData HP/ATK stay authoritative.</summary>
    public static class MultiCardRules
    {
        public const int HeroHealth=600;
        public const int StartingMana=4;
        public const int SecondStartingMana=5;
        public const int MaximumMana=10;
        public const int FirstOpeningCards=4;
        public const int SecondOpeningCards=4;
        public const int FatigueStep=25;
        public const int ManaPowerOffset=70;
        public const int ManaPowerPerCost=45;

        public static int ManaCost(CardData card)
        {
            if(card.manaCost>0)return Math.Min(MaximumMana,card.manaCost);
            int power=card.health+2*card.attackPower-ManaPowerOffset;
            return Math.Max(1,Math.Min(MaximumMana,(power+ManaPowerPerCost-1)/ManaPowerPerCost));
        }
    }
}
