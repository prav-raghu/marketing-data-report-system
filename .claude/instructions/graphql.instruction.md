# GraphQL Layer Documentation

## Overview

`api-gateway` includes an **optional** GraphQL layer over the REST microservices, built on **HotChocolate** (not Apollo Server — this changed with the .NET migration). It's conditionally registered only when `GRAPHQL_ENABLED=true`; when disabled, `api-gateway` is pure YARP reverse-proxy with no GraphQL surface at all.

**Scope note, confirmed during the migration**: this layer only ever proxied to `customer-api`'s user endpoints — there was never real Apollo Federation across multiple services, in either the Node original or this port. A single schema, one resolver set (`Query`/`Mutation`), one backend (`customer-api`, via `UserProxyClient`). If a task references "federation," that's aspirational, not something this layer has ever done.

**Confidence note**: this is flagged as the lowest-confidence code from the whole .NET migration — the `[Service]`-attribute injection pattern for `IHttpContextAccessor`/`UserProxyClient` into `Query`/`Mutation` methods was written without the ability to compile-check it in the migration's sandbox (no .NET SDK available). Verify it actually builds before treating it as proven, if a task touches this layer.

## Architecture

```
Client → api-gateway (/graphql, HotChocolate) → customer-api (REST, /api/v1/users/*)
```

Nothing else is proxied through GraphQL — `admin-api`/`schedule-api` have no GraphQL surface.

### Key Components (all in `apps/backend/api-gateway/src/GraphQL/`)

1. **`Query.cs`** — `GetUser(id)`, `GetUsers()`, `GetCurrentUser()`
2. **`Mutation.cs`** — `CreateUser(input)`, `UpdateUser(id, input)`, `DeleteUser(id)`
3. **`UserModels.cs`** — `UserType`, `UserResponse`, `UsersResponse`, `CreateUserInput`, `UpdateUserInput` — all `sealed record`
4. **`UserProxyClient.cs`** — the `HttpClient`-based proxy to `customer-api`'s REST endpoints, token-forwarding, and GraphQL error translation

There is no separate schema-builder class or `.graphql` type-definition files — HotChocolate infers the schema from `Query`/`Mutation`'s C# method signatures and the `sealed record` types they return, registered directly in `Program.cs`.

## Configuration

### Environment Variables (`ApiGatewayOptions`)

```env
GRAPHQL_ENABLED=true
GRAPHQL_PATH=/graphql
GRAPHQL_INTROSPECTION=true
CUSTOMER_API_URL=http://localhost:4002
```

`GRAPHQL_PLAYGROUND` is **not implemented** — HotChocolate's built-in Banana Cake Pop IDE is available at the GraphQL path automatically when introspection is on; there's no separate playground toggle the way the Node era's config implied one.

### Disabling GraphQL

Set `GRAPHQL_ENABLED=false`. When disabled, `builder.Services.AddGraphQLServer()`/`AddScoped<UserProxyClient>()` never run and `app.MapGraphQL(...)` never registers — the endpoint doesn't exist (404), not just "returns an error."

## Registration (`Program.cs`) — the real code

```csharp
builder.Services.AddHttpClient<UserProxyClient>(client => client.BaseAddress = new Uri(gatewayOptions.CustomerApiUrl));

if (gatewayOptions.GraphQlEnabled)
{
    builder.Services
        .AddGraphQLServer()
        .AddQueryType<Query>()
        .AddMutationType<Mutation>()
        .AllowIntrospection(gatewayOptions.GraphQlIntrospection);
    builder.Services.AddScoped<UserProxyClient>();
}
```

And later, after building the app:

```csharp
if (gatewayOptions.GraphQlEnabled)
{
    app.MapGraphQL(gatewayOptions.GraphQlPath);
}
```

`AddHttpClient<UserProxyClient>` is registered unconditionally (harmless if unused when GraphQL is disabled) so `UserProxyClient`'s constructor-injected `HttpClient` always has its `BaseAddress` set correctly.

## Usage Examples

### GraphQL Endpoint

`http://localhost:4000/graphql`

### Example Queries

#### Get User by ID
```graphql
query GetUser {
  getUser(id: "user-123") {
    success
    data {
      id
      email
      firstName
      lastName
      role
      isActive
      createdAt
    }
    error
  }
}
```

Field names in the GraphQL schema are camelCase (`firstName`, `isActive`) even though the underlying C# properties are PascalCase (`FirstName`, `IsActive`) — HotChocolate's default naming convention handles this translation automatically, same end result as the Node era's schema, no extra configuration needed.

