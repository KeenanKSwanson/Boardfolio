# Downloading and caching the switch catalog

Both catalog endpoints are public and require no login. Catalog data can be cached across account changes; it does not include a user's private custom switch recipes.

## Paged catalog

```http
GET /api/switches?page=1&pageSize=200
```

The response is an object with these fields:

| Field | Meaning |
| --- | --- |
| `switches` | This page's switch records (`id`, `name`, `manufacturer`, `type`) |
| `page` | Requested page, starting at 1 |
| `pageSize` | Requested page size, default 200 and maximum 200 |
| `totalCount` | Number of records matching the search across all pages |
| `totalPages` | Number of matching pages; 0 when no records match |
| `catalogVersion` | Opaque 64-character lowercase SHA-256 fingerprint of the complete public catalog |

**Response compatibility:** this replaces the old raw array returned by `/api/switches`. Clients must now decode the `switches` property and accompanying metadata.

`search` remains optional and matches switch name or manufacturer. For example, `/api/switches?search=Cherry&page=1&pageSize=50`. For a complete offline catalog, omit `search`. Keep search and page size unchanged throughout a download. Ordering is manufacturer, name, then unique ID to resolve duplicate names deterministically. Pages beyond the end return an empty `switches` array and retain the total counts/version. Invalid pagination or malformed version parameters return `400` Problem Details.

## Version check

```http
GET /api/switches/version
```

Returns `{ "catalogVersion": "<64-character fingerprint>" }` without downloading switch records. Compare this string for equality with the locally stored version; it is not an incrementing number or a timestamp. The same version is used regardless of search, page, and page size. Changes to any switch's ID, name, manufacturer, or type, including additions and deletions, change the fingerprint. Restoring exactly the previous catalog restores its fingerprint.

The current implementation calculates the fingerprint by streaming the public catalog fields from the database on each request. It avoids loading the full catalog into application memory but still scans the catalog. It uses a serializable read transaction to keep the version, count, and page internally consistent. If the catalog becomes very large or frequently updated, a transactionally maintained revision would reduce this work and locking.

## Mobile cache flow

1. Display the existing local catalog immediately, if present.
2. Request `/api/switches/version`. If it equals the cached version, keep the cache.
3. Otherwise, request page 1 with no search and capture `catalogVersion` and `totalPages`.
4. Request remaining pages with the same page size and that version:

   ```http
   GET /api/switches?page=2&pageSize=200&catalogVersion=<version-from-page-1>
   ```

5. If the catalog changed, the API returns `409` Problem Details. Discard the incomplete staged download and restart at page 1. Keep the old complete cache while retrying.
6. After all pages succeed, replace the local catalog and its version together in one local database transaction. An empty catalog (`totalPages: 0`) is a successful download and should replace a stale populated cache too.

Each request reads the current catalog; the server does not retain historical versions between requests. Supplying `catalogVersion` detects changes instead of serving an older snapshot. Without it, requests return the latest page and the client must compare versions itself. On network failures, preserve the old complete cache rather than replacing it with partial data.

These endpoints only read data. They do not register accounts or add switches, and this change requires no database migrations.
