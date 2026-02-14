using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Repositories;

namespace DoWeHaveItApp.Services;

public sealed class SearchService : ISearchService
{
    private readonly IInventoryRepository _repository;

    public SearchService(IInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchResultDto> SearchItemsAsync(string userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultDto { Items = Array.Empty<ItemDto>() };
        }

        var items = await _repository.SearchItemsAsync(userId, query);
        var itemDtos = items.Select(DtoMapper.ToDto).ToList();

        return new SearchResultDto { Items = itemDtos };
    }
}
