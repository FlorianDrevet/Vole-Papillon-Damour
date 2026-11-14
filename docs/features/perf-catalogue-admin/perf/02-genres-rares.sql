SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
USE [vole-papillon-damour-db];
;WITH names AS (
    SELECT * FROM (VALUES (1,N'Science Fiction'),(2,N'Amour'),(3,N'Cheval'),(4,N'édition commerciale'),(5,N'Erotische Literatur'),
        (6,N'Érotisme'),(7,N'French Erotic literature'),(8,N'History and criticism'),(9,N'Littérature érotique'),
        (10,N'Ouvrages illustrés'),(11,N'Pornography'),(12,N'Thème littéraire')) v(i, Name)
), target AS (
    SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY Isbn13) AS i, Isbn13 FROM Books
    WHERE MetadataStatus = 1 AND Isbn13 NOT IN (SELECT BookIsbn13 FROM BookGenres) AND Isbn13 > '9781000050000'
)
INSERT INTO BookGenres (BookIsbn13, Name) SELECT t.Isbn13, n.Name FROM target t JOIN names n ON n.i = t.i;
UPDATE STATISTICS BookGenres WITH FULLSCAN;
DBCC FREEPROCCACHE WITH NO_INFOMSGS;
SELECT COUNT(DISTINCT Name) AS distinct_genres FROM BookGenres;
