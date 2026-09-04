namespace Prismica.Core.Scheduling;

public static class Easing
{
    public static double Linear(double t) => t;
    public static double EaseInQuad(double t) => t * t;
    public static double EaseOutQuad(double t) => 1 - (1 - t) * (1 - t);
    public static double EaseInOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    public static double EaseInCubic(double t) => t * t * t;
    public static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
    public static double EaseInOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    public static double EaseInQuart(double t) => t * t * t * t;
    public static double EaseOutQuart(double t) => 1 - Math.Pow(1 - t, 4);
    public static double EaseInOutQuart(double t) => t < 0.5 ? 8 * t * t * t * t : 1 - Math.Pow(-2 * t + 2, 4) / 2;
    public static double EaseInExpo(double t) => t == 0 ? 0 : Math.Pow(2, 10 * (t - 1));
    public static double EaseOutExpo(double t) => t == 1 ? 1 : 1 - Math.Pow(2, -10 * t);
    public static double EaseInOutExpo(double t) =>
        t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ? Math.Pow(2, 20 * t - 10) / 2 : (2 - Math.Pow(2, -20 * t + 10)) / 2;
    public static double EaseInSine(double t) => 1 - Math.Cos(t * Math.PI / 2);
    public static double EaseOutSine(double t) => Math.Sin(t * Math.PI / 2);
    public static double EaseInOutSine(double t) => -(Math.Cos(Math.PI * t) - 1) / 2;
    public static double EaseInCirc(double t) => 1 - Math.Sqrt(1 - t * t);
    public static double EaseOutCirc(double t) => Math.Sqrt(1 - Math.Pow(t - 1, 2));
    public static double EaseInOutCirc(double t) =>
        t < 0.5 ? (1 - Math.Sqrt(1 - Math.Pow(2 * t, 2))) / 2 : (Math.Sqrt(1 - Math.Pow(-2 * t + 2, 2)) + 1) / 2;
    public static double EaseInBack(double t) => 2.70158 * t * t * t - 1.70158 * t * t;
    public static double EaseOutBack(double t) => 1 + 2.70158 * Math.Pow(t - 1, 3) + 1.70158 * Math.Pow(t - 1, 2);
    public static double EaseInOutBack(double t) =>
        t < 0.5 ? Math.Pow(2 * t, 2) * (3.70158 * 2 * t - 2.70158) / 2 : (Math.Pow(2 * t - 2, 2) * (3.70158 * (2 * t - 2) + 2.70158) + 2) / 2;
    public static double EaseInElastic(double t)
    {
        if (t == 0) return 0;
        if (t == 1) return 1;
        return -Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * 2 * Math.PI / 3);
    }
    public static double EaseOutElastic(double t)
    {
        if (t == 0) return 0;
        if (t == 1) return 1;
        return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * 2 * Math.PI / 3) + 1;
    }
    public static double EaseInOutElastic(double t)
    {
        if (t == 0) return 0;
        if (t == 1) return 1;
        return t < 0.5
            ? -Math.Pow(2, 20 * t - 10) * Math.Sin((20 * t - 11.125) * 2 * Math.PI / 4.5) / 2
            : Math.Pow(2, -20 * t + 10) * Math.Sin((20 * t - 11.125) * 2 * Math.PI / 4.5) / 2 + 1;
    }
    public static double EaseInBounce(double t) => 1 - EaseOutBounce(1 - t);
    public static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625, d1 = 2.75;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
        if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
        return n1 * (t -= 2.625 / d1) * t + 0.984375;
    }
    public static double EaseInOutBounce(double t) =>
        t < 0.5 ? EaseInBounce(t * 2) / 2 : EaseOutBounce(t * 2 - 1) / 2 + 0.5;
}