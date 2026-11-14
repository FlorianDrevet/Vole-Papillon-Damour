-- Supprime le catalogue synthétique injecté pour le diagnostic perf (ISBN 9781000xxxxx).
SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
USE [vole-papillon-damour-db];
DELETE FROM BookMovements WHERE Isbn13 LIKE '9781%';
DELETE FROM BookGenres WHERE BookIsbn13 LIKE '9781%';
DELETE FROM Books WHERE Isbn13 LIKE '9781%';
SELECT COUNT(*) AS remaining_books FROM Books;
