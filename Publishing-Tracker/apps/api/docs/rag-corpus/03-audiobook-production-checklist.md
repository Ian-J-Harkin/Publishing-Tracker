# Audiobook Production Checklist

Audiobook editions undergo distinct contributor, runtime, and storefront identifier checks prior to distribution.

## Required Narrator and Runtime
Every `Audiobook` edition requires a non-whitespace `Narrator` credit and a `Duration` strictly greater than `TimeSpan.Zero`. Missing either field flags the edition in `find_titles_missing_metadata`.

## Audiobook Identifiers
Like digital Ebooks, an Audiobook satisfies identifier completeness (`ISBN_Or_PlatformProductId`) with either an `ISBN` or at least one non-empty `PlatformProductId` (for example an Audible ASIN) linked in platform metadata.
