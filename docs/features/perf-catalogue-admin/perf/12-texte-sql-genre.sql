SET NOCOUNT ON;
USE [vole-papillon-damour-db];
SELECT TOP 1 st.text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.text LIKE '(@genre%' AND st.text NOT LIKE '%dm_exec%'
ORDER BY qs.max_spills DESC;
