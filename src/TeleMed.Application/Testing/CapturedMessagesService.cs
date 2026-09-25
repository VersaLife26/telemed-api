using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;

namespace TeleMed.Application.Testing;

public sealed class CapturedMessagesService(ICapturedMessageRepository repository, IUnitOfWork unitOfWork)
{
    private const int ListLimit = 100;

    public async Task<IReadOnlyList<CapturedMessageDto>> ListAsync(CapturedMessageQuery query, CancellationToken ct) =>
        (await repository.ListNewestFirstAsync(query.Recipient, query.Channel, ListLimit, ct)).Select(m => m.ToDto()).ToList();

    public async Task<CapturedMessageDto> LatestAsync(CapturedMessageQuery query, CancellationToken ct) =>
        (await repository.ListNewestFirstAsync(query.Recipient, query.Channel, 1, ct)).SingleOrDefault()?.ToDto()
        ?? throw new NotFoundException("No captured message matches.");

    public async Task ClearAsync(CancellationToken ct)
    {
        await repository.RemoveAllAsync(ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
