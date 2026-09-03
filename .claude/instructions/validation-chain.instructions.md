---
applyTo: "apps/backend/**/Dtos/**,apps/backend/**/Validators/**,apps/frontend/**/*.tsx,apps/frontend/**/*.ts,apps/mobile/**/*.tsx"
description: "End-to-end validation chain — EF Core entity drives FluentValidation DTO validator, which drives Zod client, which drives UI error behaviour"
---

# End-to-End Validation Chain

The EF Core entity (in `common/DotNetMonoRepoTemplate.Database/Entities/`) is the **single source of truth**. Every constraint defined there must propagate to the backend's FluentValidation validator, the frontend's Zod client schema, and the UI error response — in that order, with nothing invented or omitted at any layer. This replaces the Node era's Prisma → AJV → Zod chain — same shape, different backend technology.

## The chain

```
EF Core entity
    ↓  (required/nullable, HasMaxLength, format implied by field name)
FluentValidation validator (backend DTO)
    ↓  on validation failure
400 Bad Request  { isSuccessful: false, message: "Validation failed", errors: [{ field, message }] }
    ↓
frontend useMutation onError
    ↓
toast.error(message)

Meanwhile, client-side:
Zod schema (mirrors the FluentValidation rules)
    ↓  on validation failure
react-hook-form field error
    ↓
inline error text below the field  ← NO toast here
```

## Error behaviour rules — unchanged

| Source | UI behaviour |
|---|---|
| Zod client validation failure | Inline error below the field. No toast. |
| Server 400 (FluentValidation / business rule) | Toast with the server message. No inline error. |
| Server 409 (unique constraint) | Toast with a specific conflict message. |
| Server 500 | Toast: "Something went wrong. Please try again." |
| Network error | Toast: "Network error. Please check your connection." |

## EF Core → FluentValidation → Zod mapping table

| EF Core entity property | FluentValidation (backend) | Zod (frontend) |
|---|---|---|
| non-nullable `string` | `.NotEmpty()` | `z.string().min(1, 'Required')` |
| nullable `string?` | no `.NotEmpty()` rule | `z.string().optional()` |
| `.HasMaxLength(N)` | `.MaximumLength(N)` | `.max(N, 'Too long')` |
| email field name | `.EmailAddress()` | `z.string().email('Invalid email address')` |
| phone field name | `.Matches(@"^(\+27\|0)[6-8][0-9]{8}$")` | `z.string().regex(/^(\+27\|0)[6-8][0-9]{8}$/, 'Invalid phone number')` |
| `DateTime` (ISO on the wire, dd/MM/yyyy on screen) | model binding already enforces ISO shape; add `.Must(...)` only for extra constraints (e.g. future-only) | `z.string().datetime()`, or `z.string().regex(/^\d{2}\/\d{2}\/\d{4}$/, 'Use dd/mm/yyyy')` for a free-text dd/mm/yyyy field — see `date-handling.instructions.md` |
| `decimal` | `.GreaterThanOrEqualTo(0)` (or the correct bound) | `z.number().min(0, 'Must be 0 or greater')` |
| string-constant class (ported TS union — e.g. `RoleName`) | `.Must(v => RoleName.All.Contains(v))` | `z.enum(['A', 'B'])` |
| non-nullable `bool` | usually no rule needed (model binding requires it) | `z.boolean()` |
| `int` | `.GreaterThanOrEqualTo(0)` where applicable | `z.number().int().min(0)` |
| foreign-key `string` (GUID-as-string) | `.NotEmpty()` | `z.string().uuid('Invalid selection')` — note: EF Core GUIDs in this codebase are `string`, not native `Guid`, so the backend rule is just `.NotEmpty()`, not a UUID-format check |
| URL field | `.Must(v => Uri.TryCreate(v, UriKind.Absolute, out _))` | `z.string().url('Invalid URL')` |
| `.IsUnique()` in `OnModelCreating` | No FluentValidation rule (service layer checks) | No Zod rule | → returns 409 Conflict |

