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

## Related docs

- Backend rules: [06-backend-rules.md](06-backend-rules.md)
- Frontend client: [04-frontend-structure.md](04-frontend-structure.md)
