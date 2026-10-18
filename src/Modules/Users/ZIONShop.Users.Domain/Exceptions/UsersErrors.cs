using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Users.Domain.Exceptions;

public static class UsersErrors
{
    public static readonly Error ProfileNotFound = Error.NotFound("Users.ProfileNotFound", "User profile not found");
    public static readonly Error AddressNotFound = Error.NotFound("Users.AddressNotFound", "Address not found");
    public static readonly Error ProvinceNotFound = Error.NotFound("Users.ProvinceNotFound", "Province not found");
    public static readonly Error InvalidProvince = Error.Validation("Users.InvalidProvince", "Province is not a recognized administrative unit");
    public static readonly Error InvalidWard = Error.Validation("Users.InvalidWard", "Ward does not belong to the given province");
    public static readonly Error ProductNotFound = Error.NotFound("Users.ProductNotFound", "Sản phẩm không tồn tại hoặc chưa được công bố");
    public static readonly Error WishlistFull = Error.Validation("Users.WishlistFull", "Danh sách yêu thích đã đạt tối đa 200 sản phẩm");
}
