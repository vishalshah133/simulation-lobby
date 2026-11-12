using SimulationLobby.Simulations.Brachistochrone.Kernel;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>
    /// The comp-001 "Racetrack" data: six rounds on a 16:9 frame (A high-left, B far right and slightly lower, so the
    /// cycloid has room to dip below B), and the pacing/text that goes with them. Pure data; applied to a fresh
    /// <see cref="BrachistochroneConfig"/> by the wide scene builder. Slot i keeps one colour for the whole video:
    /// 0 straight, 1 curve, 2 steep, 3 circle, 4 parabola, 5 deep, 6 sine, 7 wave.
    /// </summary>
    public static class BrachistochroneWidePreset
    {
        public const int SlotCount = 8;

        public static void Apply(BrachistochroneConfig c)
        {
            c.rounds = Rounds();
            c.gravity = 8f;
            c.frameWidth = 12.6f;
            c.pathSamples = 800;
            c.firstHoldSeconds = 7.5f;      // segment card (1.5s) + explainer (6s) play over this hold
            c.preLaunchHoldSeconds = 1.5f;  // the guess hold
            c.transitionSeconds = 0.6f;
            c.resultHoldSeconds = 1.25f;    // caption hold must stay >= 1.2s
            c.endCardSeconds = 3.5f;
            c.slowMoRadius = 0.7f;
            c.slowMoGap = 0.11f;
            c.slowMoRate = 0.3f;
            c.title = "RACETRACK";
            c.question = "WHICH SLIDE WINS?";
            c.holdCaption = "WHO WINS? GUESS!";
            c.endCard = "BRACHISTOCHRONE CURVE";
            c.endTitle = "GREEK FOR \"SHORTEST TIME\"";
            c.endLabel = "BRACHISTOCHRONE";
            c.endLine = "BRACHISTOS = SHORTEST,  CHRONOS = TIME";
            c.endLine2 = "THE FASTEST SLIDE ISN'T THE SHORTEST";
            c.intent = "comp-001 #7 Racetrack (wide). Six rounds, one question; the cycloid wins every round, " +
                       "including with a bump dented into it. Run lengths are estimated, not measured.";
        }

        public static BrachRound[] Rounds()
        {
            BrachTrack straight = T(CurveKind.Straight, "STRAIGHT", 0f);
            BrachTrack curve = T(CurveKind.Cycloid, "CURVE", 0f);
            BrachTrack bumped = T(CurveKind.BumpedCycloid, "BUMPY CURVE", 0.7f);
            BrachTrack steep = T(CurveKind.SteepDrop, "STEEP", 0.12f);
            BrachTrack circle = T(CurveKind.CircleArc, "CIRCLE", 0f);
            BrachTrack parabola = T(CurveKind.Parabola, "PARABOLA", 0f);
            BrachTrack deep = T(CurveKind.DeepDip, "DEEP", 1f);
            BrachTrack sine = T(CurveKind.Sine, "SINE", 0f);
            BrachTrack wave = T(CurveKind.Wave, "WAVE", 0f);

            return new[]
            {
                R(new[] { straight, curve }, 10f, 3f, "STRAIGHT vs CURVE", "STRAIGHT vs CURVE. WHO WINS?",
                  "THE CURVE WINS!", 3.0f),
                R(new[] { straight, curve, steep }, 10f, 3f, "NOW A STEEP DROP", "NOW A STEEP DROP. WHO WINS?",
                  "CURVE AGAIN!", 3.0f),
                R(new[] { straight, curve, steep, circle }, 10f, 3f, "NOW A CIRCLE", "NOW A CIRCLE. WHO WINS?",
                  "CURVE, BY A HAIR!", 3.3f),
                R(new[] { straight, curve, steep, circle }, 10.5f, 1.8f, "FINISH MOVES UP", "FINISH MOVES UP. WHO WINS?",
                  "STILL THE CURVE!", 4.0f, "CURVE GOES UNDER IT!"),
                R(new[] { straight, bumped, steep, circle, parabola }, 10f, 2.5f, "SABOTAGE: A BUMP ON THE CURVE",
                  "A BUMP ON THE CURVE! WHO WINS?", "STILL THE FASTEST!", 3.5f),
                R(new[] { straight, curve, steep, circle, parabola, deep, sine, wave }, 10f, 3f, "ALL 8 SHAPES",
                  "ALL 8 SHAPES. WHO WINS?", "CURVE. ALWAYS!", 3.1f)
            };
        }

        static BrachTrack T(CurveKind kind, string label, float shape)
        {
            return new BrachTrack { kind = kind, label = label, shape = shape };
        }

        static BrachRound R(BrachTrack[] tracks, float bx, float by, string caption, string hold, string result,
                            float expectedRun, string caption2 = "")
        {
            return new BrachRound
            {
                tracks = tracks,
                finishX = bx,
                finishY = by,
                caption = caption,
                caption2 = caption2,
                holdCaption = hold,
                result = result,
                result2 = string.Empty,
                showLengths = false,
                maxRunSeconds = 14f,
                expectedRunSeconds = expectedRun,
                resultSeconds = 0f
            };
        }
    }
}
