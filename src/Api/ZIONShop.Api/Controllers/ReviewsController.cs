using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Reviews.Application.Features.AdminSearchReviews;
using ZIONShop.Reviews.Application.Features.AnswerProductQuestion;
using ZIONShop.Reviews.Application.Features.AskProductQuestion;
using ZIONShop.Reviews.Application.Features.CreateReview;
using ZIONShop.Reviews.Application.Features.DeleteProductQuestion;
using ZIONShop.Reviews.Application.Features.DeleteReview;
using ZIONShop.Reviews.Application.Features.GetMyReviews;
using ZIONShop.Reviews.Application.Features.GetProductQuestions;
using ZIONShop.Reviews.Application.Features.GetProductReviews;
using ZIONShop.Reviews.Application.Features.GetReviewEligibility;
using ZIONShop.Reviews.Application.Features.GetReviewSummaries;
using ZIONShop.Reviews.Application.Features.GetReviewSummary;
using ZIONShop.Reviews.Application.Features.HideReview;
using ZIONShop.Reviews.Application.Features.RemoveReviewReply;
using ZIONShop.Reviews.Application.Features.ReplyToReview;
using ZIONShop.Reviews.Application.Features.ShowReview;
using ZIONShop.Reviews.Application.Features.UpdateReview;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ReviewsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private bool IsStaffOrAdmin => _currentUser.Roles.Contains(Roles.Staff) || _currentUser.Roles.Contains(Roles.Admin);

    [HttpGet("products/{productId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductReviews(Guid productId, [FromQuery] GetProductReviewsRequest req, CancellationToken ct)
    {
        var query = new GetProductReviewsQuery(productId, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Rating);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("products/{productId:guid}/reviews/summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummary(Guid productId, CancellationToken ct)
        => (await _mediator.Send(new GetReviewSummaryQuery(productId), ct)).ToActionResult();

    [HttpGet("reviews/summaries")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummaries([FromQuery] string? productIds, CancellationToken ct)
    {
        var ids = (productIds ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        return (await _mediator.Send(new GetReviewSummariesQuery(ids), ct)).ToActionResult();
    }

    [HttpGet("products/{productId:guid}/reviews/eligibility")]
    [Authorize]
    public async Task<IActionResult> GetEligibility(Guid productId, CancellationToken ct)
        => (await _mediator.Send(new GetReviewEligibilityQuery(_currentUser.UserId!.Value, productId), ct)).ToActionResult();

    [HttpGet("reviews/mine")]
    [Authorize]
    public async Task<IActionResult> GetMine([FromQuery] GetMyReviewsRequest req, CancellationToken ct)
    {
        var query = new GetMyReviewsQuery(_currentUser.UserId!.Value, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpPost("products/{productId:guid}/reviews")]
    [Authorize]
    public async Task<IActionResult> Create(Guid productId, [FromBody] CreateReviewBody body, CancellationToken ct)
    {
        var cmd = new CreateReviewCommand(_currentUser.UserId!.Value, productId, body.Rating, body.Title, body.Content);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Review created");
    }

    [HttpPut("reviews/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewBody body, CancellationToken ct)
    {
        var cmd = new UpdateReviewCommand(id, _currentUser.UserId!.Value, body.Rating, body.Title, body.Content);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Review updated");
    }

    [HttpDelete("reviews/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _mediator.Send(new DeleteReviewCommand(id, _currentUser.UserId!.Value), ct)).ToActionResult("Review deleted");

    [HttpGet("admin/reviews")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> AdminSearch([FromQuery] AdminSearchReviewsRequest req, CancellationToken ct)
    {
        var query = new AdminSearchReviewsQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Status, req.Rating, req.Keyword);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpPost("admin/reviews/{id:guid}/hide")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    [AdminAudit("Review.Hide", "Review")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] HideReviewBody? body, CancellationToken ct)
        => (await _mediator.Send(new HideReviewCommand(id, body?.Reason), ct)).ToActionResult("Review hidden");

    [HttpPost("admin/reviews/{id:guid}/show")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    [AdminAudit("Review.Show", "Review")]
    public async Task<IActionResult> Show(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ShowReviewCommand(id), ct)).ToActionResult("Review shown");

    [HttpPost("admin/reviews/{id:guid}/reply")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    [AdminAudit("Review.Reply", "Review")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplyToReviewBody body, CancellationToken ct)
        => (await _mediator.Send(new ReplyToReviewCommand(id, body.Content), ct)).ToActionResult("Reply added");

    [HttpDelete("admin/reviews/{id:guid}/reply")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    [AdminAudit("Review.RemoveReply", "Review")]
    public async Task<IActionResult> RemoveReply(Guid id, CancellationToken ct)
        => (await _mediator.Send(new RemoveReviewReplyCommand(id), ct)).ToActionResult("Reply removed");

    [HttpGet("products/{productId:guid}/questions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQuestions(Guid productId, [FromQuery] GetProductQuestionsRequest req, CancellationToken ct)
    {
        var query = new GetProductQuestionsQuery(productId, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpPost("products/{productId:guid}/questions")]
    [Authorize]
    public async Task<IActionResult> AskQuestion(Guid productId, [FromBody] AskProductQuestionBody body, CancellationToken ct)
    {
        var cmd = new AskProductQuestionCommand(_currentUser.UserId!.Value, productId, body.Content);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Question submitted");
    }

    [HttpPost("admin/questions/{id:guid}/answer")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    [AdminAudit("ProductQuestion.Answer", "ProductQuestion")]
    public async Task<IActionResult> AnswerQuestion(Guid id, [FromBody] AnswerProductQuestionBody body, CancellationToken ct)
    {
        var cmd = new AnswerProductQuestionCommand(id, _currentUser.UserId!.Value, body.Content);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Question answered");
    }

    [HttpDelete("questions/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteQuestion(Guid id, CancellationToken ct)
        => (await _mediator.Send(new DeleteProductQuestionCommand(id, _currentUser.UserId!.Value, IsStaffOrAdmin), ct)).ToActionResult("Question deleted");
}

public record GetProductReviewsRequest(int Page = 1, int PageSize = 20, int? Rating = null);
public record CreateReviewBody(int Rating, string? Title, string Content);
public record UpdateReviewBody(int Rating, string? Title, string Content);
public record HideReviewBody(string? Reason);
public record AdminSearchReviewsRequest(int Page = 1, int PageSize = 20, string? Status = null, int? Rating = null, string? Keyword = null);
public record GetMyReviewsRequest(int Page = 1, int PageSize = 20);
public record ReplyToReviewBody(string Content);
public record GetProductQuestionsRequest(int Page = 1, int PageSize = 20);
public record AskProductQuestionBody(string Content);
public record AnswerProductQuestionBody(string Content);
