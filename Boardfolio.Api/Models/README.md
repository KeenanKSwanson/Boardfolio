# Domain models and phase 1 storage

- `KeyboardBuild` is an account-owned keyboard with current components and optional media links. Responses omit its owner ID and never include private costs.
- `KeyboardBuildComponent` describes a part and quantity. It may reference either a catalog `KeyboardSwitch` or an account-owned `CustomSwitch`, or use a manual description. It contains no prices.
- `CustomSwitch` describes a private recipe with top housing, bottom housing, stem, and spring. Its switch type is independent of being custom.
- `PrivateOwnerCosts` is a separate owner ledger. Itemized mode sums all entries; overall mode adds the initial amount to later spending only. Totals stay grouped by currency. Entries are total costs, not unit prices.
- `BuildMedia` holds HTTPS link metadata only. Uploaded files and native device-local media are not implemented.
- `ApplicationUser` is the ASP.NET Core Identity account.

Controllers use explicit write contracts. Services validate request data, ownership, switch selection, expense relationships, and import batches. The data accessor performs EF queries; `BoardfolioDbContext` configures storage and migrations. Server records require an authenticated owner. Device-local records are uploaded through the account import endpoint, which assigns ownership from the token.

Deleting a component or build does not delete private spending history. Deleted-build costs remain accessible to their original owner. Future ownership transfers must preserve that boundary and must not transfer custom recipes or private costs implicitly. Transfer/history features are not yet implemented.

See the root README for API usage, backup semantics, and remaining mobile/hosting work.
