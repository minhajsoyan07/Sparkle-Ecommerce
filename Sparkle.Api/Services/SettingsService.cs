using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Sparkle.Domain.Configuration;
using Sparkle.Infrastructure;
using System.Text.Json;

namespace Sparkle.Api.Services;

public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private const string CacheKeyPrefix = "SiteSetting_";

    public SettingsService(ApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var cacheKey = CacheKeyPrefix + key;
        var cachedValue = await _cache.GetStringAsync(cacheKey);

        if (cachedValue != null)
        {
            return cachedValue;
        }

        var setting = await _context.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (setting != null)
        {
            await _cache.SetStringAsync(cacheKey, setting.Value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });
            return setting.Value;
        }

        return defaultValue;
    }

    public async Task<T> GetValueAsync<T>(string key, T defaultValue)
    {
        var valueStr = await GetValueAsync(key, string.Empty);

        if (string.IsNullOrEmpty(valueStr))
        {
            return defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(valueStr, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetStringValueAsync(string key, string value, string group = "General", string dataType = "string", string? description = null)
    {
        var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            setting = new SiteSetting
            {
                Key = key,
                Value = value,
                Group = group,
                DataType = dataType,
                Description = description
            };
            _context.SiteSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            // Only update metadata if provided and not empty
            if (!string.IsNullOrEmpty(group)) setting.Group = group;
            if (!string.IsNullOrEmpty(dataType)) setting.DataType = dataType;
            if (!string.IsNullOrEmpty(description)) setting.Description = description;
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeyPrefix + key);
    }

    public async Task SetValueAsync<T>(string key, T value, string group = "General", string? description = null)
    {
        string valueStr = value?.ToString() ?? "";
        string dataType = typeof(T).Name.ToLower();
        
        if (typeof(T) == typeof(bool)) dataType = "bool";
        if (typeof(T) == typeof(int)) dataType = "int";
        if (typeof(T) == typeof(decimal)) dataType = "decimal";

        await SetStringValueAsync(key, valueStr, group, dataType, description);
    }

    public async Task<List<SiteSetting>> GetAllSettingsAsync()
    {
        return await _context.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Group)
            .ThenBy(s => s.Key)
            .ToListAsync();
    }

    public async Task<List<SiteSetting>> GetSettingsByGroupAsync(string group)
    {
        return await _context.SiteSettings
            .AsNoTracking()
            .Where(s => s.Group == group)
            .OrderBy(s => s.Key)
            .ToListAsync();
    }

    /// <summary>
    /// Batch fetch multiple settings in a single query for performance
    /// </summary>
    public async Task<Dictionary<string, string>> GetBatchAsync(string[] keys)
    {
        var result = new Dictionary<string, string>();
        var keysToFetch = new List<string>();

        // First, check cache for each key
        foreach (var key in keys)
        {
            var cacheKey = CacheKeyPrefix + key;
            var cachedValue = await _cache.GetStringAsync(cacheKey);
            if (cachedValue != null)
            {
                result[key] = cachedValue;
            }
            else
            {
                keysToFetch.Add(key);
            }
        }

        // If all were cached, return immediately
        if (!keysToFetch.Any())
        {
            return result;
        }

        // Fetch remaining from database in one query
        var settings = await _context.SiteSettings
            .AsNoTracking()
            .Where(s => keysToFetch.Contains(s.Key))
            .ToListAsync();

        // Cache and add to result
        foreach (var setting in settings)
        {
            result[setting.Key] = setting.Value;
            await _cache.SetStringAsync(CacheKeyPrefix + setting.Key, setting.Value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });
        }

        return result;
    }
}
