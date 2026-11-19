# Print and Ebook Format Specifications

Format-specific validation ensures downstream distributor feeds have valid physical dimensions or digital package fields.

## Print Editions (Paperback, Hardcover, LargeText)
All print editions strictly require an `ISBN`, a positive `PageCount` greater than zero, and non-empty physical `Dimensions`. A platform product ID alone cannot substitute for a print `ISBN`.

## Ebook Editions
Ebooks require a positive `FileSize` greater than zero, `PublicationDate`, and `BasePrice`. For identification, an Ebook satisfies completeness (`ISBN_Or_PlatformProductId`) if it has either an `ISBN` or at least one non-empty `PlatformProductId` such as a Kindle ASIN or Kobo ID.
