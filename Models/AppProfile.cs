namespace ElectroScroll.Models;

public sealed class AppProfile
{
    public string Name { get; set; } = "Default";
    public bool Enabled { get; set; } = true;
    public List<string> ProcessNames { get; set; } = [];
    public ScrollTuning Tuning { get; set; } = ScrollTuning.Balanced();

    public bool Matches(string processName)
    {
        if (ProcessNames.Count == 0)
        {
            return true;
        }

        return ProcessNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
    }
}
