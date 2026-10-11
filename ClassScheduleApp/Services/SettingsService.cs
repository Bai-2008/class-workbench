using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassScheduleApp.Data;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 data/settings.json 的设置实现（模块可见性 + 主题模式）。
/// 只序列化用户显式改过的项；文件不存在或损坏时自动忽略并按默认值运行，不抛异常。
/// 兼容旧格式（顶层直接是"模块 Id → 是否可见"的对象）。
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>用户覆盖值：模块 Id → 是否可见。只保存显式改过的项。</summary>
    private readonly Dictionary<string, bool> _overrides;

    /// <summary>主题模式（Light / Dark / System）。</summary>
    private string _themeMode = AppThemeModes.System;

    private readonly string _filePath;

    public SettingsService()
    {
        _filePath = ResolveFilePath();
        _overrides = LoadOverrides(_filePath);
        _themeMode = LoadThemeMode(_filePath);
    }

    /// <inheritdoc />
    public bool GetEffectiveVisibility(FeatureModule module)
    {
        return _overrides.TryGetValue(module.Id, out var isVisible)
            ? isVisible
            : module.IsVisibleByDefault;
    }

    /// <inheritdoc />
    public void SetModuleVisibility(string moduleId, bool isVisible)
    {
        _overrides[moduleId] = isVisible;
        SaveOverrides();
    }

    /// <inheritdoc />
    public void ResetToDefaults()
    {
        _overrides.Clear();
        _themeMode = AppThemeModes.System;

        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException ex)
        {
            // 删不掉也不影响本次运行：内存里已恢复默认
            Debug.WriteLine($"删除设置文件失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"删除设置文件失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public string GetThemeMode()
    {
        return AppThemeModes.Normalize(_themeMode);
    }

    /// <inheritdoc />
    public void SetThemeMode(string mode)
    {
        _themeMode = AppThemeModes.Normalize(mode);
        SaveOverrides();
    }

    /// <summary>设置文件与数据库放在同一个 data 目录下。</summary>
    private static string ResolveFilePath()
    {
        var databaseFilePath = DatabasePathProvider.GetDatabaseFilePath();
        var directory = Path.GetDirectoryName(databaseFilePath);
        return string.IsNullOrEmpty(directory)
            ? Path.Combine(AppContext.BaseDirectory, SettingsFileName)
            : Path.Combine(directory, SettingsFileName);
    }

    /// <summary>读模块覆盖值：支持新格式的 Modules 段，也支持旧格式（顶层即模块表）。</summary>
    private static Dictionary<string, bool> LoadOverrides(string filePath)
    {
        var result = new Dictionary<string, bool>();
        var root = ReadRoot(filePath);
        if (root is null)
        {
            return result;
        }

        // 新格式读 Modules 段；旧格式（顶层直接是模块表）回退到根对象
        var modules = root["Modules"] as JsonObject ?? root as JsonObject;
        if (modules is null)
        {
            return result;
        }

        foreach (var pair in modules)
        {
            if (pair.Value is JsonValue value && value.TryGetValue<bool>(out var flag))
            {
                result[pair.Key] = flag;
            }
        }

        return result;
    }

    /// <summary>读主题模式。</summary>
    private static string LoadThemeMode(string filePath)
    {
        var root = ReadRoot(filePath);
        var theme = root?["Theme"]?.GetValue<string>();
        return AppThemeModes.Normalize(theme);
    }

    private static JsonNode? ReadRoot(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            // 文件损坏：忽略覆盖值，按默认值运行
            Debug.WriteLine($"设置文件格式错误，已按默认值运行：{ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"读取设置文件失败，已按默认值运行：{ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"读取设置文件失败，已按默认值运行：{ex.Message}");
            return null;
        }
    }

    private void SaveOverrides()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var modules = new JsonObject();
            foreach (var pair in _overrides)
            {
                modules[pair.Key] = pair.Value;
            }

            var root = new JsonObject
            {
                ["Modules"] = modules,
                ["Theme"] = _themeMode,
            };

            File.WriteAllText(_filePath, root.ToJsonString(SerializerOptions));
        }
        catch (IOException ex)
        {
            // 写不进去也不能让程序崩溃：本次运行内存里仍然生效
            Debug.WriteLine($"保存设置文件失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"保存设置文件失败：{ex.Message}");
        }
    }
}
