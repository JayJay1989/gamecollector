using GameCollector.Application.Common;
using GameCollector.Contracts.Media;

namespace GameCollector.Application.ExternalCatalog;

public interface IExternalProductLookupService
{
    Task<Result<ProductMetadataCandidateDto>> LookupAsync(string barcode, CancellationToken cancellationToken = default);
}
