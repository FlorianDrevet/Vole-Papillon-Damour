"""Resolve disabled Entra users and prepare a guarded SQL reset script."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from pathlib import Path
from typing import Callable
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import Request, urlopen
from uuid import UUID


GRAPH_USERS_URL = "https://graph.microsoft.com/v1.0/users"
DISABLED_IDS_MARKER = "__DISABLED_ENTRA_USER_IDS_JSON__"


def _read_json(opener: Callable, request: Request) -> dict:
    with opener(request, timeout=30) as response:
        payload = json.loads(response.read().decode("utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("Microsoft returned an unexpected response.")
    return payload


def _get_access_token(
    tenant_id: str,
    client_id: str,
    client_secret: str,
    opener: Callable,
) -> str:
    token_url = f"https://login.microsoftonline.com/{tenant_id}/oauth2/v2.0/token"
    form = urlencode(
        {
            "client_id": client_id,
            "client_secret": client_secret,
            "scope": "https://graph.microsoft.com/.default",
            "grant_type": "client_credentials",
        }
    ).encode("utf-8")
    request = Request(
        token_url,
        data=form,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        method="POST",
    )
    payload = _read_json(opener, request)
    token = payload.get("access_token")
    if not isinstance(token, str) or not token:
        raise ValueError("Microsoft Entra did not return an access token.")
    return token


def _normalize_external_id(value: object) -> str:
    if not isinstance(value, str):
        raise ValueError("Microsoft Graph returned an invalid Entra object ID.")
    try:
        return str(UUID(value))
    except ValueError as exception:
        raise ValueError("Microsoft Graph returned an invalid Entra object ID.") from exception


def fetch_disabled_external_ids(
    tenant_id: str,
    client_id: str,
    client_secret: str,
    opener: Callable = urlopen,
) -> list[str]:
    """Return sorted Entra object IDs whose accountEnabled value is false."""
    if not tenant_id or not client_id or not client_secret:
        raise ValueError("Microsoft Graph credentials are incomplete.")

    token = _get_access_token(tenant_id, client_id, client_secret, opener)
    query = urlencode(
        {
            "$select": "id,accountEnabled",
            "$filter": "accountEnabled eq false",
            "$top": "999",
        }
    )
    next_url = f"{GRAPH_USERS_URL}?{query}"
    disabled_ids: set[str] = set()

    while next_url:
        parsed_url = urlparse(next_url)
        if parsed_url.scheme != "https" or parsed_url.hostname != "graph.microsoft.com":
            raise ValueError("Microsoft Graph returned an untrusted pagination URL.")

        request = Request(next_url, headers={"Authorization": f"Bearer {token}"})
        payload = _read_json(opener, request)
        users = payload.get("value")
        if not isinstance(users, list):
            raise ValueError("Microsoft Graph returned an unexpected user list.")

        for user in users:
            if not isinstance(user, dict) or user.get("accountEnabled") is not False:
                raise ValueError("Microsoft Graph returned an enabled account in the disabled-user result.")
            disabled_ids.add(_normalize_external_id(user.get("id")))

        next_url = payload.get("@odata.nextLink", "")
        if not isinstance(next_url, str):
            raise ValueError("Microsoft Graph returned an invalid pagination URL.")

    return sorted(disabled_ids)


def render_reset_sql(sql_template: str, disabled_external_ids: list[str]) -> str:
    """Insert only validated Entra object IDs into the SQL template."""
    if sql_template.count(DISABLED_IDS_MARKER) != 1:
        raise ValueError("The SQL template must contain exactly one disabled-user marker.")

    normalized_ids = sorted({_normalize_external_id(value) for value in disabled_external_ids})
    ids_json = json.dumps(normalized_ids, separators=(",", ":"))
    sql_json = ids_json.replace("'", "''")
    return sql_template.replace(DISABLED_IDS_MARKER, sql_json)


def _write_github_outputs(output_path: Path, disabled_external_ids: list[str]) -> None:
    digest_source = "\n".join(disabled_external_ids).encode("utf-8")
    digest = hashlib.sha256(digest_source).hexdigest()
    with output_path.open("a", encoding="utf-8", newline="\n") as output:
        output.write(f"disabled_entra_id_count={len(disabled_external_ids)}\n")
        output.write(f"disabled_entra_id_sha256={digest}\n")


def safe_failure_summary(exception: Exception) -> str:
    """Describe setup failures without logging URLs, response bodies, or credentials."""
    if isinstance(exception, HTTPError):
        return f"Microsoft endpoint returned HTTP {exception.code}."
    if isinstance(exception, URLError):
        return f"Microsoft endpoint network error ({type(exception.reason).__name__})."
    if isinstance(exception, ValueError):
        return f"Reset input validation failed: {exception}"
    return f"Reset preparation failed ({type(exception).__name__})."


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--template", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--github-output", type=Path)
    arguments = parser.parse_args()

    try:
        disabled_ids = fetch_disabled_external_ids(
            os.environ.get("AZURE_TENANT_ID", ""),
            os.environ.get("ENTRA_GRAPH_CLIENT_ID", ""),
            os.environ.get("ENTRA_GRAPH_CLIENT_SECRET", ""),
        )
        template = arguments.template.read_text(encoding="utf-8")
        prepared_sql = render_reset_sql(template, disabled_ids)
        arguments.output.write_text(prepared_sql, encoding="utf-8", newline="\n")
        if arguments.github_output is not None:
            _write_github_outputs(arguments.github_output, disabled_ids)
    except (HTTPError, URLError, OSError, ValueError, json.JSONDecodeError) as exception:
        print(
            f"{safe_failure_summary(exception)} No database connection was opened.",
            file=sys.stderr,
        )
        return 1

    print(f"Resolved {len(disabled_ids)} disabled Entra account IDs; identifiers are not written to the log.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
