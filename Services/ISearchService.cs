using DoWeHaveItApp.Dtos;

namespace DoWeHaveItApp.Services;

public interface ISearchService
{
    Task<SearchResultDto> SearchItemsAsync(string userId, string query);
}
