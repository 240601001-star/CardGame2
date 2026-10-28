using System;
using System.Collections.Generic;

namespace CardGame.Core
{
    public enum Tactic { Attack, Defense, Focus }
    public enum DuelState
    {
        WaitingForPlayer1Card, WaitingForPlayer1Tactic,
        WaitingForPlayer2Card, WaitingForPlayer2Tactic,
        ResolvingBattle, RoundResult, GameOver
    }

    public readonly struct BattleStep
    {
        public readonly int Player1HP, Player2HP;
        public BattleStep(int hp1, int hp2) { Player1HP = hp1; Player2HP = hp2; }
    }

    public sealed class BattleResult
    {
        public int Winner { get; } // -1 = draw, 0 = P1, 1 = P2
        public int Player1Damage { get; }
        public int Player2Damage { get; }
        public bool SafetyLimitReached { get; }
        public IReadOnlyList<BattleStep> Steps { get; }
        internal BattleResult(int winner, int damage1, int damage2, bool limited, List<BattleStep> steps)
        {
            Winner = winner; Player1Damage = damage1; Player2Damage = damage2;
            SafetyLimitReached = limited; Steps = steps.AsReadOnly();
        }
    }

    /// <summary>Deterministic battle rules; no Unity objects, timers, UI or animations.</summary>
    public static class BattleResolver
    {
        public const int MaxExchanges = 1024;
        public static bool IsValid(Tactic tactic) => tactic >= Tactic.Attack && tactic <= Tactic.Focus;
        public static bool Beats(Tactic own, Tactic other)
        {
            if (!IsValid(own) || !IsValid(other)) throw new ArgumentOutOfRangeException(nameof(own));
            return (own == Tactic.Attack && other == Tactic.Focus) ||
                   (own == Tactic.Focus && other == Tactic.Defense) ||
                   (own == Tactic.Defense && other == Tactic.Attack);
        }
        public static int Damage(int attack, Tactic own, Tactic other)
        {
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            long damage = Beats(own, other) ? (long)attack * 120 / 100 : attack;
            return (int)Math.Min(int.MaxValue, Math.Max(1, damage));
        }
        public static BattleResult Resolve(int hp1, int atk1, Tactic tactic1, int hp2, int atk2, Tactic tactic2)
        {
            if (hp1 < 1 || hp2 < 1) throw new ArgumentOutOfRangeException(nameof(hp1));
            int damage1 = Damage(atk1, tactic1, tactic2), damage2 = Damage(atk2, tactic2, tactic1);
            var steps = new List<BattleStep>();
            for (int exchange = 0; exchange < MaxExchanges; exchange++)
            {
                // Both attacks use the state at the START of the exchange, even if one is lethal.
                hp1 = Math.Max(0, hp1 - damage2);
                hp2 = Math.Max(0, hp2 - damage1);
                steps.Add(new BattleStep(hp1, hp2));
                if (hp1 == 0 || hp2 == 0)
                    return new BattleResult(hp1 == 0 && hp2 == 0 ? -1 : hp2 == 0 ? 0 : 1,
                        damage1, damage2, false, steps);
            }
            // Abnormal data cannot hang the game; a safety-limited battle awards no point.
            return new BattleResult(-1, damage1, damage2, true, steps);
        }
    }

    /// <summary>Five rounds, first to three wins; draws consume a round without awarding a point.</summary>
    public sealed class MatchScore
    {
        public const int MaxRounds = 5;
        public const int WinsRequired = 3;
        public int Player1 { get; private set; }
        public int Player2 { get; private set; }
        public int CompletedRounds { get; private set; }
        public bool IsOver => Player1 >= WinsRequired || Player2 >= WinsRequired || CompletedRounds >= MaxRounds;
        public int Winner => !IsOver ? -2 : Player1 == Player2 ? -1 : Player1 > Player2 ? 0 : 1;
        public void RecordRound(int winner)
        {
            if (winner < -1 || winner > 1) throw new ArgumentOutOfRangeException(nameof(winner));
            if (IsOver) throw new InvalidOperationException("Match has already ended.");
            CompletedRounds++;
            if (winner == 0) Player1++;
            if (winner == 1) Player2++;
        }
    }
}