#### Get All Users
```graphql
query GetAllUsers {
  getUsers {
    success
    data {
      id
      email
      firstName
      lastName
    }
    error
  }
}
```

#### Get Current User (Authenticated)
```graphql
query GetCurrentUser {
  getCurrentUser {
    success
    data {
      id
      email
      firstName
      lastName
    }
    error
  }
}
```

### Example Mutations

#### Create User
```graphql
mutation CreateUser {
  createUser(input: {
    email: "john.doe@example.com"
    password: "SecurePass123!"
    firstName: "John"
    lastName: "Doe"
    role: "customer"
  }) {
    success
    data { id email firstName lastName }
    error
  }
}
```

#### Update User
```graphql
mutation UpdateUser {
  updateUser(id: "user-123", input: { firstName: "Jane", isActive: true }) {
    success
    data { id firstName lastName isActive }
    error
  }
}
```

#### Delete User
```graphql
mutation DeleteUser {
  deleteUser(id: "user-123") {
    success
    data { id }
    error
  }
}
```

## Authentication — token forwarding, not gateway-side verification

GraphQL requests support JWT authentication via Bearer tokens, but **`api-gateway` never verifies the token itself** — it extracts the raw bearer token from the incoming request (`Query.ExtractToken`, reading `IHttpContextAccessor.HttpContext.Request.Headers.Authorization`) and forwards it unchanged to `customer-api` on every proxied call. `customer-api`'s own `AuthGuardMiddleware` is what actually validates it. An invalid/expired/missing token doesn't fail inside the GraphQL layer — it fails when `customer-api` returns 401, which `UserProxyClient` surfaces as a `GraphQLException` (see "Error Handling" below).

```bash
curl -X POST http://localhost:4000/graphql \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "{ getCurrentUser { success data { email } } }"}'
```

## Frontend Integration — unchanged from the Node era

### Apollo Client (React)

```typescript
import { ApolloClient, InMemoryCache, createHttpLink } from '@apollo/client';
import { setContext } from '@apollo/client/link/context';

const httpLink = createHttpLink({ uri: 'http://localhost:4000/graphql' });

const authLink = setContext((_, { headers }) => {
  const token = getInMemoryAccessToken(); // held in memory, not localStorage — see jwt-security.md
  return { headers: { ...headers, authorization: token ? `Bearer ${token}` : '' } };
});

const client = new ApolloClient({ link: authLink.concat(httpLink), cache: new InMemoryCache() });
```

## Adding New Schemas (querying a new REST resource through GraphQL)

There's no separate "type definitions" step — the schema is inferred from C# types. To add a new domain (e.g. products):

### 1. Add the models (`GraphQL/ProductModels.cs`)

```csharp
public sealed record ProductType
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string? Description { get; init; }
}

public sealed record ProductResponse
{
    public required bool Success { get; init; }
    public ProductType? Data { get; init; }
    public string? Error { get; init; }
}
```

### 2. Add a proxy client (`GraphQL/ProductProxyClient.cs`)

Mirror `UserProxyClient` exactly — `HttpClient`-based, `BuildRequest` attaches the forwarded bearer token, catches exceptions and rethrows as `GraphQLException`.

### 3. Add resolver methods to `Query`/`Mutation`

```csharp
public Task<ProductResponse> GetProduct(
    string id,
    [Service] ProductProxyClient client,
    [Service] IHttpContextAccessor httpContextAccessor,
    CancellationToken cancellationToken) =>
    client.GetProductAsync(id, Query.ExtractToken(httpContextAccessor), cancellationToken);
```

### 4. Register in `Program.cs`

```csharp
builder.Services.AddHttpClient<ProductProxyClient>(client => client.BaseAddress = new Uri(gatewayOptions.CustomerApiUrl));
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()   // HotChocolate merges methods across partial classes / multiple AddQueryType calls — check current HotChocolate docs for the exact multi-type-extension syntax before assuming a specific pattern here, since this wasn't exercised during the migration
    .AddMutationType<Mutation>();
builder.Services.AddScoped<ProductProxyClient>();
```

## Error Handling

`UserProxyClient` catches any exception from the downstream HTTP call and rethrows as a `GraphQLException`:

```csharp
throw new GraphQLException(ErrorBuilder.New().SetMessage(ex.Message).SetCode("INTERNAL_SERVER_ERROR").Build());
```

