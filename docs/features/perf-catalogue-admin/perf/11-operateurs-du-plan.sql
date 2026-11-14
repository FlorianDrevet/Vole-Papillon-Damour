SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
USE [vole-papillon-damour-db];
;WITH XMLNAMESPACES (DEFAULT 'http://schemas.microsoft.com/sqlserver/2004/07/showplan'),
q AS (
    SELECT TOP 1 qp.query_plan, st.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
    CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
    WHERE st.text LIKE '(@genre%' AND st.text NOT LIKE '%dm_exec%'
    ORDER BY qs.max_spills DESC, qs.total_logical_reads DESC
)
SELECT
    op.value('@NodeId', 'int') AS node,
    op.value('@PhysicalOp', 'varchar(40)') AS physical_op,
    CAST(op.value('@EstimateRows', 'float') AS decimal(12,1)) AS est_rows,
    COALESCE(op.value('(./*/Object/@Index)[1]', 'varchar(80)'), op.value('(./*/*/Object/@Index)[1]', 'varchar(80)'), '') AS idx,
    COALESCE(op.value('(./*/Object/@Table)[1]', 'varchar(40)'), '') AS tbl
FROM q CROSS APPLY q.query_plan.nodes('//RelOp') x(op)
ORDER BY node;
