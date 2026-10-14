namespace ZIONShop.Auth.Authorization;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string Staff = "Staff";
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string StaffOrAdmin = "StaffOrAdmin";
    public const string Authenticated = "Authenticated";
}
