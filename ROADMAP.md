# Boardfolio project roadmap

Last updated: October 24, 2026

## Product direction

Boardfolio is a free place to track keyboard builds without signing up, share builds and join discussions, and eventually buy and sell keyboards. A keyboard can retain its build and ownership history as it changes hands, while each owner's financial records remain private.

This document records the agreed product direction and proposed delivery stages. The implementation status below distinguishes completed backend work from remaining product work.

## Access and accounts

| Activity | Account required? |
| --- | --- |
| Track personal builds privately | No |
| Browse public builds | No |
| Read discussions | No |
| Browse keyboards listed for sale | No |
| Publish a build or start a discussion | Yes |
| Comment or participate in discussions | Yes |
| List a keyboard for sale | Yes |
| Purchase a keyboard | Yes initially; guest checkout remains a future option |

The planned frontend is an iOS/Android app. Account-free tracking should save builds locally on the device, explain where the data lives, and support export/import backups. Users should be able to bring their existing local builds into an account later. Deleting app data or changing devices must not be presented as safe without a backup or account synchronization.

Anonymous builds stay on the user's device; do not store them on the server as unowned records. Offer a clear "Create an account to back up your builds" action. Account creation should import existing local builds and their private cost records into storage linked to that user, preserving local data until the upload succeeds and avoiding duplicates on retries. Signed-in users can restore their builds on another device. Account-backed backup belongs to the personal tracking stage; community publishing can follow later.

## Build records and media

- Track multiple keyboard builds, their parts, configurations, notes, and modifications.
- Let people keep builds private or publish them to showcase their work.
- Support photos, video, and sound tests. Media is optional for ordinary build tracking and required for sale listings.
- Define the exact sale-media requirement before marketplace launch, including whether video and sound tests are alternatives or both required.
- Give each keyboard a lasting record so public sharing and sale listings can refer to the same board over time.

## Private owner cost records

- Let an owner record what they paid to purchase a keyboard or what they spent building it.
- Support individual component and modification costs, plus an overall purchase or build amount, so an owner can understand their total spending on each keyboard and across their collection.
- Make the total calculation clear and avoid counting an overall build amount and its component breakdown twice. Record currency and do not silently add different currencies together.
- Keep these amounts and related financial notes private to that owner. They must not appear in public builds, discussions, sale listings, or public board history.
- Store cost records separately from the transferable keyboard record. Selling or transferring a keyboard must never give the next owner access to a previous owner's costs.
- Preserve the previous owner's private cost history after a sale, with sold boards distinguishable from the current collection.
- Give the new owner their own private cost record, starting with what they paid and any later spending. They do not inherit the seller's build costs or financial notes.
- Support private cost tracking for local, account-free builds as well as account-backed builds. Public sharing and transfer must exclude that private data.

## Community

- Provide public build pages and discovery so people can show and explore keyboards.
- Allow everyone to read discussions without an account.
- Require an account to publish, post, or comment.

## Marketplace and ownership history

- Let account holders list builds for sale, with required media checked before publication.
- Keep sale listings browsable without an account.
- Require an account to purchase initially; evaluate guest checkout later, including how a guest would claim ownership of the board.
- Plan payment, fulfillment, completion confirmation, and dispute handling before enabling sales.
- Transfer ownership after a confirmed completed sale, not merely when a listing is created or checkout begins.
- Preserve a snapshot of the keyboard's configuration and sale details at the time of purchase.
- Let subsequent owners modify the board without rewriting earlier configurations or sale snapshots.
- Provide a Zillow-like board timeline of configurations, modifications, and sales, subject to privacy decisions.
- Keep buyer identities, addresses, payment information, and owner cost records out of public history. Decide separately whether completed sale prices are public; a sale price is distinct from an owner's private cost basis.

## Proposed delivery stages

### 1. Personal build tracking

- Free tracking without signup.
- Parts, configurations, notes, and optional media.
- Private purchase/build costs and spending totals.
- Local persistence with clear storage messaging and export/import backups.
- Optional account creation to back up builds and private costs to the server and restore them on other devices.
- Define build, component, and private owner-cost models first, then implement storage, create/edit/delete behavior, and the tracking interface. Stage one delivers usable tracking, not only model classes or a database schema.

### 2. Community and accounts

- Extend the accounts introduced for backup with community profiles and publishing.
- Public build pages, discovery, discussions, and comments.
- Account-free reading and browsing; accounts required for publishing and participation.
- Enforce privacy for owner financial records in every public view.

### 3. Marketplace and board history

- Sale listings and required media validation.
- Payments, fulfillment, completion confirmation, and disputes.
- Ownership transfer with sale-time configuration snapshots.
- Board modification and sale history across owners.
- Separate private financial records for each owner, retained after transfer.
- Evaluate guest checkout and public sale-price history.

## Decisions still open

- Exact photo/video/sound-test requirements for sale listings.
- Whether and when to offer guest checkout.
- Whether completed sale prices appear publicly in a board's history.
- Detailed rules for confirming completion and handling cancellations, returns, and ownership reversals.

## Phase 1 implementation status — October 24, 2026

Implemented and covered by backend integration tests:

- Persistent local-development SQLite storage with versioned EF migrations.
- Account registration, bearer-token login/refresh, and owner-scoped private endpoints.
- Build and component CRUD, starter switch catalog, and private custom switch recipes.
- Separate private cost ledgers, currency-grouped totals, and retained spending history after deletion.
- Optional HTTPS media-link metadata (not file uploads).
- Atomic, additive account import and versioned private export; retrying an import skips existing records.

Still required before phase 1 is complete:

- iOS/Android client framework selection and implementation, including screens and device-local persistence.
- Native backup/export/import, secure login storage, account upload/restore UX, and real-device testing.
- Native media selection and private media file storage/uploads.
- Production email delivery, hosting/database selection, durable authentication keys, and operational backups.

Azure is a possible future host, not a configured deployment. The local SQLite provider is not an Azure SQL configuration. Sale listing amounts will retain the seller's currency; buyer-facing converted estimates remain marketplace work, separate from private owner costs.
