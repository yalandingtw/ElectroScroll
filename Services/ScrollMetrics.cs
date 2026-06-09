namespace ElectroScroll.Services;

public sealed record ScrollMetrics(
    double Speed,
    double Boost,
    double Velocity,
    double InputSignal,
    double OutputSignal,
    string ProfileName,
    string ProcessName,
    string WindowTitle,
    bool IsIntercepting,
    string Status,
    string Diagnostics);
