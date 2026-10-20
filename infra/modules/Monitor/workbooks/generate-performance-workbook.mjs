// Generates vpd-performance.workbook.json.
// Run after editing a query: node infra/modules/Monitor/workbooks/generate-performance-workbook.mjs
// The workspace resource ID is injected at deployment by workbook.module.bicep.
import {writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';

const WORKSPACE = '__WORKSPACE_ID__';
const grain = '{TimeRange:grain}';
const browserApp = 'extend App = tostring(split(_ResourceId, "/")[-1])';

let sequence = 0;

function markdown(text) {
  sequence += 1;
  return {type: 1, content: {json: text}, name: `text-${sequence}`};
}

function query(title, kql, visualization = 'table', extra = {}) {
  sequence += 1;
  return {
    type: 3,
    content: {
      version: 'KqlItem/1.0',
      query: kql,
      size: 0,
      title,
      timeContextFromParameter: 'TimeRange',
      queryType: 0,
      resourceType: 'microsoft.operationalinsights/workspaces',
      crossComponentResources: [WORKSPACE],
      visualization,
      ...extra,
    },
    customWidth: extra.customWidth,
    name: `query-${sequence}`,
  };
}

function metric(title, resourceType, resourceIds, metrics, chartId) {
  sequence += 1;
  return {
    type: 10,
    content: {
      chartId,
      version: 'MetricsItem/2.0',
      size: 2,
      chartType: 2,
      resourceType,
      metricScope: 0,
      resourceIds,
      timeContextFromParameter: 'TimeRange',
      timeContext: {durationMs: 0},
      metrics,
      title,
      gridSettings: {rowLimit: 10000},
    },
    name: `metric-${sequence}`,
  };
}

function half(item) {
  return {...item, customWidth: '50'};
}

function tab(id, items) {
  return {
    type: 12,
    content: {version: 'NotebookGroup/1.0', groupType: 'editable', items},
    conditionalVisibility: {parameterName: 'Tab', comparison: 'isEqualTo', value: id},
    name: `tab-${id}`,
  };
}

const apiRolePredicate = 'AppRoleName contains "vpd-api"';
const backendRolePredicate =
  '(AppRoleName contains "vpd-api" or AppRoleName contains "vpd-worker" or AppRoleName contains "vpd-catalog")';
const allApplicationRolePredicate =
  '(AppRoleName contains "vpd-api" or AppRoleName contains "vpd-worker" or AppRoleName contains "vpd-catalog" or AppRoleName contains "vpd-scan" or AppRoleName contains "vpd-web" or AppRoleName contains "vpd-bo")';
const api = `AppRequests\n| where ${apiRolePredicate}`;

const workbook = {
  version: 'Notebook/1.0',
  items: [
    markdown(
      '# Vole Papillon d\'Amour — performance et santé\n' +
        'Toutes les requêtes portent sur l\'espace Log Analytics commun. Pour une requête lente, copier son ' +
        '`OperationId` et suivre la procédure « Diagnostiquer une latence API » de `11-observabilite.md`.',
    ),
    {
      type: 9,
      content: {
        version: 'KqlParameterItem/1.0',
        parameters: [
          {
            id: 'c1f3a7d2-5d2e-4b8f-9a61-1d2b3c4d5e6f',
            version: 'KqlParameterItem/1.0',
            name: 'TimeRange',
            label: 'Période',
            type: 4,
            isRequired: true,
            value: {durationMs: 86400000},
            typeSettings: {
              selectableValues: [
                {durationMs: 3600000},
                {durationMs: 14400000},
                {durationMs: 43200000},
                {durationMs: 86400000},
                {durationMs: 259200000},
                {durationMs: 604800000},
                {durationMs: 2592000000},
              ],
              allowCustom: true,
            },
          },
        ],
        style: 'pills',
        queryType: 0,
        resourceType: 'microsoft.operationalinsights/workspaces',
      },
      name: 'parameters',
    },
    {
      type: 9,
      content: {
        version: 'KqlParameterItem/1.0',
        parameters: [
          {
            id: '73bc047d-87cc-4e25-b1a6-2ec01884717d',
            version: 'KqlParameterItem/1.0',
            name: 'EmbeddingCostPerMillionUsd',
            label: 'Tarif embedding (USD / million tokens)',
            description: 'Tarif d’entrée du déploiement Azure OpenAI utilisé pour estimer le coût. Ajuster à la facturation de l’abonnement.',
            type: 1,
            isRequired: true,
            value: '__EMBEDDING_COST_PER_MILLION_USD__',
          },
        ],
        style: 'formHorizontal',
        queryType: 0,
        resourceType: 'microsoft.operationalinsights/workspaces',
      },
      name: 'embedding-cost-parameter',
    },
    {
      type: 11,
      content: {
        version: 'LinkItem/1.0',
        style: 'tabs',
        links: [
          ['api', 'API'],
          ['latency', 'Où passe le temps'],
          ['errors', 'Erreurs'],
          ['worker', 'Worker'],
          ['clients', 'Front-ends et disponibilité'],
          ['embeddings', 'Embeddings et coûts'],
          ['infrastructure', 'Infrastructure'],
        ].map(([id, label]) => ({
          id: `tab-link-${id}`,
          cellValue: 'Tab',
          linkTarget: 'parameter',
          linkLabel: label,
          subTarget: id,
          style: 'link',
        })),
      },
      name: 'tabs',
    },
    tab('api', [
      half(query(
        'Débit et échecs',
        `${api}\n| summarize Requetes = count(), Echecs = countif(Success == false) by bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      half(query(
        'Latence (ms) — P50 / P95 / P99',
        `${api}\n| summarize P50 = percentile(DurationMs, 50), P95 = percentile(DurationMs, 95), P99 = percentile(DurationMs, 99) by bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      query(
        'Opérations, triées par P95',
        `${api}\n| summarize Requetes = count(), Echecs = countif(Success == false), P50 = round(percentile(DurationMs, 50)), P95 = round(percentile(DurationMs, 95)), P99 = round(percentile(DurationMs, 99)), Max = round(max(DurationMs)) by Name\n| extend TauxEchec = round(100.0 * Echecs / Requetes, 1)\n| order by P95 desc`,
      ),
      half(query(
        'Les 50 requêtes les plus lentes',
        `${api}\n| top 50 by DurationMs desc\n| project TimeGenerated, Name, DurationMs = round(DurationMs), ResultCode, OperationId`,
      )),
      half(query(
        'Codes de réponse',
        `${api}\n| summarize Requetes = count() by ResultCode, bin(TimeGenerated, ${grain})`,
        'barchart',
      )),
    ]),
    tab('latency', [
      markdown(
        'Temps passé dans chaque type de dépendance par opération. Une opération dont le temps SQL ' +
          'ou HTTP est proche de sa durée totale attend sa dépendance ; sinon le temps est dans le code API.',
      ),
      query(
        'Décomposition moyenne par opération',
        `let requests = ${api.replaceAll('\n', ' ')}\n    | project OperationId, Operation = Name, RequestMs = DurationMs;\nAppDependencies\n| where ${apiRolePredicate}\n| summarize DependencyMs = sum(DurationMs), Calls = count() by OperationId, DependencyType\n| join kind=inner requests on OperationId\n| summarize Requetes = dcount(OperationId), DureeMoyenneMs = round(avg(RequestMs)), DependanceMoyenneMs = round(avg(DependencyMs)), AppelsMoyens = round(avg(Calls), 1) by Operation, DependencyType\n| extend PartDependance = round(100.0 * DependanceMoyenneMs / DureeMoyenneMs, 1)\n| order by DependanceMoyenneMs desc`,
      ),
      half(query(
        'Requêtes SQL par opération (détection N+1)',
        `AppDependencies\n| where ${apiRolePredicate} and DependencyType =~ "SQL"\n| summarize SqlCalls = count() by OperationId\n| join kind=inner (${api.replaceAll('\n', ' ')} | project OperationId, Name) on OperationId\n| summarize AppelsSqlMoyens = round(avg(SqlCalls), 1), AppelsSqlMax = max(SqlCalls), Requetes = count() by Name\n| order by AppelsSqlMax desc\n| take 20`,
      )),
      half(query(
        'Instructions SQL les plus lentes',
        `AppDependencies\n| where DependencyType =~ "SQL"\n| summarize Appels = count(), P95 = round(percentile(DurationMs, 95)), Max = round(max(DurationMs)) by AppRoleName, Instruction = substring(Data, 0, 300)\n| order by P95 desc\n| take 30`,
      )),
      query(
        'Dépendances par cible',
        `AppDependencies\n| where ${backendRolePredicate}\n| summarize Appels = count(), Echecs = countif(Success == false), P50 = round(percentile(DurationMs, 50)), P95 = round(percentile(DurationMs, 95)) by AppRoleName, DependencyType, Target\n| order by P95 desc`,
      ),
      query(
        'Fournisseurs bibliographiques',
        `AppDependencies\n| where Name == "books.metadata.provider"\n| extend Fournisseur = tostring(Properties["book.metadata.provider"]), Issue = tostring(Properties["book.metadata.outcome"])\n| summarize Appels = count(), P50 = round(percentile(DurationMs, 50)), P95 = round(percentile(DurationMs, 95)) by AppRoleName, Fournisseur, Issue\n| order by Appels desc`,
      ),
    ]),
    tab('errors', [
      half(query(
        'Exceptions serveur par service',
        `AppExceptions\n| where ClientType != "Browser"\n| summarize Exceptions = count() by AppRoleName, bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      half(query(
        'Requêtes en échec',
        `AppRequests\n| where Success == false\n| summarize Echecs = count(), Dernier = max(TimeGenerated) by AppRoleName, Name, ResultCode\n| order by Echecs desc`,
      )),
      query(
        'Exceptions regroupées',
        `AppExceptions\n| summarize Occurrences = count(), Derniere = max(TimeGenerated), Exemple = take_any(OuterMessage), OperationId = take_any(OperationId) by AppRoleName, ClientType, ProblemId\n| order by Occurrences desc`,
      ),
      query(
        'Journaux Warning et Error',
        `AppTraces\n| where SeverityLevel >= 2\n| summarize Occurrences = count(), Dernier = max(TimeGenerated), Exemple = take_any(Message) by AppRoleName, SeverityLevel, Modele = tostring(Properties[strcat("{", "OriginalFormat", "}")])\n| order by Occurrences desc\n| take 50`,
      ),
    ]),
    tab('worker', [
      query(
        'Exécutions des fonctions',
        `AppRequests\n| where AppRoleName has "worker"\n| summarize Executions = count(), Echecs = countif(Success == false), P50 = round(percentile(DurationMs, 50)), P95 = round(percentile(DurationMs, 95)), Derniere = max(TimeGenerated) by Name\n| order by Name asc`,
      ),
      half(query(
        'Balayages et alertes e-mail',
        `AppTraces\n| where Message startswith "Worker sweep completed"\n| summarize Balayages = count(), AlertesEnvoyees = sum(toint(Properties.AlertSent)), AlertesEnEchec = sum(toint(Properties.AlertFailed)), AnnoncesBasculees = sum(toint(Properties.ReleasedAnnouncements)) by bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      half(query(
        'Durée des exécutions (ms)',
        `AppRequests\n| where AppRoleName has "worker"\n| summarize P95 = percentile(DurationMs, 95) by Name, bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
    ]),
    tab('clients', [
      half(query(
        'Disponibilité (%)',
        `AppAvailabilityResults\n| summarize Disponibilite = round(100.0 * countif(Success == true) / count(), 2) by Name, bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      half(query(
        'Rendu SSR du catalogue',
        `AppRequests\n| where AppRoleName contains "vpd-catalog"\n| summarize Requetes = count(), P95 = percentile(DurationMs, 95), Echecs = countif(Success == false) by bin(TimeGenerated, ${grain})`,
        'timechart',
      )),
      query(
        'Chargement des pages navigateur',
        `AppPageViews\n| ${browserApp}\n| summarize Vues = count(), P50 = round(percentile(DurationMs, 50)), P95 = round(percentile(DurationMs, 95)) by App, Name\n| order by Vues desc\n| take 50`,
      ),
      half(query(
        'Appels API vus du navigateur',
        `AppDependencies\n| where ClientType == "Browser"\n| ${browserApp}\n| summarize Appels = count(), Echecs = countif(Success == false), P95 = round(percentile(DurationMs, 95)) by App, Name\n| order by P95 desc\n| take 30`,
      )),
      half(query(
        'Erreurs navigateur',
        `AppExceptions\n| where ClientType == "Browser"\n| ${browserApp}\n| summarize Occurrences = count(), Exemple = take_any(OuterMessage), Derniere = max(TimeGenerated) by App, ProblemId\n| order by Occurrences desc`,
      )),
    ]),
    tab('embeddings', [
      markdown(
        'Les spans couvrent un lot envoyé au service d’embeddings. Le texte, les ISBN et les identifiants de livre ne sont pas enregistrés. ' +
          'Le coût est estimé à partir des tokens d’entrée renvoyés par Azure OpenAI et du tarif unitaire ci-dessus ; il ne remplace pas la facture Azure.',
      ),
      query(
        'Volumes, erreurs, couverture des tokens et coût estimé',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| extend Outcome = coalesce(tostring(Properties["embedding.outcome"]), iff(Success == false, "failed", "unknown"))
| extend InputTokens = tolong(Properties["gen_ai.usage.input_tokens"])
| extend EstimatedCostUsd = coalesce(InputTokens, 0) * todouble('{EmbeddingCostPerMillionUsd}') / 1000000.0
| summarize Lots = count(), Reussites = countif(Outcome == "success"), Echecs = countif(Outcome == "failed"), Annulations = countif(Outcome == "cancelled"), TokensComptabilises = countif(isnotnull(InputTokens)), TokensManquants = countif(isnull(InputTokens)), TokensEntree = sum(coalesce(InputTokens, 0)), CoutEstimeUsd = round(sum(EstimatedCostUsd), 6), P95Ms = round(percentile(DurationMs, 95)), MaxMs = round(max(DurationMs)) by AppRoleName
| order by AppRoleName asc`,
      ),
      half(query(
        'Tokens d’entrée dans le temps',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| extend InputTokens = tolong(Properties["gen_ai.usage.input_tokens"])
| summarize TokensEntree = sum(coalesce(InputTokens, 0)) by bin(TimeGenerated, ${grain}), AppRoleName`,
        'timechart',
      )),
      half(query(
        'Coût estimé dans le temps (USD)',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| extend InputTokens = tolong(Properties["gen_ai.usage.input_tokens"])
| extend CoutEstimeUsd = coalesce(InputTokens, 0) * todouble('{EmbeddingCostPerMillionUsd}') / 1000000.0
| summarize CoutEstimeUsd = sum(CoutEstimeUsd) by bin(TimeGenerated, ${grain}), AppRoleName`,
        'timechart',
      )),
      half(query(
        'Latence des lots (P50 / P95 / P99 / maximum)',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| summarize P50 = percentile(DurationMs, 50), P95 = percentile(DurationMs, 95), P99 = percentile(DurationMs, 99), MaxMs = max(DurationMs) by bin(TimeGenerated, ${grain}), AppRoleName`,
        'timechart',
      )),
      half(query(
        'Lots en échec dans le temps',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| summarize Echecs = countif(tostring(Properties["embedding.outcome"]) == "failed") by bin(TimeGenerated, ${grain}), AppRoleName`,
        'timechart',
      )),
      query(
        'Lots récents et consommation détaillée',
        `AppDependencies
| where Name == "books.recommendations.embedding.generate"
| extend Modele = tostring(Properties["gen_ai.request.model"]), Outcome = tostring(Properties["embedding.outcome"]), Textes = toint(Properties["embedding.input_count"]), TokensEntree = tolong(Properties["gen_ai.usage.input_tokens"])
| extend CoutEstimeUsd = coalesce(TokensEntree, 0) * todouble('{EmbeddingCostPerMillionUsd}') / 1000000.0
| project TimeGenerated, AppRoleName, Modele, Outcome, Textes, TokensEntree, CoutEstimeUsd = round(CoutEstimeUsd, 6), DureeMs = round(DurationMs, 1), Success, ResultCode, OperationId
| order by TimeGenerated desc
| take 100`,
      ),
    ]),
    tab('infrastructure', [
      markdown(
        'Métriques de plateforme Azure par application conteneur et base SQL. CPU et mémoire indiquent les pics par replica ; une application à zéro replica ne produit pas de mesures CPU/mémoire.',
      ),
      metric(
        'CPU des Container Apps (maximum, %)',
        'microsoft.app/containerapps',
        ['__CONTAINER_APP_IDS__'],
        [{namespace: 'microsoft.app/containerapps', metric: 'microsoft.app/containerapps-Basic-CpuPercentage', aggregation: 3, splitBy: null}],
        'workbook-vpd-containerapp-cpu',
      ),
      metric(
        'Mémoire des Container Apps (maximum, %)',
        'microsoft.app/containerapps',
        ['__CONTAINER_APP_IDS__'],
        [{namespace: 'microsoft.app/containerapps', metric: 'microsoft.app/containerapps-Basic-MemoryPercentage', aggregation: 3, splitBy: null}],
        'workbook-vpd-containerapp-memory',
      ),
      metric(
        'Réplicas et redémarrages des Container Apps',
        'microsoft.app/containerapps',
        ['__CONTAINER_APP_IDS__'],
        [
          {namespace: 'microsoft.app/containerapps', metric: 'microsoft.app/containerapps-Basic-Replicas', aggregation: 3, splitBy: null},
          {namespace: 'microsoft.app/containerapps', metric: 'microsoft.app/containerapps-Basic-RestartCount', aggregation: 3, splitBy: null},
        ],
        'workbook-vpd-containerapp-replicas',
      ),
      metric(
        'Base SQL (DTU maximum, %)',
        'microsoft.sql/servers/databases',
        ['__SQL_DATABASE_ID__'],
        [{namespace: 'microsoft.sql/servers/databases', metric: 'microsoft.sql/servers/databases-Basic-dtu_consumption_percent', aggregation: 3, splitBy: null}],
        'workbook-vpd-sql-dtu',
      ),
      query(
        'Latences et erreurs applicatives',
        `AppRequests
| where ${allApplicationRolePredicate}
| summarize Requetes = count(), Echecs = countif(Success == false), P95Ms = percentile(DurationMs, 95), P99Ms = percentile(DurationMs, 99), PicMs = max(DurationMs) by AppRoleName, bin(TimeGenerated, ${grain})
| order by TimeGenerated desc`,
        'timechart',
      ),
    ]),
  ],
  fallbackResourceIds: [WORKSPACE],
  $schema: 'https://github.com/Microsoft/Application-Insights-Workbooks/blob/master/schema/workbook.json',
};

const target = fileURLToPath(new URL('./vpd-performance.workbook.json', import.meta.url));
writeFileSync(target, `${JSON.stringify(workbook, null, 2)}\n`);
console.log(`Wrote ${target}`);
