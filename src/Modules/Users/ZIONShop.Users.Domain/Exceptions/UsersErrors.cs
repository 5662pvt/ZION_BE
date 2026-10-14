using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Users.Domain.Exceptions;

public static class UsersErrors
{
    public static readonly Error ProfileNotFound = Error.NotFound("Users.ProfileNotFound", "User profile not found");
    public static readonly Error AddressNotFound = Error.NotFound("Users.AddressNotFound", "Address not found");
}
