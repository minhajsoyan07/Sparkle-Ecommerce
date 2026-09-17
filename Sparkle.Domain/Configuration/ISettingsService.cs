namespace Sparkle.Domain.Configuration;

public interface ISettingsService
{
    Task<string> GetValueAsync(string key, string defaultValue = "");
    Task<T> GetValueAsync<T>(string key, T defaultValue);
    Task SetStringValueAsync(string key, string value, string group = "General", string dataType = "string", string? description = null);
    Task SetValueAsync<T>(string key, T value, string group = "General", string? description = null);
    Task<List<SiteSetting>> GetAllSettingsAsync();
    Task<List<SiteSetting>> GetSettingsByGroupAsync(string group);
    Task<Dictionary<string, string>> GetBatchAsync(string[] keys);
}
