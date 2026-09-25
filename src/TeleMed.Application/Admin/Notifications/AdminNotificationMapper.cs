using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Notifications;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class AdminNotificationMapper
{
    public static partial AdminNotificationDto ToDto(this AdminNotification notification);
}
