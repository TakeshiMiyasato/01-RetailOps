using RetailOps.Domain.Enums;

namespace RetailOps.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class UnauthorizedException(string message) : Exception(message);

public static class StoreAccess
{
    /// <summary>Admins can operate any store; managers and cashiers only the store they are assigned to.</summary>
    public static void EnsureCanOperate(this ICurrentUser user, Guid storeId)
    {
        if (user.Role == UserRole.Admin) return;
        if (user.StoreId != storeId)
            throw new ForbiddenException("You can only operate on your assigned store.");
    }

    /// <summary>Returns the store a query should be scoped to: the requested one for admins, the assigned one otherwise.</summary>
    public static Guid? ScopeStore(this ICurrentUser user, Guid? requestedStoreId) =>
        user.Role == UserRole.Admin ? requestedStoreId : user.StoreId;
}

public static class DocumentNumber
{
    public static string Next(string prefix, string storeCode, DateTimeOffset now) =>
        $"{prefix}-{storeCode}-{now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
}
