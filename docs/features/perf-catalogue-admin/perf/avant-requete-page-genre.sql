-- Requête EF de la page de recherche avec genre, capturée le 14 novembre 2026 avant correctif.
(@genre nvarchar(100),@p int,@p4 int)SELECT [b3].[Isbn13], [b3].[Authors], [b3].[BibliographicDetailsRefreshedAt], [b3].[CoverCheckedAt], [b3].[CoverSource], [b3].[CoverUrl], [b3].[Edition], [b3].[FirstSeenAt], [b3].[Genre], [b3].[GenresRefreshedAt], [b3].[IsAbsentFromIsbndb], [b3].[IsFavoriteReference], [b3].[IsHiddenFromCatalog], [b3].[IsIsbndbSourced], [b3].[IsbndbCheckedAt], [b3].[IsbndbCompletedAt], [b3].[IsbndbCompletedBy], [b3].[Language], [b3].[LastAttemptAt], [b3].[LastAvailableAt], [b3].[ManuallyEditedFields], [b3].[MetadataFailureReason], [b3].[MetadataFetchedAt], [b3].[MetadataSource], [b3].[MetadataStatus], [b3].[Pages], [b3].[PhysicalFormat], [b3].[PublicationYear], [b3].[Publisher], [b3].[QuantityAvailable], [b3].[RawPayload], [b3].[RedirectedToIsbn13], [b3].[RejectionCount], [b3].[ResolveAttempts], [b3].[RowVersion], [b3].[SalesCount], [b3].[Summary], [b3].[Title], [b3].[UpdatedAt], [b3].[WorkId], [b3].[WorkKey], [b2].[BookIsbn13], [b2].[Name]
FROM (
    SELECT [b].[Isbn13], [b].[Authors], [b].[BibliographicDetailsRefreshedAt], [b].[CoverCheckedAt], [b].[CoverSource], [b].[CoverUrl], [b].[Edition], [b].[FirstSeenAt], [b].[Genre], [b].[GenresRefreshedAt], [b].[IsAbsentFromIsbndb], [b].[IsFavoriteReference], [b].[IsHiddenFromCatalog], [b].[IsIsbndbSourced], [b].[IsbndbCheckedAt], [b].[IsbndbCompletedAt], [b].[IsbndbCompletedBy], [b].[Language], [b].[LastAttemptAt], [b].[LastAvailableAt], [b].[ManuallyEditedFields], [b].[MetadataFailureReason], [b].[MetadataFetchedAt], [b].[MetadataSource], [b].[MetadataStatus], [b].[Pages], [b].[PhysicalFormat], [b].[PublicationYear], [b].[Publisher], [b].[QuantityAvailable], [b].[RawPayload], [b].[RedirectedToIsbn13], [b].[RejectionCount], [b].[ResolveAttempts], [b].[RowVersion], [b].[SalesCount], [b].[Summary], [b].[Title], [b].[UpdatedAt], [b].[WorkId], [b].[WorkKey]
    FROM [Books] AS [b]
    WHERE [b].[IsHiddenFromCatalog] = CAST(0 AS bit) AND [b].[IsFavoriteReference] = CAST(0 AS bit) AND [b].[RedirectedToIsbn13] IS NULL AND [b].[MetadataStatus] IN (CAST(1 AS tinyint), CAST(3 AS tinyint)) AND [b].[Title] IS NOT NULL AND LTRIM(RTRIM([b].[Title])) <> N'' AND NOT EXISTS (
        SELECT 1
        FROM [BookMovements] AS [b0]
        WHERE [b0].[Isbn13] = [b].[Isbn13] AND [b0].[ScanSessionId] IS NOT NULL AND EXISTS (
            SELECT 1
            FROM [ScanSessions] AS [s]
            WHERE [s].[Id] = [b0].[ScanSessionId] AND [s].[Status] = CAST(0 AS tinyint))) AND EXISTS (
        SELECT 1
        FROM [BookGenres] AS [b1]
        WHERE [b].[Isbn13] = [b1].[BookIsbn13] AND [b1].[Name] = @genre) AND [b].[QuantityAvailable] > 0
    ORDER BY [b].[FirstSeenAt] DESC, [b].[Title], [b].[Isbn13]
    OFFSET @p ROWS FETCH NEXT @p4 ROWS ONLY
) AS [b3]
LEFT JOIN [BookGenres] AS [b2] ON [b3].[Isbn13] = [b2].[BookIsbn13]
ORDER BY [b3].[FirstSeenAt] DESC, [b3].[Title], [b3].[Isbn13], [b2].[BookIsbn13]

