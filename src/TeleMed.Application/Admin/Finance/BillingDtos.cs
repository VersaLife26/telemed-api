namespace TeleMed.Application.Admin.Finance;

public sealed record BillingSettingsDto(decimal? LkrPerUsd);

public sealed record UpdateBillingSettingsRequest(decimal LkrPerUsd);
