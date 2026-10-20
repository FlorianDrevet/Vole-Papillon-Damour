import json
import os
import re
import sys
import tempfile
import unittest
from io import BytesIO
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from unittest.mock import patch

import prepare_reset_sql
from prepare_reset_sql import fetch_disabled_external_ids, render_reset_sql, safe_failure_summary


class FakeResponse(BytesIO):
    pass


class PrepareResetSqlTests(unittest.TestCase):
    def test_main_uses_graph_tenant_instead_of_azure_subscription_tenant(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            template_path = Path(temporary_directory) / "reset.sql"
            output_path = Path(temporary_directory) / "prepared.sql"
            template_path.write_text(
                "DECLARE @Ids nvarchar(max) = N'__DISABLED_ENTRA_USER_IDS_JSON__';",
                encoding="utf-8",
            )
            environment = {
                "AZURE_TENANT_ID": "azure-subscription-tenant",
                "ENTRA_GRAPH_TENANT_ID": "external-id-tenant",
                "ENTRA_GRAPH_CLIENT_ID": "graph-client-id",
                "ENTRA_GRAPH_CLIENT_SECRET": "graph-client-secret",
            }
            arguments = [
                "prepare_reset_sql",
                "--template",
                str(template_path),
                "--output",
                str(output_path),
            ]

            with (
                patch.dict(os.environ, environment, clear=True),
                patch.object(sys, "argv", arguments),
                patch.object(prepare_reset_sql, "fetch_disabled_external_ids", return_value=[]) as fetch,
            ):
                self.assertEqual(0, prepare_reset_sql.main())

        fetch.assert_called_once_with(
            "external-id-tenant", "graph-client-id", "graph-client-secret"
        )

    def test_safe_failure_summary_reports_structured_entra_error_without_sensitive_details(self):
        from urllib.error import HTTPError

        error = HTTPError(
            "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token?secret=hidden",
            403,
            "Forbidden",
            hdrs=None,
            fp=BytesIO(json.dumps({
                "error": "invalid_client",
                "error_description": "AADSTS7000215: Invalid secret client_secret=hidden tenant-id",
                "error_codes": [7000215],
            }).encode()),
        )

        summary = safe_failure_summary(error)

        self.assertIn("HTTP 403", summary)
        self.assertIn("Entra token endpoint", summary)
        self.assertIn("invalid_client", summary)
        self.assertIn("AADSTS7000215", summary)
        self.assertNotIn("hidden", summary)
        self.assertNotIn("tenant-id", summary)
        self.assertNotIn("Invalid secret", summary)
        error.close()

    def test_safe_failure_summary_reports_graph_error_code_only(self):
        from urllib.error import HTTPError

        error = HTTPError(
            "https://graph.microsoft.com/v1.0/users?$filter=private",
            403,
            "Forbidden",
            hdrs=None,
            fp=BytesIO(json.dumps({
                "error": {
                    "code": "Authorization_RequestDenied",
                    "message": "Sensitive response detail",
                }
            }).encode()),
        )

        summary = safe_failure_summary(error)

        self.assertIn("Microsoft Graph", summary)
        self.assertIn("Authorization_RequestDenied", summary)
        self.assertNotIn("private", summary)
        self.assertNotIn("Sensitive response detail", summary)
        error.close()

    def test_safe_failure_summary_does_not_report_an_unrecognized_host(self):
        from urllib.error import HTTPError

        error = HTTPError(
            "https://attacker.example/private-path",
            400,
            "Bad Request",
            hdrs=None,
            fp=BytesIO(json.dumps({"error": "invalid_request"}).encode()),
        )

        summary = safe_failure_summary(error)

        self.assertIn("Microsoft endpoint", summary)
        self.assertNotIn("attacker.example", summary)
        self.assertNotIn("private-path", summary)
        error.close()

    def test_safe_failure_summary_reports_network_error_type_without_reason(self):
        from urllib.error import URLError

        summary = safe_failure_summary(URLError("client_secret=hidden"))

        self.assertIn("network error", summary)
        self.assertIn("str", summary)
        self.assertNotIn("hidden", summary)

    def test_fetch_disabled_external_ids_paginates_and_only_accepts_disabled_users(self):
        first_id = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
        second_id = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
        requests = []

        def opener(request, timeout=30):
            requests.append(request)
            parsed = urlparse(request.full_url)
            if parsed.path.endswith("/oauth2/v2.0/token"):
                return FakeResponse(json.dumps({"access_token": "test-token"}).encode())
            if parsed.path == "/v1.0/users" and "page=2" not in request.full_url:
                return FakeResponse(json.dumps({
                    "value": [{"id": first_id, "accountEnabled": False}],
                    "@odata.nextLink": "https://graph.microsoft.com/v1.0/users?page=2",
                }).encode())
            if parsed.path == "/v1.0/users":
                return FakeResponse(json.dumps({
                    "value": [{"id": second_id, "accountEnabled": False}],
                }).encode())
            self.fail(f"Unexpected request URL: {request.full_url}")

        result = fetch_disabled_external_ids(
            "tenant-id", "client-id", "client-secret", opener=opener
        )

        self.assertEqual([first_id, second_id], result)
        graph_requests = [request for request in requests if "graph.microsoft.com" in request.full_url]
        self.assertEqual(2, len(graph_requests))
        self.assertTrue(all(request.get_header("Authorization") == "Bearer test-token" for request in graph_requests))
        token_form = parse_qs(requests[0].data.decode("utf-8"))
        self.assertEqual(["https://graph.microsoft.com/.default"], token_form["scope"])
        graph_query = parse_qs(urlparse(graph_requests[0].full_url).query)
        self.assertEqual(["accountEnabled eq false"], graph_query["$filter"])
        self.assertEqual(["id,accountEnabled"], graph_query["$select"])

    def test_fetch_disabled_external_ids_rejects_enabled_account_in_graph_response(self):
        external_id = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"

        def opener(request, timeout=30):
            if urlparse(request.full_url).path.endswith("/oauth2/v2.0/token"):
                return FakeResponse(json.dumps({"access_token": "test-token"}).encode())
            return FakeResponse(json.dumps({
                "value": [{"id": external_id, "accountEnabled": True}],
            }).encode())

        with self.assertRaisesRegex(ValueError, "enabled account"):
            fetch_disabled_external_ids(
                "tenant-id", "client-id", "client-secret", opener=opener
            )

    def test_fetch_disabled_external_ids_rejects_untrusted_pagination_host(self):
        external_id = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
        requests = []

        def opener(request, timeout=30):
            requests.append(request)
            if urlparse(request.full_url).path.endswith("/oauth2/v2.0/token"):
                return FakeResponse(json.dumps({"access_token": "test-token"}).encode())
            return FakeResponse(json.dumps({
                "value": [{"id": external_id, "accountEnabled": False}],
                "@odata.nextLink": "https://attacker.example/users?page=2",
            }).encode())

        with self.assertRaisesRegex(ValueError, "untrusted pagination URL"):
            fetch_disabled_external_ids(
                "tenant-id", "client-id", "client-secret", opener=opener
            )

        self.assertEqual(2, len(requests))

    def test_render_reset_sql_inserts_only_validated_disabled_ids(self):
        sql_template = "DECLARE @Ids nvarchar(max) = N'__DISABLED_ENTRA_USER_IDS_JSON__';"
        external_ids = [
            "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
            "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        ]

        sql = render_reset_sql(sql_template, external_ids)

        self.assertIn(
            "N'[\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\",\"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb\"]'",
            sql,
        )
        self.assertNotIn("__DISABLED_ENTRA_USER_IDS_JSON__", sql)

    def test_render_reset_sql_rejects_non_guid_external_id(self):
        sql_template = "DECLARE @Ids nvarchar(max) = N'__DISABLED_ENTRA_USER_IDS_JSON__';"

        with self.assertRaisesRegex(ValueError, "valid Entra object ID"):
            render_reset_sql(sql_template, ["not-a-guid"])

    def test_reset_sql_deletes_only_disabled_non_icloud_keeper_users(self):
        sql = self._reset_sql()

        self.assertRegex(
            sql,
            r"DELETE\s+user_row\s+FROM dbo\.Users AS user_row\s+INNER JOIN @DisabledEntraUsers AS disabled_user",
        )
        self.assertIn("user_row.Id <> @KeeperId", sql)
        self.assertIn("@DisabledUsersToDelete", sql)
        self.assertIn("INSERT INTO @RetainedUserIds", sql)
        self.assertIn("SQL user profiles to retain", sql)
        self.assertIn("EXCEPT SELECT Id FROM dbo.Users", sql)
        self.assertIn("user_row.Id = @KeeperId", sql)
        self.assertIn("OR NOT EXISTS", sql)
        self.assertNotIn("@KeeperId2", sql)
        self.assertNotIn("volepapillondamour@sfr.fr", sql)
        self.assertNotIn("DELETE FROM dbo.Users\n    WHERE Id <> @KeeperId", sql)

    def test_reset_sql_requires_only_the_icloud_profile_and_reports_that_keeper(self):
        sql = self._reset_sql()

        self.assertIn("@KeeperEmail nvarchar(320) = N'floriandrevet@icloud.com'", sql)
        self.assertIn("IF @KeeperCount <> 1", sql)
        self.assertIn("COUNT_BIG(*) FROM dbo.Users WHERE Id = @KeeperId", sql)
        self.assertIn("@KeeperEmail AS ProtectedKeeper", sql)
        self.assertNotIn("@KeeperEmail2", sql)
        self.assertNotIn("volepapillondamour@sfr.fr", sql)

    def test_reset_sql_enables_quoted_identifier_before_transaction(self):
        sql = self._reset_sql()

        self.assertIn("SET QUOTED_IDENTIFIER ON;", sql)
        self.assertLess(sql.index("SET QUOTED_IDENTIFIER ON;"), sql.index("DECLARE @ApplyReset"))

    def test_reset_sql_reassigns_audit_only_when_its_profile_will_be_deleted(self):
        sql = self._reset_sql()

        self.assertRegex(
            sql,
            r"SELECT @AssociationSettingsAuditRows = COUNT_BIG\(\*\)\s+"
            r"FROM dbo\.AssociationSettings AS settings\s+"
            r"INNER JOIN dbo\.Users AS user_row ON settings\.UpdatedBy = user_row\.Id\s+"
            r"INNER JOIN @DisabledEntraUsers AS disabled_user\s+"
            r"ON TRY_CONVERT\(uniqueidentifier, user_row\.ExternalId\) = disabled_user\.ExternalId\s+"
            r"WHERE user_row\.Id <> @KeeperId",
        )
        self.assertRegex(
            sql,
            r"UPDATE settings\s+SET UpdatedBy = @KeeperId,\s+"
            r"UpdatedAt = SYSUTCDATETIME\(\)\s+"
            r"FROM dbo\.AssociationSettings AS settings\s+"
            r"INNER JOIN dbo\.Users AS user_row ON settings\.UpdatedBy = user_row\.Id\s+"
            r"INNER JOIN @DisabledEntraUsers AS disabled_user\s+"
            r"ON TRY_CONVERT\(uniqueidentifier, user_row\.ExternalId\) = disabled_user\.ExternalId\s+"
            r"WHERE user_row\.Id <> @KeeperId",
        )

    def test_reset_sql_brackets_reserved_rowcount_aliases(self):
        sql = self._reset_sql()

        self.assertNotRegex(sql, r"\bAS\s+RowCount\b")
        self.assertGreaterEqual(len(re.findall(r"\bAS\s+\[RowCount\]", sql)), 2)

    def test_reset_sql_clears_manual_requests_before_requesters(self):
        sql = self._reset_sql()
        request_position = sql.index("(1, N'ManualBookRequests')")
        requester_position = sql.index("(2, N'ManualRequesters')")
        books_position = sql.index("(21, N'Books')")

        self.assertLess(request_position, requester_position)
        self.assertLess(requester_position, books_position)
        self.assertIn("N'ManualBookRequests'", sql)
        self.assertIn("N'ManualRequesters'", sql)

    def test_reset_sql_contains_a_single_graph_id_marker(self):
        sql = self._reset_sql()

        self.assertEqual(1, sql.count("__DISABLED_ENTRA_USER_IDS_JSON__"))

    def test_real_reset_sql_template_renders_a_disabled_identity_set(self):
        prepared = render_reset_sql(
            self._reset_sql(), ["aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"]
        )

        self.assertNotIn("__DISABLED_ENTRA_USER_IDS_JSON__", prepared)
        self.assertIn(
            "N'[\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\"]'", prepared
        )

    @staticmethod
    def _reset_sql():
        repository_root = Path(__file__).resolve().parents[2]
        sql_path = repository_root / ".agents/skills/book-data-reset/resources/reset-book-data.sql"
        return sql_path.read_text(encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
