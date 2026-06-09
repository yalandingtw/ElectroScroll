namespace ElectroScroll.Models;

public sealed class ScrollTuning
{
    public double Step { get; set; } = 1.0;
    public double Threshold { get; set; } = 0.035;
    public double Acceleration { get; set; } = 18.0;
    public double MaxBoost { get; set; } = 4.5;
    public double KickMs { get; set; } = 110.0;
    public double FrictionMs { get; set; } = 320.0;
    public double Flywheel { get; set; } = 0.55;
    public double DirectShare { get; set; } = 38.0;
    public int Smoothness { get; set; } = 4;

    public ScrollTuning Clone()
    {
        return new ScrollTuning
        {
            Step = Step,
            Threshold = Threshold,
            Acceleration = Acceleration,
            MaxBoost = MaxBoost,
            KickMs = KickMs,
            FrictionMs = FrictionMs,
            Flywheel = Flywheel,
            DirectShare = DirectShare,
            Smoothness = Smoothness
        };
    }

    public static ScrollTuning Precise()
    {
        return new ScrollTuning
        {
            Step = 0.86,
            Threshold = 0.034,
            Acceleration = 16.0,
            MaxBoost = 3.8,
            KickMs = 118,
            FrictionMs = 300,
            Flywheel = 0.42,
            DirectShare = 42,
            Smoothness = 4
        };
    }

    public static ScrollTuning DesktopPrecise()
    {
        return new ScrollTuning
        {
            Step = 0.94,
            Threshold = 0.03,
            Acceleration = 18.0,
            MaxBoost = 4.2,
            KickMs = 108,
            FrictionMs = 360,
            Flywheel = 0.52,
            DirectShare = 36,
            Smoothness = 4
        };
    }

    public static ScrollTuning WebPrecise()
    {
        return new ScrollTuning
        {
            Step = 0.84,
            Threshold = 0.045,
            Acceleration = 12.5,
            MaxBoost = 3.1,
            KickMs = 132,
            FrictionMs = 260,
            Flywheel = 0.34,
            DirectShare = 50,
            Smoothness = 3
        };
    }

    public static ScrollTuning Balanced()
    {
        return new ScrollTuning();
    }

    public static ScrollTuning FreeSpin()
    {
        return new ScrollTuning
        {
            Step = 1.05,
            Threshold = 0.026,
            Acceleration = 26.0,
            MaxBoost = 7.0,
            KickMs = 92,
            FrictionMs = 520,
            Flywheel = 0.86,
            DirectShare = 24,
            Smoothness = 5
        };
    }
}
