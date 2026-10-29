# Boardfolio API

Phase 1 backend for a future iOS/Android app. The API uses controllers, services, data accessors, and an Entity Framework database context. SQLite is the local development database; production configuration targets Azure SQL using the App Service managed identity.

## Run locally

Requires the .NET 10 SDK.

```powershell
dotnet tool restore
dotnet restore Boardfolio.Api/Boardfolio.Api.slnx
dotnet run --project Boardfolio.Api --launch-profile https
```

The development profile applies versioned migrations and creates `Boardfolio.Api/App_Data/boardfolio.db`. Open https://localhost:7226/docs for the API reference. Trust the local ASP.NET development certificate if necessary. Keep the database file out of source control.

```powershell
dotnet test Boardfolio.Api.Tests/Boardfolio.Api.Tests.csproj
```

Tests create isolated SQLite databases and exercise real account registration, token login, ownership checks, costs, and import behavior.

## Accounts and mobile authentication

1. `POST /api/auth/register` with `email` and `password` (at least 10 characters with upper/lowercase letters, a digit, and a symbol).
2. `POST /api/auth/login?useCookies=false` with the same fields.
3. Send `Authorization: Bearer <accessToken>` for private endpoints.
4. Refresh using `POST /api/auth/refresh` and the returned `refreshToken`.

These are ASP.NET Core Identity opaque tokens, not JWTs. The mobile app should use platform secure storage for refresh tokens and clear local authentication state on logout. Build ownership comes from authenticated claims, never a request-supplied user ID. Controllers require bearer authentication even though Identity also exposes cookie login.

Email confirmation and password-reset delivery require a real email sender before public release. Current development registration does not require email confirmation. Identity lockout and a basic per-address authentication rate limit are enabled; proxy/rate-limit settings must be configured for the eventual host.

## Tracking endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/switches?search=MX&page=1&pageSize=200` | Public paginated catalog with a catalog version |
| GET | `/api/switches/version` | Check the complete catalog version before refreshing a device cache |
| GET, POST | `/api/builds` | List the account's builds or create a build |
| GET, PUT, DELETE | `/api/builds/{id}` | Read, edit, or delete an owned build |
| POST | `/api/builds/{id}/components` | Add a component |
| PUT, DELETE | `/api/builds/{id}/components/{componentId}` | Edit or remove a component |
| GET, POST | `/api/custom-switches` | List or create the account's switch recipes |
| GET, PUT, DELETE | `/api/custom-switches/{id}` | Read, edit, or delete a recipe |
| GET, PUT | `/api/builds/{id}/costs` | Read or replace the private cost ledger |
| GET | `/api/me/costs` | All private ledgers, including deleted builds |
| GET | `/api/me/cost-totals` | Lifetime recorded spending, grouped by currency |
| POST | `/api/builds/{id}/media` | Attach an HTTPS photo/video/sound-test link |
| DELETE | `/api/builds/{id}/media/{mediaId}` | Remove a media link |
| GET | `/api/me/export` | Export versioned private backup data |
| POST | `/api/me/import` | Atomically import a batch of device-local records |

Build listing uses `page` (default 1) and `pageSize` (default 20, maximum 100). Build responses use `keyboardBuild` or `keyboardBuilds`. Enum values accept their names, such as `Switches`, `Linear`, `OverallAmount`, and `LaterSpending`.

Catalog pagination defaults to page 1 with 200 switches per page (maximum 200). Send the first page's `catalogVersion` when requesting later pages; a changed catalog returns `409` so the app can restart instead of caching mixed versions. The switches response is now an object rather than a raw array. See [mobile catalog caching](docs/switch-catalog.md) for the complete download/refresh flow.

### Response bodies and status codes

The generated reference at `/docs` declares each endpoint's response schema and status codes. It is enabled in Development and in the shipped Production configuration. Set `ApiDocumentation__Enabled=false` in Azure to disable it in Production; the OpenAPI schema at `/openapi/v1.json` follows the same setting. The Test Request button is enabled only in Development; production documentation is informational.

