using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Notifications;

public sealed class AdminNotificationQueryValidator : AbstractValidator<AdminNotificationQuery>
{
    public AdminNotificationQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