## Backend — FluentValidation with a shared 400 formatter

Every endpoint that accepts a body validates explicitly, then calls a shared extension method to format the 400:

```csharp
group.MapPost("/", async (CreateUserDto body, IValidator<CreateUserDto> validator, UserService userService, HttpContext context) =>
{
    var validation = await validator.ValidateAsync(body);
    if (!validation.IsValid)
    {
        return validation.ToBadRequest();
    }
    var result = await userService.CreateAsync(body, context.GetCurrentUser()?.Id ?? "SYSTEM");
    return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status201Created : StatusCodes.Status400BadRequest);
});
```

`ValidationResultExtensions.ToBadRequest()` (in `Validators/ValidationResultExtensions.cs`, one copy per service — see `api-builder.md`) is the shared formatter:

```csharp
public static class ValidationResultExtensions
{
    public static IResult ToBadRequest(this ValidationResult result) =>
        Results.Json(
            new
            {
                isSuccessful = false,
                message = "Validation failed",
                errors = result.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
            },
            statusCode: StatusCodes.Status400BadRequest);
}
```

This replaces the Node era's AJV `setSchemaErrorFormatter` + `buildValidationMessage` switch statement — FluentValidation's `ValidationResult.Errors` already carries a human-readable `ErrorMessage` per failed rule (customizable via `.WithMessage(...)` on any rule), so there's no separate keyword-to-message mapping function needed the way AJV's raw `ErrorObject`s required one.

Unique constraint violations return 409 from the service layer, not the validator:

```csharp
public async Task<UserResponseDto> CreateAsync(CreateUserDto dto, string userId, CancellationToken cancellationToken = default)
{
    var existing = await _db.Users.AnyAsync(u => u.Email == dto.Email, cancellationToken);
    if (existing)
    {
        return new UserResponseDto { IsSuccessful = false, Message = "An account with this email already exists" };
    }
    // ...
}
```

