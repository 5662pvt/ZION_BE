namespace ZIONShop.Common.Pagination;

public class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch { < 1 => 20, > 100 => 100, _ => value };
    }

    public string? Sort { get; set; }
    public string? Filter { get; set; }
}
