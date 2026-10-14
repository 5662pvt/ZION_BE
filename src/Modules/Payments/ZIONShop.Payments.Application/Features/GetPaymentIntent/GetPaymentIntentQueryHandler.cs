using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Mappings;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.GetPaymentIntent;

public class GetPaymentIntentQueryHandler : IRequestHandler<GetPaymentIntentQuery, Result<PaymentIntentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;

    public GetPaymentIntentQueryHandler(IPaymentRepository payments, IPaymentGateway gateway)
    {
        _payments = payments;
        _gateway = gateway;
    }

    public async Task<Result<PaymentIntentDto>> Handle(GetPaymentIntentQuery request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIntentIdAsync(request.IntentId, cancellationToken);
        if (payment is null || payment.UserId != request.UserId)
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.PaymentNotFound);

        return Result.Success(payment.ToDto(_gateway));
    }
}