The endpoint maps this to `StatusCodes.Status409Conflict` based on the message/a dedicated flag — see any current `Endpoints/*.cs` file for the exact pattern (some services use a `IsConflict` bool on the response, others infer from the message; check the target service's existing convention before inventing a third approach).

## Worked example — User entity

### 1. EF Core entity

```csharp
public sealed class User : AuditableEntity
{
    public required string Email { get; set; }
    public required string Username { get; set; }
    public string? Phone { get; set; }
    public required string RoleId { get; set; }
}
```

```csharp
modelBuilder.Entity<User>(entity =>
{
    entity.HasIndex(u => u.Email).IsUnique();
    entity.Property(u => u.Email).HasMaxLength(255);
    entity.Property(u => u.Username).HasMaxLength(100);
    entity.Property(u => u.Phone).HasMaxLength(20);
});
```

### 2. DTO + FluentValidation validator (backend)

```csharp
public sealed record CreateUserDto
{
    public required string Email { get; init; }
    public required string Username { get; init; }
    public string? Phone { get; init; }
    public required string RoleId { get; init; }
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).Matches(@"^(\+27|0)[6-8][0-9]{8}$").MaximumLength(20).When(x => x.Phone is not null);
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
```

### 3. Zod schema (frontend — mirrors the FluentValidation rules exactly)

```typescript
export const createUserSchema = z.object({
  email: z.string().min(1, 'Required').email('Invalid email address').max(255, 'Too long'),
  username: z.string().min(1, 'Username is required').max(100, 'Too long'),
  phone: z.string().regex(/^(\+27|0)[6-8][0-9]{8}$/, 'Invalid phone number').optional().or(z.literal('')),
  roleId: z.string().min(1, 'Please select a role'),
});

export type CreateUserFormData = z.infer<typeof createUserSchema>;
```

### 4. React form (admin-web) — unchanged from the Node era

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';

export function CreateUserForm() {
  const queryClient = useQueryClient();

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<CreateUserFormData>({
    resolver: zodResolver(createUserSchema),
  });

  const mutation = useMutation({
    mutationFn: (data: CreateUserFormData) => userService.create(data),
    onSuccess: () => {
      toast.success('User created successfully');
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      const status = error.response?.status;
      const message = error.response?.data?.message;
      if (status === 409) {
        toast.error('An account with this email already exists');
      } else if (status === 400) {
        toast.error(message ?? 'Please check your input and try again');
      } else {
        toast.error('Something went wrong. Please try again.');
      }
    },
  });

  return (
    <form onSubmit={handleSubmit((data) => mutation.mutate(data))}>
      <div>
        <input type="email" {...register('email')} placeholder="Email address" />
        {errors.email && (
          <span className="text-destructive text-sm mt-1 block">{errors.email.message}</span>
        )}
      </div>

      <div>
        <input {...register('username')} placeholder="Username" />
        {errors.username && (
          <span className="text-destructive text-sm mt-1 block">{errors.username.message}</span>
        )}
      </div>

      <div>
        <input {...register('phone')} placeholder="Phone (optional)" />
        {errors.phone && (
          <span className="text-destructive text-sm mt-1 block">{errors.phone.message}</span>
        )}
      </div>

      <button type="submit" disabled={isSubmitting || mutation.isPending}>
        {mutation.isPending ? 'Creating...' : 'Create User'}
      </button>
    </form>
  );
}
```

## Email — reject disposable/throwaway domains (service layer, after FluentValidation)

`.EmailAddress()` (FluentValidation) and `.email()` (Zod) only validate syntax — they accept `anything@yopmail.com` just as happily as a real address. Domain-reputation is a business rule, not a shape constraint, so it's checked the same way a uniqueness constraint is: in the service layer, after FluentValidation passes, before the write.

```csharp
if (DisposableEmailDomains.IsDisposableEmailDomain(dto.Email))
{
    return new UserResponseDto { IsSuccessful = false, Message = "Please use a permanent email address" };
}
```

`DisposableEmailDomains.IsDisposableEmailDomain` checks against `DisposableEmailDomains.Domains` in `common/DotNetMonoRepoTemplate.Types/DisposableEmailDomains.cs` — extend that `IReadOnlySet<string>` as new throwaway providers show up. This returns `400`, the same status as any other DTO-shape failure, with a field-less message (surfaces as a toast per the error-behaviour rules above, not an inline field error, since it's a server-side business rule rather than a client-side shape check).

Note: the seed list includes `proton.me`/`protonmail.com` alongside actual disposable-inbox services (`yopmail.*`, `mailinator.com`, `guerrillamail.*`, etc.) per explicit product decision, carried over unchanged from the Node era — ProtonMail is a real, permanent mailbox provider, not a throwaway service, so this blocks legitimate privacy-conscious signups along with the intended throwaway ones. Revisit this specific entry if that tradeoff turns out to cost more signups than it prevents abuse.

## Rules — always enforced

- Zod schema is derived FROM the FluentValidation rules. Never write one without updating the other.
- FluentValidation's `.NotEmpty()` already rejects both null and empty string on a required field — no separate "empty string slips through" gap to guard against the way AJV's `required[]`-without-`minLength` had.
- Client-side Zod failure → inline error, no toast.
- Server 400/409/500 → toast, no inline error.
- The `errors` array in the 400 response always includes the `field` name (`error.PropertyName`) so future field-mapping is possible.
- Every form submit button shows a loading/pending state during mutation.
- Unique constraint violations (`.IsUnique()` in `OnModelCreating`) always return 409, not 400.
- Monetary `decimal` fields use `z.number().min(0)` client-side; send as a JSON number; the backend DTO/service reads it as `decimal` — no manual `Convert.ToDecimal` string-parsing needed since `System.Text.Json` handles the numeric binding.