| Endpoints | Successful response |
| --- | --- |
| List builds | `200` with `KeyboardBuildListResponse` (`keyboardBuilds`, `page`, `pageSize`, `totalCount`, `totalPages`) |
| Read or update a build | `200` with `KeyboardBuildResponse` (`keyboardBuild`) |
| Create a build | `201` with `KeyboardBuildResponse` and a `Location` header |
| List catalog switches | `200` with `SwitchCatalogResponse` (`switches`, `page`, `pageSize`, `totalCount`, `totalPages`, `catalogVersion`) |
| Read catalog version | `200` with `SwitchCatalogVersionResponse` (`catalogVersion`) |
| List custom recipes | `200` with an array of recipes |
| Read or update a custom recipe | `200` with the recipe |
| Create a recipe, component, or media link | `201` with the created object and a `Location` header |
| Update a component | `200` with the component |
| Read or replace build costs | `200` with `OwnerCostsResponse` |
| List account costs or currency totals | `200` with an array of ledgers or currency totals |
| Export / import | `200` with the versioned backup / `ImportResponse` (`importedBuilds`, `skippedBuilds`, `importedCustomSwitches`) |
| Delete a build, component, recipe, or media link | `204` with no body |

Private endpoints return `401` when bearer authentication fails. Invalid input returns `400`; missing or inaccessible owned records return `404`. Deleting a recipe in use, changing a nonempty ledger's currency, or conflicting import IDs returns `409`. Errors use Problem Details; automatic request validation can also include an `errors` dictionary. Authentication endpoints also enforce a `429` rate limit.

A component may select `keyboardSwitchId` or `customSwitchId`, never both. A manually described switch can omit both. Custom recipes are private to their creator. A recipe in use cannot be deleted.

Media is currently link metadata, not uploaded files. The server does not fetch these URLs or guarantee their availability/privacy. Native file picking, local media storage, and authenticated cloud uploads remain mobile/storage integration work.

## Private cost rules

Costs are never included in build responses. `PUT` replaces the ledger's entries; it is not an append operation. Amounts are decimal totals, not per-unit prices.

- `Itemized`: initial-build entries + later-spending entries.
- `OverallAmount`: initial amount + later-spending entries. Initial-build entries can describe the breakdown without being counted twice.
- One currency per ledger. Supported codes come from the runtime's region currency data. Currency conversion is not implemented, and an existing nonempty ledger cannot be silently relabeled to another currency.
- Deleting components or builds preserves spending history. Deleted builds' ledgers remain available through `/api/me/costs` and export; they are included in lifetime totals.
- Sale listing prices and buyer-facing conversions belong to the marketplace phase and are separate from these private costs.

## Device-local backup and import

Anonymous records belong on the phone, not on this server. The future client should generate GUIDs and retain local records until the server confirms a successful import. Do not delete the device copy when registration alone succeeds.

`GET /api/me/export` returns the version 1 import shape:

```json
{
  "version": 1,
  "builds": [],
  "customSwitches": [],
  "archivedCosts": []
}
```

Each build includes its stable `id`, `name`, `keyboardModel`, `notes`, `components`, `media`, and optional `costs`. Each component and custom recipe has a stable `id`. Cost entry IDs and media IDs are regenerated on import; they are not synchronization identities. Archived costs include `keyboardBuildId` and preserve expenses for deleted builds. An exported expense for a removed component retains its description/amount but drops its obsolete component link.

Import is **additive**, not a two-way synchronization protocol. Records with IDs already owned by the signed-in account are skipped without overwriting server edits. Other ID conflicts reject the entire batch. All new records save in one transaction. Retrying a completed import does not duplicate builds or recipes. Imports are limited to 500 builds, 500 custom recipes, and 500 archived ledgers per request; large exports must be split into batches, importing recipes before dependent builds. Ordinary account edits use the CRUD endpoints.

## Azure and remaining phase 1 work

The separate migration build/apply workflow is documented in [database deployment](docs/database-deployment.md). It creates a release-specific migration bundle, requires an explicit target connection, and stops the rollout on migration failure. Run it once per database before deploying the API.

Follow [Azure setup](docs/azure-setup.md) for `boardfolio-api` and `boardfolio-sql-dev`. Production uses SQL Server with passwordless managed identity authentication; Development uses SQLite. Select the provider with `Database:Provider` (`Database__Provider`) and override the connection using `ConnectionStrings:Boardfolio` (`ConnectionStrings__Boardfolio`). Each provider has its own context and migration history. Production startup does not apply migrations; deploy schema separately before publishing the API. Preserve durable ASP.NET Data Protection keys for account tokens across restarts/instances.

Still needed for a complete phase 1 product:

- Choose and build the iOS/Android client, its tracking screens, and device-local storage.
- Implement local export/import files, account backup UX, secure token storage, and restore on another phone.
- Implement native media selection and private file upload/storage if links are insufficient.
- Configure email delivery, production database/hosting, secrets, HTTPS, durable token keys, and operational backups.
- Expand/maintain the switch catalog and test real-device workflows.

Authentication follows Microsoft's Identity API documentation: https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0
