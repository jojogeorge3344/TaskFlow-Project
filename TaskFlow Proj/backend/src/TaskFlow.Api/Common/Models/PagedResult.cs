namespace TaskFlow.Api.Common.Models;

/// <summary>One page of results with the total count and page metadata (TDD §8.4).</summary>
public record PagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);
