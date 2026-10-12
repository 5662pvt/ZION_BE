using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Domain.Exceptions;

public static class ReviewsErrors
{
    public static readonly Error NotEligible = Error.Forbidden("Reviews.NotEligible", "Bạn cần mua và nhận hàng sản phẩm này trước khi đánh giá");
    public static readonly Error AlreadyReviewed = Error.Conflict("Reviews.AlreadyReviewed", "Bạn đã đánh giá sản phẩm này");
    public static readonly Error ReviewNotFound = Error.NotFound("Reviews.ReviewNotFound", "Đánh giá không tồn tại");
    public static readonly Error ProductNotAvailable = Error.NotFound("Reviews.ProductNotAvailable", "Sản phẩm không tồn tại hoặc chưa được công bố");
    public static readonly Error AlreadyHidden = Error.Conflict("Reviews.AlreadyHidden", "Đánh giá đã bị ẩn");
    public static readonly Error AlreadyPublished = Error.Conflict("Reviews.AlreadyPublished", "Đánh giá đang được hiển thị");
    public static readonly Error TooManyProductIds = Error.Validation("Reviews.TooManyProductIds", "Tối đa 100 productId mỗi lần");
}
