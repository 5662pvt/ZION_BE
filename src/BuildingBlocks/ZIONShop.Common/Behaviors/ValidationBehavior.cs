using FluentValidation;
using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var ctx = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(ctx, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errors = failures
            .Select(f => Error.Validation($"{f.PropertyName}.{f.ErrorCode}", f.ErrorMessage))
            .ToList();

        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(errors);
        }
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = responseType.GetGenericArguments()[0];
            var method = typeof(Result<>).MakeGenericType(inner)
                .GetMethod(nameof(Result<int>.Failure), new[] { typeof(IReadOnlyList<Error>) })!;
            return (TResponse)method.Invoke(null, new object[] { errors })!;
        }

        throw new ValidationException(failures);
    }
}
