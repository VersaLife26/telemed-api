namespace TeleMed.Application.Admin.Finance;

public sealed record BillingSettingsDto(decimal? LkrPerUsd, bool HoldLkrWithinSixDays, bool HoldUsdWithinSixDays);

public sealed record UpdateBillingSettingsRequest(decimal LkrPerUsd);

public sealed record UpdateCardHoldRequest(bool HoldLkrWithinSixDays, bool HoldUsdWithinSixDays);
