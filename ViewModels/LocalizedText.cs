using System.ComponentModel;
using ElectroScroll.Models;

namespace ElectroScroll.ViewModels;

public sealed class LocalizedText : INotifyPropertyChanged
{
    private static readonly Dictionary<string, (string En, string Zh)> Catalog = new()
    {
        ["AppSubtitle"] = ("Velocity-aware wheel acceleration with a flywheel tail.", "依滾輪速度啟動慣性，模擬電磁飛輪手感。"),
        ["Enabled"] = ("Enabled", "啟用"),
        ["Modifiers"] = ("Modifiers", "修飾鍵"),
        ["Fullscreen"] = ("Fullscreen", "全螢幕"),
        ["Games"] = ("Games", "遊戲"),
        ["Log"] = ("Log", "記錄"),
        ["Language"] = ("Language", "語言"),

        ["Presets"] = ("Presets", "預設"),
        ["Precise"] = ("Precise", "精準"),
        ["Balanced"] = ("Balanced", "平衡"),
        ["FreeSpin"] = ("Free-spin", "飛輪"),
        ["Physics"] = ("Physics", "物理參數"),
        ["ResetMotion"] = ("Reset motion", "清除慣性"),
        ["Save"] = ("Save", "儲存"),

        ["Step"] = ("Step", "單格距離"),
        ["StepHelp"] = ("Base distance per wheel tick. Higher values move farther even before inertia starts.", "每一格滾輪的基礎距離。調高會讓一般滾動也更遠，還沒進入慣性前就會變快。"),
        ["Threshold"] = ("Threshold", "觸發門檻"),
        ["ThresholdHelp"] = ("Wheel speed needed before ElectroScroll takes over. Higher values keep slow scrolling native; lower values trigger inertia sooner.", "進入慣性的速度門檻。調高可保留慢滾精準；調低會更早觸發慣性。"),
        ["Acceleration"] = ("Acceleration", "加速斜率"),
        ["AccelerationHelp"] = ("How strongly extra speed turns into boost after crossing the threshold. Higher values feel punchier.", "超過門檻後速度轉成 boost 的力道。調高會更有衝勁，也更容易暴衝。"),
        ["MaxBoost"] = ("Max boost", "Boost 上限"),
        ["MaxBoostHelp"] = ("Hard cap for acceleration. Lower it to prevent long pages from jumping too far.", "加速倍率上限。調低可避免長頁面一次跳太遠。"),
        ["ImpulseTime"] = ("Impulse time", "脈衝時間"),
        ["ImpulseTimeHelp"] = ("How quickly a wheel burst becomes inertia velocity. Lower values react faster; higher values feel softer.", "滾輪爆發轉成慣性速度的時間。調低反應更快；調高較柔和。"),
        ["Friction"] = ("Friction", "摩擦時間"),
        ["FrictionHelp"] = ("How long inertia keeps moving before fading. Higher values glide longer; lower values stop sooner.", "慣性衰退前可滑行多久。調高尾巴更長；調低更快停住。"),
        ["Flywheel"] = ("Flywheel", "飛輪慣量"),
        ["FlywheelHelp"] = ("Extra tail after a fast burst. Higher values feel closer to free-spin; lower values feel controlled.", "快速撥動後額外保留的滑行尾巴。調高更像自由飛輪；調低更容易控制。"),
        ["DirectShare"] = ("Direct share", "直接位移"),
        ["DirectShareHelp"] = ("How much of each burst is applied immediately instead of becoming inertia. Higher values feel more direct; lower values feel floatier.", "每次快速滾動有多少立即位移，而不是轉成慣性。調高更跟手；調低更飄、更像飛輪。"),
        ["Smoothness"] = ("Smoothness", "平滑分段"),
        ["SmoothnessHelp"] = ("How many smaller wheel packets are emitted. Higher values look smoother in modern apps; lower values are safer for legacy apps.", "把慣性拆成多少小段輸出。調高在現代 app 更平滑；調低對舊程式較相容。"),

        ["Speed"] = ("Speed", "速度"),
        ["Boost"] = ("Boost", "Boost"),
        ["Velocity"] = ("Velocity", "慣性速度"),
        ["Mode"] = ("Mode", "模式"),
        ["Charts"] = ("Monitor", "監測線圖"),
        ["ShowCharts"] = ("Draw charts", "啟用線圖"),
        ["InputSignal"] = ("Input", "輸入"),
        ["OutputSignal"] = ("Output", "輸出"),
        ["ChartsHelp"] = ("Charts are sampled and drawn only while enabled.", "只有啟用時才取樣與繪圖。"),
        ["Target"] = ("Target", "目標"),
        ["Profile"] = ("Profile", "設定檔"),
        ["Process"] = ("Process", "程序"),
        ["Window"] = ("Window", "視窗"),
        ["Diagnostics"] = ("Diagnostics", "診斷"),

        ["ImplementationNotes"] = ("Implementation notes", "實作備註"),
        ["ImplementationNote1"] = ("Low-speed wheel input is passed through unchanged. ElectroScroll only intercepts once wheel speed crosses the threshold, then emits smaller wheel packets over time with exponential decay. Modifier keys are bypassed so Ctrl+wheel zoom and Shift+wheel behaviors stay native.", "低速滾輪輸入會原樣放行。只有速度超過門檻時，ElectroScroll 才會接管，並依指數衰退送出較小的滾輪封包。修飾鍵會被放行，因此 Ctrl+滾輪縮放與 Shift+滾輪仍維持原生行為。"),
        ["CompatibilityNote"] = ("If an older application ignores smooth packets, lower Smoothness toward 1 for compatibility. Browser and Electron apps usually benefit from 3-5 steps.", "如果舊程式不吃平滑分段，請把平滑分段降到 1。瀏覽器與 Electron 類 app 通常適合 3-5 段。"),
        ["SettingsLoaded"] = ("Settings are loaded from AppData", "已從 AppData 載入設定"),
        ["PreciseApplied"] = ("Precise preset applied", "已套用精準預設"),
        ["BalancedApplied"] = ("Balanced preset applied", "已套用平衡預設"),
        ["FreeSpinApplied"] = ("Free-spin preset applied", "已套用飛輪預設"),
        ["Saved"] = ("Saved", "已儲存"),
        ["LanguageChanged"] = ("Language changed", "語言已切換"),
        ["PerformanceOff"] = ("Performance mode: off", "效能模式：關閉"),
        ["PerformanceMode"] = ("Performance mode", "效能模式"),
        ["Timer"] = ("timer", "計時器"),
        ["Default"] = ("default", "預設"),
        ["EcoQos"] = ("EcoQoS", "EcoQoS"),
        ["Off"] = ("off", "關閉"),
        ["Unchanged"] = ("unchanged", "未變更"),
        ["GcLowLatency"] = ("GC=low latency", "GC=低延遲")
    };

    private UiLanguage _language;

    public LocalizedText(UiLanguage language)
    {
        _language = language;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key]
    {
        get
        {
            if (!Catalog.TryGetValue(key, out var value))
            {
                return key;
            }

            return _language == UiLanguage.TraditionalChinese ? value.Zh : value.En;
        }
    }
}
