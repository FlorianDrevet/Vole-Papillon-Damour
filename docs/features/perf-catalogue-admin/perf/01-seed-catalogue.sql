SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE [vole-papillon-damour-db];
IF EXISTS (SELECT 1 FROM Books) BEGIN RAISERROR('Books not empty, abort', 16, 1); RETURN; END;

DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @payload nvarchar(max) = REPLICATE(CAST(N'{"source":"openlibrary","k":"lorem ipsum dolor sit amet"},' AS nvarchar(max)), 110);
DECLARE @summary nvarchar(max) = REPLICATE(CAST(N'Résumé du livre, une histoire passionnante. ' AS nvarchar(max)), 23);

;WITH n AS (
    SELECT TOP (19500) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
), d AS (
    SELECT n, '978' + RIGHT('000000000' + CAST(100000000 + n AS varchar(9)), 9) AS base12 FROM n
), c AS (
    SELECT n, base12,
        (  CAST(SUBSTRING(base12,1,1) AS int) + 3*CAST(SUBSTRING(base12,2,1) AS int)
         + CAST(SUBSTRING(base12,3,1) AS int) + 3*CAST(SUBSTRING(base12,4,1) AS int)
         + CAST(SUBSTRING(base12,5,1) AS int) + 3*CAST(SUBSTRING(base12,6,1) AS int)
         + CAST(SUBSTRING(base12,7,1) AS int) + 3*CAST(SUBSTRING(base12,8,1) AS int)
         + CAST(SUBSTRING(base12,9,1) AS int) + 3*CAST(SUBSTRING(base12,10,1) AS int)
         + CAST(SUBSTRING(base12,11,1) AS int) + 3*CAST(SUBSTRING(base12,12,1) AS int)) AS s
    FROM d
)
INSERT INTO Books (Isbn13, Title, Authors, Publisher, PublicationYear, Language, QuantityAvailable, SalesCount, RejectionCount,
    IsHiddenFromCatalog, MetadataStatus, MetadataSource, ResolveAttempts, RawPayload, Summary, FirstSeenAt, UpdatedAt,
    IsAbsentFromIsbndb, IsIsbndbSourced, IsFavoriteReference, CoverUrl)
SELECT base12 + CAST((10 - s % 10) % 10 AS char(1)),
    N'Titre du livre numéro ' + CAST(n AS nvarchar(10)),
    N'Auteur ' + CAST(n % 3000 AS nvarchar(10)),
    N'Éditeur ' + CAST(n % 200 AS nvarchar(10)),
    1950 + n % 75, N'fre',
    CASE WHEN (n * 37) % 10 = 0 THEN 0 ELSE 1 + n % 3 END,
    0, 0, 0,
    CASE WHEN n > 16500 THEN 0 WHEN n % 50 = 0 THEN 3 ELSE 1 END,
    1, 0, @payload, @summary,
    DATEADD(minute, -(n * 7), @now),
    DATEADD(minute, -(n * 7) + (n * 13) % 5000, @now),
    0, 0, 0,
    N'https://covers.example/' + CAST(n AS nvarchar(10)) + N'.jpg'
FROM c;

;WITH b AS (
    SELECT Isbn13, CAST(SUBSTRING(Isbn13, 4, 9) AS int) - 100000000 AS n FROM Books WHERE MetadataStatus IN (1, 3)
), g AS (
    SELECT Isbn13, (n * 919 + 13) % 1000 AS k FROM b
)
INSERT INTO BookGenres (BookIsbn13, Name)
SELECT Isbn13,
    CASE WHEN k <= 226 THEN N'Roman' WHEN k <= 264 THEN N'Histoire' WHEN k <= 297 THEN N'Thriller'
         WHEN k <= 328 THEN N'Policier' WHEN k <= 358 THEN N'Bande dessinée' WHEN k <= 382 THEN N'Documentaire'
         WHEN k <= 405 THEN N'Romance' WHEN k <= 418 THEN N'Jeunesse' WHEN k <= 425 THEN N'Biographie'
         WHEN k <= 432 THEN N'Essai' WHEN k <= 437 THEN N'Théâtre' WHEN k <= 442 THEN N'Fantasy'
         WHEN k <= 446 THEN N'Poésie' WHEN k <= 449 THEN N'Science-fiction' WHEN k <= 452 THEN N'Manga'
         ELSE N'Cuisine' END
FROM g WHERE k <= 454;

UPDATE b SET Genre = g.Name FROM Books b JOIN BookGenres g ON g.BookIsbn13 = b.Isbn13;

INSERT INTO BookMovements (Id, Isbn13, Type, Quantity, OccurredAt, ReceivedAt, ClockSuspect)
SELECT NEWID(), Isbn13, 1, CASE WHEN QuantityAvailable > 0 THEN QuantityAvailable ELSE 1 END, FirstSeenAt, FirstSeenAt, 0 FROM Books;
INSERT INTO BookMovements (Id, Isbn13, Type, Quantity, OccurredAt, ReceivedAt, ClockSuspect)
SELECT NEWID(), Isbn13, 3, -1, UpdatedAt, UpdatedAt, 0 FROM Books WHERE QuantityAvailable = 0;

UPDATE STATISTICS Books WITH FULLSCAN;
UPDATE STATISTICS BookGenres WITH FULLSCAN;
UPDATE STATISTICS BookMovements WITH FULLSCAN;

SELECT (SELECT COUNT(*) FROM Books) AS books, (SELECT COUNT(*) FROM BookGenres) AS genres,
       (SELECT COUNT(*) FROM BookGenres WHERE Name = N'Roman') AS roman, (SELECT COUNT(*) FROM BookMovements) AS movements;
