using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClassScheduleApp.Services;

/// <summary>
/// 主题服务实现：替换 Application 资源字典里的主题词典，并把窗口背景同步成主题色。
/// 浅色主题只提供调色板（界面与之前完全一致）；深色主题额外覆盖常用控件的隐式样式。
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string LightDictionaryPath = "/Themes/LightTheme.xaml";
    private const string DarkDictionaryPath = "/Themes/DarkTheme.xaml";
    private const string ControlStylesPath = "/Themes/ControlStyles.xaml";

    /// <summary>窗口背景画刷的资源键。</summary>
    public const string WindowBackgroundKey = "App.Brush.Window";

    private static readonly Uri LightUri = new(LightDictionaryPath, UriKind.Relative);
    private static readonly Uri DarkUri = new(DarkDictionaryPath, UriKind.Relative);
    private static readonly Uri ControlStylesUri = new(ControlStylesPath, UriKind.Relative);

    private ResourceDictionary? _currentDictionary;
    private ResourceDictionary? _sharedStyles;
    private bool _classHandlerRegistered;

    public ThemeService()
    {
        // 之后打开的每个窗口（含各弹窗）在 Loaded 时统一套用主题背景
        if (!_classHandlerRegistered)
        {
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
            _classHandlerRegistered = true;
        }
    }

    /// <inheritdoc />
    public string CurrentMode { get; private set; } = AppThemeModes.System;

    /// <inheritdoc />
    public bool IsDarkEffective { get; private set; }

    /// <inheritdoc />
    public void Apply(string mode)
    {
        CurrentMode = AppThemeModes.Normalize(mode);
        var dark = CurrentMode switch
        {
            AppThemeModes.Dark => true,
            AppThemeModes.Light => false,
            _ => IsSystemDark(),
        };

        IsDarkEffective = dark;

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;

        // 控件外观词典只挂一次：它与主题无关，靠 DynamicResource 跟随调色板变色
        if (_sharedStyles is null)
        {
            _sharedStyles = new ResourceDictionary { Source = ControlStylesUri };
            dictionaries.Add(_sharedStyles);
        }

        if (_currentDictionary is not null)
        {
            dictionaries.Remove(_currentDictionary);
        }

        var dictionary = new ResourceDictionary { Source = dark ? DarkUri : LightUri };
        dictionaries.Add(dictionary);
        _currentDictionary = dictionary;

        ApplyWindowBackgrounds(dark);
    }

    /// <inheritdoc />
    public void Reapply()
    {
        Apply(CurrentMode);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            window.Background = ResolveWindowBrush();
        }
    }

    private void ApplyWindowBackgrounds(bool dark)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var brush = ResolveWindowBrush();
        foreach (Window window in application.Windows)
        {
            window.Background = brush;
        }
    }

    private Brush ResolveWindowBrush()
    {
        var application = Application.Current;
        if (application is not null &&
            application.TryFindResource(WindowBackgroundKey) is Brush brush)
        {
            return brush;
        }

        return IsDarkEffective ? Brushes.Black : Brushes.White;
    }

    /// <summary>读取 Windows 个性化设置：AppsUseLightTheme = 0 表示系统使用深色。</summary>
    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int light && light == 0;
        }
        catch (Exception)
        {
            // 读不到注册表时按浅色处理（不因系统探测失败影响启动）
            return false;
        }
    }
}
