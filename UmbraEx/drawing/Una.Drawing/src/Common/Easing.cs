namespace Una.Drawing;

public static class Easing
{
    #region Public API

    /// <summary>
    /// Delegate representing an easing function for float values.
    /// </summary>
    /// <param name="source">The starting value.</param>
    /// <param name="target">The ending value.</param>
    /// <param name="progress">The interpolation progress (usually 0.0 to 1.0).</param>
    /// <returns>The interpolated float value.</returns>
    public delegate float Function(float source, float target, double progress);

    /// <summary>
    /// Gets the appropriate easing function for float values based on the transition type.
    /// </summary>
    /// <param name="type">The desired transition type.</param>
    /// <returns>The corresponding FloatEasingFunction delegate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if no function is defined for the given TransitionType.</exception>
    public static Function GetEasingFunction(TransitionType type)
    {
        if (SFloatEasings.TryGetValue(type, out var function)) {
            return function;
        }

        throw new ArgumentOutOfRangeException(nameof(type),
            $"No float easing function defined for TransitionType.{type}");
    }

    #endregion

    #region Easing Function Implementations (Delegate compatible)

    private static float ApplyEase(float source, float target, double progress, Func<double, double> easingHelper)
    {
        var clampedProgress = Math.Clamp(progress, 0.0, 1.0);
        if (clampedProgress == 0.0) return source;
        if (clampedProgress == 1.0) return target;

        return source + (target - source) * (float)easingHelper(Math.Clamp(progress, 0.0, 1.0));
    }

    private static float Linear(float source, float target, double progress) =>
        ApplyEase(source, target, progress, t => t);

    private static float EaseInSine(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInSineHelper);

    private static float EaseOutSine(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutSineHelper);

    private static float EaseInOutSine(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutSineHelper);

    private static float EaseInQuad(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInQuadHelper);

    private static float EaseOutQuad(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutQuadHelper);

    private static float EaseInOutQuad(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutQuadHelper);

