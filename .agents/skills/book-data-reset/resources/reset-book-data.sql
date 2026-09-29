/*
   Reset only the deployed VPD books application data.

   Default: dry run. Review the counts, then set @ApplyReset = 1 and run again.
   Target guard: vole-papillon-damour-db.
   Keeper accounts: floriandrevet@icloud.com and volepapillondamour@sfr.fr.

   This script intentionally does not delete events, news, products, orders,
   Entra identities, blobs, or account-deletion outbox jobs.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ApplyReset bit = 0;
DECLARE @KeeperEmail1 nvarchar(320) = N'floriandrevet@icloud.com';
DECLARE @KeeperEmail2 nvarchar(320) = N'volepapillondamour@sfr.fr';
DECLARE @KeeperId1 uniqueidentifier;
DECLARE @KeeperId2 uniqueidentifier;
DECLARE @KeeperCount1 bigint;
DECLARE @KeeperCount2 bigint;
DECLARE @OtherUsers bigint;
DECLARE @AssociationSettingsAuditRows bigint = 0;
DECLARE @AlertOutboxRows bigint = 0;
DECLARE @TableName sysname;
DECLARE @RowCount bigint;
DECLARE @Sql nvarchar(max);

DECLARE @BookTables table
(
    SortOrder int NOT NULL PRIMARY KEY,
    TableName sysname NOT NULL UNIQUE
);

INSERT INTO @BookTables (SortOrder, TableName)
VALUES
    (1, N'BookAnnouncements'),
    (2, N'BookMovements'),
    (3, N'CheckoutPassageLines'),
    (4, N'CheckoutPassages'),
    (5, N'MemberSelectionItems'),
    (6, N'WatchlistItems'),
    (7, N'UserAlertHistory'),
    (8, N'EmailBounceEvents'),
    (9, N'MemberCards'),
    (10, N'MemberRecommendationPreferences'),
    (11, N'BookNotFoundReports'),
    (12, N'BookNeighbors'),
    (13, N'BookSimilarityProfiles'),
    (14, N'RareBookPhotos'),
    (15, N'RareBookTombstones'),
    (16, N'ScanSessions'),
    (17, N'RareBooks'),
    (18, N'Watchlists'),
    (19, N'Books');

DECLARE @ProtectedTables table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @ProtectedTables (TableName)
VALUES
    (N'AssoEvents'),
    (N'Parties'),
    (N'LinePartie'),
    (N'Lots'),
    (N'Actualities'),
    (N'SocialPostImports');

DECLARE @AllowedUserReferences table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @AllowedUserReferences (TableName)
VALUES
    (N'AssociationSettings'),
    (N'BookMovements'),
    (N'CheckoutPassages'),
    (N'EmailBounceEvents'),
    (N'MemberCards'),
    (N'MemberRecommendationPreferences'),
    (N'MemberSelectionItems'),
    (N'RareBookPhotos'),
    (N'RareBooks'),
    (N'ScanSessions'),
    (N'UserAlertHistory'),
    (N'Watchlists');

DECLARE @Preview table
(
    TableName sysname NOT NULL PRIMARY KEY,
    RowsBefore bigint NOT NULL
);

DECLARE @ProtectedCounts table
(
    TableName sysname NOT NULL PRIMARY KEY,
    RowsBefore bigint NOT NULL
);

IF DB_NAME() <> N'vole-papillon-damour-db'
BEGIN
    ;THROW 51000, N'Wrong database. This script is restricted to vole-papillon-damour-db.', 1;
END;

IF @ApplyReset NOT IN (0, 1)
BEGIN
    ;THROW 51001, N'@ApplyReset must be 0 (dry run) or 1 (execute).', 1;
END;

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AssoEvents', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Actualities', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Books', N'U') IS NULL
BEGIN
    ;THROW 51002, N'A required Users, AssoEvents, Actualities, or Books table is missing.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @KeeperCount1 = COUNT_BIG(*)
    FROM dbo.Users
    WHERE LOWER(LTRIM(RTRIM(Email))) = @KeeperEmail1;

    SELECT @KeeperCount2 = COUNT_BIG(*)
    FROM dbo.Users
    WHERE LOWER(LTRIM(RTRIM(Email))) = @KeeperEmail2;

    IF @KeeperCount1 <> 1 OR @KeeperCount2 <> 1
    BEGIN
        ;THROW 51003, N'Each keeper email must match exactly one row in dbo.Users. Nothing was changed.', 1;
    END;

    SELECT @KeeperId1 = Id FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(Email))) = @KeeperEmail1;
    SELECT @KeeperId2 = Id FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(Email))) = @KeeperEmail2;

    SELECT @OtherUsers = COUNT_BIG(*)
    FROM dbo.Users
    WHERE Id NOT IN (@KeeperId1, @KeeperId2);

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys AS fk
        INNER JOIN sys.tables AS referencing_table ON referencing_table.object_id = fk.parent_object_id
        INNER JOIN sys.schemas AS referencing_schema ON referencing_schema.schema_id = referencing_table.schema_id
        WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.Users')
          AND NOT
          (
              referencing_schema.name = N'dbo'
              AND EXISTS
              (
                  SELECT 1
                  FROM @AllowedUserReferences AS allowed_reference
                  WHERE allowed_reference.TableName = referencing_table.name
              )
          )
    )
    BEGIN
        ;THROW 51004, N'An unreviewed foreign key references dbo.Users. Nothing was changed.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.triggers AS trigger_definition
        INNER JOIN sys.tables AS target_table ON target_table.object_id = trigger_definition.parent_id
        INNER JOIN sys.schemas AS target_schema ON target_schema.schema_id = target_table.schema_id
        WHERE trigger_definition.is_disabled = 0
          AND target_schema.name = N'dbo'
          AND
          (
              target_table.name IN (SELECT TableName FROM @BookTables)
              OR target_table.name IN (N'Users', N'OutboxMessages', N'AssociationSettings', N'RecommendationGenerations')
          )
    )
    BEGIN
        ;THROW 51005, N'An enabled trigger exists on a table this script changes. Nothing was changed.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys AS fk
        INNER JOIN sys.tables AS child_table ON child_table.object_id = fk.parent_object_id
        INNER JOIN sys.schemas AS child_schema ON child_schema.schema_id = child_table.schema_id
        INNER JOIN sys.tables AS parent_table ON parent_table.object_id = fk.referenced_object_id
        INNER JOIN sys.schemas AS parent_schema ON parent_schema.schema_id = parent_table.schema_id
        WHERE fk.delete_referential_action = 1
          AND child_schema.name = N'dbo'
          AND child_table.name IN (SELECT TableName FROM @ProtectedTables)
          AND parent_schema.name = N'dbo'
          AND
          (
              parent_table.name IN (SELECT TableName FROM @BookTables)
              OR parent_table.name IN (N'Users', N'OutboxMessages', N'AssociationSettings', N'RecommendationGenerations')
          )
    )
    BEGIN
        ;THROW 51006, N'A cascade from a changed table reaches protected event/news data. Nothing was changed.', 1;
    END;

    DECLARE table_counts CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @BookTables ORDER BY SortOrder;

    OPEN table_counts;
    FETCH NEXT FROM table_counts INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @RowCount = 0;

        IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NOT NULL
        BEGIN
            SET @Sql = N'SELECT @CountOut = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@TableName) + N';';
            EXEC sys.sp_executesql
                @Sql,
                N'@CountOut bigint OUTPUT',
                @CountOut = @RowCount OUTPUT;
        END;

        INSERT INTO @Preview (TableName, RowsBefore) VALUES (@TableName, @RowCount);
        FETCH NEXT FROM table_counts INTO @TableName;
    END;

    CLOSE table_counts;
    DEALLOCATE table_counts;

    IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NOT NULL
    BEGIN
        SELECT @AlertOutboxRows = COUNT_BIG(*)
        FROM dbo.OutboxMessages
        WHERE Kind = 1;

        INSERT INTO @Preview (TableName, RowsBefore)
        VALUES (N'OutboxMessages (book alerts only)', @AlertOutboxRows);
    END;

    IF OBJECT_ID(N'dbo.AssociationSettings', N'U') IS NOT NULL
    BEGIN
        SELECT @AssociationSettingsAuditRows = COUNT_BIG(*)
        FROM dbo.AssociationSettings
        WHERE UpdatedBy NOT IN (@KeeperId1, @KeeperId2);

        INSERT INTO @Preview (TableName, RowsBefore)
        VALUES (N'AssociationSettings audit reassignment', @AssociationSettingsAuditRows);
    END;

    SELECT N'Users to delete' AS TableName, @OtherUsers AS RowCount
    UNION ALL
    SELECT TableName, RowsBefore AS RowCount FROM @Preview
    ORDER BY TableName;

    DECLARE protected_counts CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @ProtectedTables ORDER BY TableName;

    OPEN protected_counts;
    FETCH NEXT FROM protected_counts INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NOT NULL
        BEGIN
            SET @Sql = N'SELECT @CountOut = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@TableName) + N';';
            EXEC sys.sp_executesql
                @Sql,
                N'@CountOut bigint OUTPUT',
                @CountOut = @RowCount OUTPUT;

            INSERT INTO @ProtectedCounts (TableName, RowsBefore) VALUES (@TableName, @RowCount);
        END;

        FETCH NEXT FROM protected_counts INTO @TableName;
    END;

    CLOSE protected_counts;
    DEALLOCATE protected_counts;

    SELECT N'Protected event/news counts before reset' AS CheckName, TableName, RowsBefore AS RowCount
    FROM @ProtectedCounts
    ORDER BY TableName;

    IF @ApplyReset = 0
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'DRY RUN - no data changed. Review counts, set @ApplyReset = 1, and execute again.' AS Result,
               DB_NAME() AS DatabaseName,
               @KeeperCount1 AS Keeper1Matches,
               @KeeperCount2 AS Keeper2Matches;
        RETURN;
    END;

    DECLARE delete_tables CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @BookTables ORDER BY SortOrder;

    OPEN delete_tables;
    FETCH NEXT FROM delete_tables INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NOT NULL
        BEGIN
            IF @TableName = N'Books'
            BEGIN
                EXEC sys.sp_executesql N'UPDATE dbo.Books SET RedirectedToIsbn13 = NULL WHERE RedirectedToIsbn13 IS NOT NULL;';
            END;

            SET @Sql = N'DELETE FROM dbo.' + QUOTENAME(@TableName) + N';';
            EXEC sys.sp_executesql @Sql;
        END;

        FETCH NEXT FROM delete_tables INTO @TableName;
    END;

    CLOSE delete_tables;
    DEALLOCATE delete_tables;

    IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.OutboxMessages WHERE Kind = 1;
    END;

    IF OBJECT_ID(N'dbo.AssociationSettings', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.AssociationSettings
        SET UpdatedBy = @KeeperId1,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UpdatedBy NOT IN (@KeeperId1, @KeeperId2);
    END;

    IF OBJECT_ID(N'dbo.RecommendationGenerations', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.RecommendationGenerations
        SET CurrentGenerationId = NULL,
            ComputedAt = NULL,
            BookCount = 0;
    END;

    DELETE FROM dbo.Users
    WHERE Id NOT IN (@KeeperId1, @KeeperId2);

    DECLARE verify_book_tables CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @BookTables ORDER BY SortOrder;

    OPEN verify_book_tables;
    FETCH NEXT FROM verify_book_tables INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NOT NULL
        BEGIN
            SET @Sql = N'SELECT @CountOut = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@TableName) + N';';
            EXEC sys.sp_executesql
                @Sql,
                N'@CountOut bigint OUTPUT',
                @CountOut = @RowCount OUTPUT;

            IF @RowCount <> 0
            BEGIN
                ;THROW 51007, N'A book data table is not empty after deletion. The transaction will roll back.', 1;
            END;
        END;

        FETCH NEXT FROM verify_book_tables INTO @TableName;
    END;

    CLOSE verify_book_tables;
    DEALLOCATE verify_book_tables;

    IF (SELECT COUNT_BIG(*) FROM dbo.Users) <> 2
       OR EXISTS
       (
           SELECT 1
           FROM dbo.Users
           WHERE LOWER(LTRIM(RTRIM(Email))) NOT IN (@KeeperEmail1, @KeeperEmail2)
              OR Email IS NULL
       )
    BEGIN
        ;THROW 51008, N'dbo.Users does not contain exactly the two keeper accounts. The transaction will roll back.', 1;
    END;

    IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.OutboxMessages WHERE Kind = 1)
    BEGIN
        ;THROW 51009, N'Book alert outbox rows remain. The transaction will roll back.', 1;
    END;

    DECLARE verify_protected_counts CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @ProtectedCounts ORDER BY TableName;

    OPEN verify_protected_counts;
    FETCH NEXT FROM verify_protected_counts INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'SELECT @CountOut = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@TableName) + N';';
        EXEC sys.sp_executesql
            @Sql,
            N'@CountOut bigint OUTPUT',
            @CountOut = @RowCount OUTPUT;

        IF @RowCount <> (SELECT RowsBefore FROM @ProtectedCounts WHERE TableName = @TableName)
        BEGIN
            ;THROW 51010, N'An event/news row count changed during the reset. The transaction will roll back.', 1;
        END;

        FETCH NEXT FROM verify_protected_counts INTO @TableName;
    END;

    CLOSE verify_protected_counts;
    DEALLOCATE verify_protected_counts;

    COMMIT TRANSACTION;

    SELECT N'COMMITTED' AS Result,
           DB_NAME() AS DatabaseName,
           @KeeperEmail1 AS Keeper1,
           @KeeperEmail2 AS Keeper2,
           @OtherUsers AS UsersDeleted,
           @AlertOutboxRows AS BookAlertOutboxRowsDeleted,
           SYSUTCDATETIME() AS CompletedAtUtc;

    SELECT TableName, RowsBefore AS ProtectedRowsAfterReset
    FROM @ProtectedCounts
    ORDER BY TableName;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'table_counts') >= 0
    BEGIN
        CLOSE table_counts;
    END;
    IF CURSOR_STATUS('local', 'table_counts') > -3
    BEGIN
        DEALLOCATE table_counts;
    END;

    IF CURSOR_STATUS('local', 'protected_counts') >= 0
    BEGIN
        CLOSE protected_counts;
    END;
    IF CURSOR_STATUS('local', 'protected_counts') > -3
    BEGIN
        DEALLOCATE protected_counts;
    END;

    IF CURSOR_STATUS('local', 'delete_tables') >= 0
    BEGIN
        CLOSE delete_tables;
    END;
    IF CURSOR_STATUS('local', 'delete_tables') > -3
    BEGIN
        DEALLOCATE delete_tables;
    END;

    IF CURSOR_STATUS('local', 'verify_book_tables') >= 0
    BEGIN
        CLOSE verify_book_tables;
    END;
    IF CURSOR_STATUS('local', 'verify_book_tables') > -3
    BEGIN
        DEALLOCATE verify_book_tables;
    END;

    IF CURSOR_STATUS('local', 'verify_protected_counts') >= 0
    BEGIN
        CLOSE verify_protected_counts;
    END;
    IF CURSOR_STATUS('local', 'verify_protected_counts') > -3
    BEGIN
        DEALLOCATE verify_protected_counts;
    END;

    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
