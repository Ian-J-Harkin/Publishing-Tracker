# PublishingTracker Metadata Completeness Guide

Every title owned by an author is evaluated against format-aware completeness rules before catalogue reporting.

## Book-Level Required Fields
All books require non-empty `Description`, `Genre`, `CoverImageUrl`, and at least one attached `Edition`. Whitespace-only strings count as missing.

## Conditional Series Numbering
`SeriesNumber` must be a positive integer whenever `SeriesName` is non-empty. Standalone books with an empty `SeriesName` do not require a `SeriesNumber`.

## Universal Edition Pricing and Dates
Every edition across print, ebook, and audio formats requires both `PublicationDate` and `BasePrice`. A `BasePrice` of `0.00` is valid for free promotional editions; only `null` prices are flagged as missing.