    private static float EaseInCubic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInCubicHelper);

    private static float EaseOutCubic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutCubicHelper);

    private static float EaseInOutCubic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutCubicHelper);

    private static float EaseInQuart(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInQuartHelper);

    private static float EaseOutQuart(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutQuartHelper);

    private static float EaseInOutQuart(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutQuartHelper);

    private static float EaseInQuint(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInQuintHelper);

    private static float EaseOutQuint(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutQuintHelper);

    private static float EaseInOutQuint(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutQuintHelper);

    private static float EaseInExpo(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInExpoHelper);

    private static float EaseOutExpo(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutExpoHelper);

    private static float EaseInOutExpo(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutExpoHelper);

    private static float EaseInCirc(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInCircHelper);

    private static float EaseOutCirc(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutCircHelper);

    private static float EaseInOutCirc(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutCircHelper);

    private static float EaseInBack(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInBackHelper);

    private static float EaseOutBack(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutBackHelper);

    private static float EaseInOutBack(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutBackHelper);

    private static float EaseInElastic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInElasticHelper);

    private static float EaseOutElastic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutElasticHelper);

    private static float EaseInOutElastic(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutElasticHelper);

    private static float EaseInBounce(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInBounceHelper);

    private static float EaseOutBounce(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseOutBounceHelper);

    private static float EaseInOutBounce(float source, float target, double progress) =>
        ApplyEase(source, target, progress, EaseInOutBounceHelper);

    #endregion

    #region Easing Function Helpers (Core Math)

    // Constants used in easing functions
    private const double Pi     = Math.PI;
    private const double HalfPi = Math.PI / 2.0;
    private const double C1     = 1.70158;
    private const double C2     = C1 * 1.525;
    private const double C3     = C1 + 1.0;
    private const double C4     = (2.0 * Pi) / 3.0;
    private const double C5     = (2.0 * Pi) / 4.5;
    private const double N1     = 7.5625;
    private const double D1     = 2.75;

    // --- Sine ---
    private static double EaseInSineHelper(double    t) => 1.0 - Math.Cos(t * HalfPi);
    private static double EaseOutSineHelper(double   t) => Math.Sin(t * HalfPi);
    private static double EaseInOutSineHelper(double t) => -(Math.Cos(Pi * t) - 1.0) / 2.0;

    // --- Quad ---
    private static double EaseInQuadHelper(double  t) => t * t;
    private static double EaseOutQuadHelper(double t) => 1.0 - (1.0 - t) * (1.0 - t);

    private static double EaseInOutQuadHelper(double t) =>
        t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2) / 2.0;

    // --- Cubic ---
    private static double EaseInCubicHelper(double  t) => t * t * t;
    private static double EaseOutCubicHelper(double t) => 1.0 - Pow3(1.0 - t);

    private static double EaseInOutCubicHelper(double t) =>
        t < 0.5 ? 4.0 * t * t * t : 1.0 - Pow3(-2.0 * t + 2.0) / 2.0;

    // --- Quart ---
    private static double EaseInQuartHelper(double  t) => t * t * t * t;
    private static double EaseOutQuartHelper(double t) => 1.0 - Pow4(1.0 - t);

    private static double EaseInOutQuartHelper(double t) =>
        t < 0.5 ? 8.0 * t * t * t * t : 1.0 - Pow4(-2.0 * t + 2.0) / 2.0;

    // --- Quint ---
    private static double EaseInQuintHelper(double  t) => t * t * t * t * t;
    private static double EaseOutQuintHelper(double t) => 1.0 - Pow5(1.0 - t);

    private static double EaseInOutQuintHelper(double t) =>
        t < 0.5 ? 16.0 * t * t * t * t * t : 1.0 - Pow5(-2.0 * t + 2.0) / 2.0;

    // --- Expo ---
    private static double EaseInExpoHelper(double  t) => t == 0 ? 0 : Math.Pow(2.0, 10.0 * t - 10.0);
    private static double EaseOutExpoHelper(double t) => t == 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t);

    private static double EaseInOutExpoHelper(double t) =>
        t == 0     ? 0
        : t == 1.0 ? 1.0
        : t < 0.5  ? Math.Pow(2.0, 20.0 * t - 10.0) / 2.0
                     : (2.0 - Math.Pow(2.0, -20.0 * t + 10.0)) / 2.0;

    // --- Circ ---
    private static double EaseInCircHelper(double  t) => 1.0 - Math.Sqrt(1.0 - Pow2(t));
    private static double EaseOutCircHelper(double t) => Math.Sqrt(1.0 - Pow2(t - 1.0));

    private static double EaseInOutCircHelper(double t) =>
        t < 0.5
            ? (1.0 - Math.Sqrt(1.0 - Pow2(2.0 * t))) / 2.0
            : (Math.Sqrt(1.0 - Pow2(-2.0 * t + 2.0)) + 1.0) / 2.0;

    // --- Back ---
    private static double EaseInBackHelper(double t) => C3 * t * t * t - C1 * t * t;

    private static double EaseOutBackHelper(double t)
    {
        double tm1        = t - 1.0;
        double tm1Squared = tm1 * tm1;

        return 1.0 + tm1Squared * (C3 * tm1 + C1);
    }

    private static double EaseInOutBackHelper(double t) =>
        t < 0.5
            ? (Pow2(2.0 * t) * ((C2 + 1.0) * 2.0 * t - C2)) / 2.0
            : (Pow2(2.0 * t - 2.0) * ((C2 + 1.0) * (t * 2.0 - 2.0) + C2) + 2.0) / 2.0;

    // --- Elastic ---
    private static double EaseInElasticHelper(double t) =>
        t == 0     ? 0
        : t == 1.0 ? 1.0
                     : -Math.Pow(2.0, 10.0 * t - 10.0) * Math.Sin((t * 10.0 - 10.75) * C4);

    private static double EaseOutElasticHelper(double t) =>
        t == 0     ? 0
        : t == 1.0 ? 1.0
                     : Math.Pow(2.0, -10.0 * t) * Math.Sin((t * 10.0 - 0.75) * C4) + 1.0;

    private static double EaseInOutElasticHelper(double t) =>
        t == 0
            ? 0
            : t == 1.0
                ? 1.0
                : t < 0.5
                    ? -(Math.Pow(2.0, 20.0 * t - 10.0) * Math.Sin((20.0 * t - 11.125) * C5)) / 2.0
                    : (Math.Pow(2.0, -20.0 * t + 10.0) * Math.Sin((20.0 * t - 11.125) * C5)) / 2.0 + 1.0;

    // --- Bounce ---
    private static double EaseOutBounceHelper(double t)
    {
        if (t < 1.0 / D1) {
            return N1 * t * t;
        }

        if (t < 2.0 / D1) {
            return N1 * (t -= 1.5 / D1) * t + 0.75;
        }

        if (t < 2.5 / D1) {
            return N1 * (t -= 2.25 / D1) * t + 0.9375;
        }

        return N1 * (t -= 2.625 / D1) * t + 0.984375;
    }

    private static double EaseInBounceHelper(double t) => 1.0 - EaseOutBounceHelper(1.0 - t);

    private static double EaseInOutBounceHelper(double t) =>
        t < 0.5
            ? (1.0 - EaseOutBounceHelper(1.0 - 2.0 * t)) / 2.0
            : (1.0 + EaseOutBounceHelper(2.0 * t - 1.0)) / 2.0;

    #endregion

    #region Power Helpers
    private static double Pow2(double t) => t * t;

    private static double Pow3(double t) => t * t * t;

    private static double Pow4(double t) => t * t * t * t;

    private static double Pow5(double t) => t * t * t * t * t;
    #endregion

    # region Lookup Table

    private static readonly Dictionary<TransitionType, Function> SFloatEasings;

    static Easing()
    {
        SFloatEasings = new Dictionary<TransitionType, Function> {
            { TransitionType.Linear, Linear },
            { TransitionType.EaseInSine, EaseInSine },
            { TransitionType.EaseOutSine, EaseOutSine },
            { TransitionType.EaseInOutSine, EaseInOutSine },
            { TransitionType.EaseInQuad, EaseInQuad },
            { TransitionType.EaseOutQuad, EaseOutQuad },
            { TransitionType.EaseInOutQuad, EaseInOutQuad },
            { TransitionType.EaseInCubic, EaseInCubic },
            { TransitionType.EaseOutCubic, EaseOutCubic },
            { TransitionType.EaseInOutCubic, EaseInOutCubic },
            { TransitionType.EaseInQuart, EaseInQuart },
            { TransitionType.EaseOutQuart, EaseOutQuart },
            { TransitionType.EaseInOutQuart, EaseInOutQuart },
            { TransitionType.EaseInQuint, EaseInQuint },
            { TransitionType.EaseOutQuint, EaseOutQuint },
            { TransitionType.EaseInOutQuint, EaseInOutQuint },
            { TransitionType.EaseInExpo, EaseInExpo },
            { TransitionType.EaseOutExpo, EaseOutExpo },
            { TransitionType.EaseInOutExpo, EaseInOutExpo },
            { TransitionType.EaseInCirc, EaseInCirc },
            { TransitionType.EaseOutCirc, EaseOutCirc },
            { TransitionType.EaseInOutCirc, EaseInOutCirc },
            { TransitionType.EaseInBack, EaseInBack },
            { TransitionType.EaseOutBack, EaseOutBack },
            { TransitionType.EaseInOutBack, EaseInOutBack },
            { TransitionType.EaseInElastic, EaseInElastic },
            { TransitionType.EaseOutElastic, EaseOutElastic },
            { TransitionType.EaseInOutElastic, EaseInOutElastic },
            { TransitionType.EaseInBounce, EaseInBounce },
            { TransitionType.EaseOutBounce, EaseOutBounce },
            { TransitionType.EaseInOutBounce, EaseInOutBounce }
        };
    }

    #endregion
}