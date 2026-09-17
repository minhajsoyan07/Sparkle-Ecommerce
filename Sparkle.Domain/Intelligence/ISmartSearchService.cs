using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sparkle.Domain.Intelligence;

public interface ISmartSearchService
{
    Task<SearchQueryAnalysis> AnalyzeQueryAsync(string query, string? userId = null);
    Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, int count = 8);
    Task<List<string>> GetSpellCorrectionsAsync(string query);
    Task<List<int>> RankSearchResultsAsync(string query, List<int> productIds, string? userId = null);
    Task RecordSearchAsync(string userId, string query, int resultCount, int? clickedProductId = null);
}
