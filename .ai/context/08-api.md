# 08 — API Rules

## Response envelope (MUST)

```json
{
  "success": true,
  "message": "Success",
  "data": {},
  "errors": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 100,
    "totalPages": 5
  }
}
```

Validation error:

```json
{
  "success": false,
  "message": "Validation failed",
  "data": null,
  "errors": [
    { "field": "quantity", "message": "Quantity must be greater than 0" }
  ],
  "pagination": null
}
```

## Requirements (MUST)

- Versioning: `/api/v1/...`
- Swagger / OpenAPI
- Pagination, filtering, sorting on lists
- FluentValidation on commands
- HTTP codes: 200, 201, 400, 401, 403, 404, **409** (concurrency), 500

## Controller pattern (MUST)

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}
```

No business logic in controllers — map `Result<T>` only.

## Endpoints added in W15 (storefront additions)

- `GET /api/v1/products/suggest?q={text}&limit={n}` — anonymous, rate-limit policy `search`. `q` trimmed 2..100 chars (else 400 `Products.InvalidSuggestQuery`); `limit` 1..10, default 8. Published products only, case-insensitive match on name/sku/brand, name-starts-with-`q` ranked first. `data`: `[{ id, name, slug, sku, price, currency, thumbnailUrl }]`.
- `GET /api/v1/orders/mine?page&pageSize&status&from=yyyy-MM-dd&to=yyyy-MM-dd` — `from`/`to` are inclusive Vietnam-local (UTC+7) dates filtered on order `CreatedAt`. `from > to`, range > 366 days, or bad format → 400 `Orders.InvalidDateRange`. Response shape unchanged (paged `OrderSummary` list).
- `POST /api/v1/payments/intents` body now accepts `channel?: "Card" | "EWallet" | "BankTransfer"` (case-insensitive, default `Card`; other value → 400 `Payments.InvalidChannel`). `PaymentIntentDto` gains `channel`. Idempotent replay of an existing intent keeps its originally stored channel. Display-only — the simulated gateway flow (Succeeded/Failed) is unchanged.

## Related docs

- Backend rules: [06-backend-rules.md](06-backend-rules.md)
- Frontend client: [04-frontend-structure.md](04-frontend-structure.md)
