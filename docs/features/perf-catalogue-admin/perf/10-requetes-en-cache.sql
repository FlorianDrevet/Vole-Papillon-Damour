SET NOCOUNT ON;
USE [vole-papillon-damour-db];
SELECT TOP 12
    qs.execution_count AS execs,
    qs.total_elapsed_time / qs.execution_count / 1000.0 AS avg_ms,
    qs.total_logical_reads / qs.execution_count AS avg_reads,
    qs.max_grant_kb, qs.max_spills,
    REPLACE(REPLACE(LEFT(st.text, 300), CHAR(10), ' '), CHAR(13), ' ') AS sql_head
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.text LIKE '%BookGenres%' AND st.text NOT LIKE '%dm_exec_query_stats%'
ORDER BY qs.total_logical_reads DESC;
