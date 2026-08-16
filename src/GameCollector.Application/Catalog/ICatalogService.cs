using GameCollector.Application.Common;
using GameCollector.Contracts.Catalog;

namespace GameCollector.Application.Catalog;

public interface ICatalogService
{
    Task<Result<IReadOnlyList<GameSummaryDto>>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReferenceDataDto>>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReferenceDataDto>>> GetTagsAsync(CancellationToken cancellationToken = default);
}