Which HotChocolate formats in the response as:

```json
{
  "errors": [
    {
      "message": "...",
      "extensions": { "code": "INTERNAL_SERVER_ERROR" }
    }
  ]
}
```

A downstream 401/404/etc. from `customer-api` isn't specially distinguished from a network failure today — both surface as `INTERNAL_SERVER_ERROR`. If a task needs GraphQL to map REST status codes to distinct error codes, that's new work, not something already differentiated.

## Performance Considerations

1. **N+1 Queries**: No DataLoader/batching exists — each field resolver that hits `customer-api` does its own HTTP call. Not a concern yet since the only resolvers are single-entity/single-list lookups, but worth addressing before adding a resolver shape that could fan out (e.g. resolving a list of users' individual related-entity fields)
2. **Caching**: Not implemented — every query re-hits `customer-api`
3. **Query Complexity/Depth Limiting**: Not implemented — HotChocolate supports this natively (`AddMaxExecutionDepthRule`, cost analysis) but it isn't configured

## Security

1. **Authentication**: JWT tokens forwarded as-is to `customer-api` — see "Authentication" above
2. **Authorization**: Entirely delegated to `customer-api`'s `AuthGuardMiddleware`/RBAC — `api-gateway`'s GraphQL layer has no authorization logic of its own
3. **Rate Limiting**: Applied at the gateway level via the same `PartitionedRateLimiter` global tier as REST routes through the gateway (see `rules/backend.md`) — no GraphQL-specific tier
4. **Introspection**: Controlled by `GraphQlIntrospection`/`GRAPHQL_INTROSPECTION` — set `false` in production

## Testing

```bash
curl -X POST http://localhost:4000/graphql \
  -H "Content-Type: application/json" \
  -d '{"query": "{ getUsers { success data { email } } }"}'
```

### Using Banana Cake Pop (HotChocolate's built-in IDE)

Access at `http://localhost:4000/graphql` in a browser when `GRAPHQL_INTROSPECTION=true` — this replaces the Node era's separate GraphQL Playground; no separate enable flag needed beyond introspection.

## Best Practices

1. **`sealed record` models**: Never a mutable class for GraphQL response/input types
2. **Type Safety**: Never `dynamic`, never unjustified `object` (see `csharp-standards.md`)
3. **Error Handling**: Every proxy-client HTTP call wrapped in try/catch, rethrown as `GraphQLException`
4. **Token Forwarding**: Always extract and forward the bearer token via `Query.ExtractToken` — never let a resolver call the downstream service unauthenticated when the original request had a token
5. **Response Structure**: Keep the `{ success, data, error }` pattern for consistency with the existing `UserResponse`/`UsersResponse` shape — note this is `success` (lowercase, GraphQL-idiomatic), **not** `isSuccessful` the way REST responses are; don't try to unify the two, they're different layers with an already-established convention each

## Future Enhancements (not built)

- [ ] DataLoader for batch loading and caching
- [ ] Subscriptions support
- [ ] Query complexity analysis / depth limiting
- [ ] Field-level permissions distinct from `customer-api`'s RBAC
- [ ] Response caching
- [ ] Distinguishing downstream HTTP status codes in GraphQL error codes (see "Error Handling" above)

## Troubleshooting

### GraphQL not available
- Check `GRAPHQL_ENABLED=true` in `.env`
- Verify `api-gateway` is running
- Check logs for `AddGraphQLServer` registration issues

### 401 / auth errors surfacing as generic `INTERNAL_SERVER_ERROR`
- This is the current (undifferentiated) error-handling behavior — see "Error Handling" above, not a bug to "fix" without being asked
- Ensure the JWT token is valid and being forwarded (check `Query.ExtractToken`)

### Schema errors
- Verify `Query`/`Mutation` method signatures — HotChocolate infers everything from them, so a compile error there is a schema error
- Check that new response/input types are `sealed record`s HotChocolate can introspect

### Connection errors
- Verify `CUSTOMER_API_URL` in `.env`
- Ensure `customer-api` is running and reachable from `api-gateway`

## Migration from REST

GraphQL complements existing REST endpoints — both coexist, unchanged conceptually from the Node era:

```
REST:    GET /api/v1/users/{id}
GraphQL: query { getUser(id: "123") { ... } }
```

Clients choose which interface to use based on their needs. Since the GraphQL layer only proxies to `customer-api`'s user endpoints (see "Overview" above), any other domain is REST-only today.
