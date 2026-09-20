# GamePlatform.Features.Contracts

CL-011 adds captured-owner immutable snapshots with explicit missing, available,
stale, pending, unavailable and error states. Feature-owned canonical query keys
include the complete private owner/view generation and bounded result-shaping
identity; they carry no transport or SQL values.

Owns application/UI-facing feature capability contracts and immutable read-model seams. Depends only on Core; excludes remote URLs, SQL and implementations. M0 exposes status/unavailability only. Gates: A05–A08.
