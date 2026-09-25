"""Exporte un canvas Claude Design en pages HTML autonomes lisibles par Codex.

Usage :
    python export_maquettes.py <canvas> <dest> --url <lien du canvas> --title "<titre>" [--zip]

<canvas> est soit le dossier qui contient canvas.json et les *.dc.html (fichiers relus
depuis le canvas publié), soit un zip exporté depuis Claude Design.

Produit dans <dest> :
    preview/<Ecran>.html   page autonome (styles en ligne, sans runtime du canvas)
    preview/index.html     sommaire : écrans, tailles, zones data-zone
    sources/               *.dc.html + canvas.json, tels quels
"""
import argparse
import html
import json
import re
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path


def locate_canvas(path: Path, workdir: Path) -> Path:
    if path.is_file() and path.suffix == ".zip":
        with zipfile.ZipFile(path) as archive:
            archive.extractall(workdir)
        path = workdir
    matches = sorted(path.rglob("canvas.json"), key=lambda p: len(p.parts))
    if not matches:
        sys.exit(f"canvas.json introuvable sous {path}")
    return matches[0].parent


def zone_key(zone: str):
    head = zone.split("-")[0]
    number = re.sub(r"\D", "", head)
    return (re.sub(r"\d", "", head), int(number) if number else 0, zone)


def export(src: Path, dest: Path, url: str, title: str) -> list:
    canvas = json.loads((src / "canvas.json").read_text(encoding="utf-8"))
    pages = {p["id"]: p["name"] for p in canvas.get("pages", [])} or {"main": title}
    default_page = next(iter(pages))
    rows = {pid: [] for pid in pages}

    for sub in ("preview", "sources"):
        if (dest / sub).exists():
            shutil.rmtree(dest / sub)
        (dest / sub).mkdir(parents=True)

    for name in canvas.get("order") or sorted(canvas["boards"]):
        board = canvas["boards"][name]
        source = (src / name).read_text(encoding="utf-8")
        shutil.copy(src / name, dest / "sources" / name)
        stem = name.removesuffix(".dc.html")
        page_title = re.search(r"<title>(.*?)</title>", source, re.S)
        helmet = re.search(r"<helmet>(.*?)</helmet>", source, re.S)
        body = re.search(r"</helmet>(.*?)</x-dc>", source, re.S)
        if not body:
            sys.exit(f"{name} : structure <x-dc><helmet>…</helmet>…</x-dc> attendue")
        body = re.sub(r'href="([A-Za-z0-9_-]+)\.dc\.html"', r'href="\1.html"', body.group(1))
        (dest / "preview" / f"{stem}.html").write_text(
            "<!doctype html>\n<html lang=\"fr\">\n<head>\n<meta charset=\"utf-8\">\n"
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n"
            f"<title>{page_title.group(1).strip() if page_title else stem}</title>\n"
            f"{helmet.group(1).strip() if helmet else ''}\n</head>\n<body>\n{body.strip()}\n</body>\n</html>\n",
            encoding="utf-8",
        )
        zones = sorted(set(re.findall(r'data-zone="([^"]+)"', body)), key=zone_key)
        rows[board.get("page", default_page)].append(
            (stem, board.get("title", stem), board.get("w"), board.get("h"), zones)
        )
    shutil.copy(src / "canvas.json", dest / "sources" / "canvas.json")

    notes = [n for n in canvas.get("notes", {}).values() if not n.get("kind")]
    parts = [
        f"<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>Maquettes — {html.escape(title)}</title>",
        "<style>body{margin:0;padding:40px;font-family:'Libre Franklin',system-ui,sans-serif;background:#f7fbfe;color:#072b45}"
        "h1,h2{font-family:Georgia,serif;font-weight:500}h2{margin-top:36px}"
        "table{border-collapse:collapse;width:100%;background:#fff}td,th{border:1px solid #d9e9f4;padding:10px;text-align:left;vertical-align:top;font-size:14px}"
        "code{font-family:'IBM Plex Mono',monospace;font-size:12px;background:#e9f4fb;padding:1px 4px}"
        ".note{background:#fff;border:1px solid #d9e9f4;padding:12px 16px;margin:8px 0;font-size:14px;line-height:1.5}</style></head><body>",
        f"<h1>Maquettes — {html.escape(title)}</h1>",
        f"<p>Export du canvas Claude Design <a href=\"{url}\">{url}</a>. Chaque page est autonome. "
        "Les zones à implémenter portent un attribut <code>data-zone</code>, cité par le plan.</p>",
    ]
    for pid, name in pages.items():
        parts.append(f"<h2>{html.escape(name)}</h2><table><tr><th>Écran</th><th>Taille</th><th>Zones</th></tr>")
        for stem, board_title, w, h, zones in rows[pid]:
            zone_list = " ".join(f"<code>{z}</code>" for z in zones) or "—"
            parts.append(f"<tr><td><a href=\"{stem}.html\">{html.escape(board_title)}</a><br><code>preview/{stem}.html</code></td>"
                         f"<td>{w} × {h}</td><td>{zone_list}</td></tr>")
        parts.append("</table>")
        parts.extend(f"<div class=\"note\">{html.escape(n['text'])}</div>" for n in notes if n.get("page", default_page) == pid)
    parts.append("</body></html>")
    (dest / "preview" / "index.html").write_text("\n".join(parts), encoding="utf-8")
    return [row for page_rows in rows.values() for row in page_rows]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("canvas", type=Path)
    parser.add_argument("dest", type=Path)
    parser.add_argument("--url", required=True)
    parser.add_argument("--title", required=True)
    parser.add_argument("--zip", action="store_true", help="ajoute maquettes.zip (preview + sources)")
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as workdir:
        boards = export(locate_canvas(args.canvas, Path(workdir)), args.dest, args.url, args.title)

    if args.zip:
        zip_path = args.dest / "maquettes.zip"
        with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
            for sub in ("preview", "sources"):
                for path in sorted((args.dest / sub).rglob("*")):
                    archive.write(path, path.relative_to(args.dest).as_posix())

    print(f"{len(boards)} écrans exportés dans {args.dest / 'preview'}")
    for stem, board_title, w, h, zones in boards:
        print(f"  {stem:28} {w}x{h}  {board_title}")
        print(f"  {'':28} zones : {', '.join(zones) or 'AUCUNE'}")


if __name__ == "__main__":
    main()
