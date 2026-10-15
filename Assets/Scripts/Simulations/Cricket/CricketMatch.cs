namespace SimulationLobby.Simulations.Cricket
{
    public enum BallOutcome
    {
        Dot = 0,
        Single = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Six = 5,
        Wicket = 6
    }

    public static class BallOutcomes
    {
        public static int Runs(BallOutcome outcome)
        {
            switch (outcome)
            {
                case BallOutcome.Single: return 1;
                case BallOutcome.Two: return 2;
                case BallOutcome.Three: return 3;
                case BallOutcome.Four: return 4;
                case BallOutcome.Six: return 6;
                default: return 0;
            }
        }

        public static bool IsBoundary(BallOutcome outcome)
        {
            return outcome == BallOutcome.Four || outcome == BallOutcome.Six;
        }

        /// <summary>Maps a run value from a config band to an outcome. Only 0, 1, 2, 3, 4 and 6 exist in cricket.</summary>
        public static bool TryFromRuns(int runs, out BallOutcome outcome)
        {
            switch (runs)
            {
                case 0: outcome = BallOutcome.Dot; return true;
                case 1: outcome = BallOutcome.Single; return true;
                case 2: outcome = BallOutcome.Two; return true;
                case 3: outcome = BallOutcome.Three; return true;
                case 4: outcome = BallOutcome.Four; return true;
                case 6: outcome = BallOutcome.Six; return true;
                default: outcome = BallOutcome.Dot; return false;
            }
        }

        public static string Label(BallOutcome outcome)
        {
            switch (outcome)
            {
                case BallOutcome.Dot: return "DOT BALL";
                case BallOutcome.Single: return "SINGLE";
                case BallOutcome.Two: return "TWO RUNS";
                case BallOutcome.Three: return "THREE RUNS";
                case BallOutcome.Four: return "FOUR!";
                case BallOutcome.Six: return "SIX!";
                default: return "OUT!";
            }
        }
    }

    /// <summary>
    /// The scorebook for a two-innings limited-overs match: runs, wickets, balls, target, result.
    /// Pure C# with no engine types, so the rules can be checked without running a scene, and the
    /// physics board only ever feeds it one <see cref="BallOutcome"/> per delivery.
    /// </summary>
    /// <remarks>
    /// Side 0 bats first. The chase ends the moment side 1 passes the target, as in the real game.
    /// A tie is a tie (<see cref="Winner"/> == -1); it is not settled by a further over.
    /// </remarks>
    public sealed class CricketMatch
    {
        readonly int[] _runs = new int[2];
        readonly int[] _wickets = new int[2];
        readonly int[] _balls = new int[2];

        public CricketMatch(int ballsPerInnings, int wicketsPerInnings, int ballsPerOver = 6)
        {
            BallsPerInnings = ballsPerInnings < 1 ? 1 : ballsPerInnings;
            WicketsPerInnings = wicketsPerInnings < 1 ? 1 : wicketsPerInnings;
            BallsPerOver = ballsPerOver < 1 ? 6 : ballsPerOver;
            Winner = -2;
        }

        public int BallsPerInnings { get; }

        public int WicketsPerInnings { get; }

        public int BallsPerOver { get; }

        /// <summary>0 for the first innings, 1 for the chase.</summary>
        public int InningsIndex { get; private set; }

        public bool IsMatchOver { get; private set; }

        /// <summary>0 or 1 for the winning side, -1 for a tie, -2 while the match is still on.</summary>
        public int Winner { get; private set; }

        public bool IsChasing => InningsIndex == 1;

        public int Runs(int innings) => _runs[innings];

        public int Wickets(int innings) => _wickets[innings];

        public int Balls(int innings) => _balls[innings];

        public int CurrentRuns => _runs[InningsIndex];

        public int CurrentWickets => _wickets[InningsIndex];

        public int CurrentBalls => _balls[InningsIndex];

        public int BallsLeft => BallsPerInnings - _balls[InningsIndex];

        /// <summary>Runs side 1 needs to win. Meaningful once the first innings is over.</summary>
        public int Target => _runs[0] + 1;

        /// <summary>Runs still needed in the chase, never below zero. Zero in the first innings.</summary>
        public int RunsNeeded => IsChasing ? System.Math.Max(0, Target - _runs[1]) : 0;

        public string OversText(int balls) => $"{balls / BallsPerOver}.{balls % BallsPerOver}";

        public string CurrentOversText => OversText(CurrentBalls);

        /// <summary>
        /// Books one delivery. Returns true if it ended the innings (or the match). Calling this after
        /// the match is over does nothing and returns false.
        /// </summary>
        public bool Record(BallOutcome outcome)
        {
            if (IsMatchOver)
            {
                return false;
            }

            int side = InningsIndex;
            _balls[side]++;
            if (outcome == BallOutcome.Wicket)
            {
                _wickets[side]++;
            }
            else
            {
                _runs[side] += BallOutcomes.Runs(outcome);
            }

            if (side == 1 && _runs[1] > _runs[0])
            {
                IsMatchOver = true;
                Winner = 1;
                return true;
            }

            bool inningsOver = _balls[side] >= BallsPerInnings || _wickets[side] >= WicketsPerInnings;
            if (!inningsOver)
            {
                return false;
            }

            if (side == 0)
            {
                InningsIndex = 1;
                return true;
            }

            IsMatchOver = true;
            Winner = _runs[1] == _runs[0] ? -1 : 0;
            return true;
        }

        /// <summary>
        /// The result line, e.g. "WON BY 3 RUNS" or "WON BY 1 WICKET". Names are the caller's, so this
        /// stays free of team data. Empty until the match is over.
        /// </summary>
        public string ResultText(string nameA, string nameB)
        {
            if (!IsMatchOver)
            {
                return string.Empty;
            }

            if (Winner == -1)
            {
                return "MATCH TIED";
            }

            if (Winner == 0)
            {
                int margin = _runs[0] - _runs[1];
                return $"{nameA} WON BY {margin} RUN{(margin == 1 ? string.Empty : "S")}";
            }

            int wicketsLeft = WicketsPerInnings - _wickets[1];
            return $"{nameB} WON BY {wicketsLeft} WICKET{(wicketsLeft == 1 ? string.Empty : "S")}";
        }
    }
}
