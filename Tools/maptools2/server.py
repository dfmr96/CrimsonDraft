#!/usr/bin/env python3
"""
MapTools server — correr con: python server.py
Abre http://localhost:8000 en el navegador.
"""
import http.server, json, os, sys
from pathlib import Path

PORT        = 8000
ROOT        = Path(__file__).parent
PROJECT_F   = ROOT / "project.json"
MAIN_SAVE_F = ROOT / "main_save.json"

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(ROOT), **kw)

    def log_message(self, fmt, *args):
        pass  # silenciar logs de requests estáticos

    def do_GET(self):
        if self.path == "/api/project":
            self._send_json(self._load_project())
        elif self.path == "/api/main_save":
            self._send_json(self._load_main_save())
        else:
            super().do_GET()

    def do_POST(self):
        if self.path == "/api/project":
            length = int(self.headers.get("Content-Length", 0))
            body   = self.rfile.read(length)
            try:
                data = json.loads(body)
                PROJECT_F.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
                self._send_json({"ok": True})
                print(f"[save] project.json — {len(body)//1024} KB")
            except Exception as e:
                self._send_json({"ok": False, "error": str(e)}, 400)

        elif self.path.startswith("/api/images/"):
            # Guardar imagen: POST /api/images/<filename>
            # Body: bytes crudos de la imagen (jpeg/png/webp)
            filename = self.path[len("/api/images/"):]
            # Validar nombre — solo alfanumérico, guiones, puntos, sin path traversal
            import re
            if not re.match(r'^[\w\-\.]+$', filename) or ".." in filename:
                self._send_json({"ok": False, "error": "nombre inválido"}, 400)
                return
            length = int(self.headers.get("Content-Length", 0))
            body   = self.rfile.read(length)
            img_dir = ROOT / "images"
            img_dir.mkdir(exist_ok=True)
            (img_dir / filename).write_bytes(body)
            self._send_json({"ok": True, "path": f"/images/{filename}"})
            print(f"[img] guardado {filename} — {len(body)//1024} KB")

        elif self.path.startswith("/api/images-delete/"):
            # Borrar imagen: POST /api/images-delete/<filename>
            import re
            filename = self.path[len("/api/images-delete/"):]
            if not re.match(r'^[\w\-\.]+$', filename) or ".." in filename:
                self._send_json({"ok": False, "error": "nombre inválido"}, 400)
                return
            img_path = ROOT / "images" / filename
            if img_path.exists():
                img_path.unlink()
            self._send_json({"ok": True})

        else:
            self.send_error(404)

    def do_OPTIONS(self):
        self.send_response(200)
        self._cors()
        self.end_headers()

    def _load_main_save(self):
        if MAIN_SAVE_F.exists():
            try:
                return json.loads(MAIN_SAVE_F.read_text(encoding="utf-8"))
            except:
                pass
        return {"exists": False}

    def _load_project(self):
        if PROJECT_F.exists():
            try:
                return json.loads(PROJECT_F.read_text(encoding="utf-8"))
            except:
                pass
        return {"exists": False}

    def _send_json(self, data, code=200):
        body = json.dumps(data).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self._cors()
        self.end_headers()
        self.wfile.write(body)

    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")

if __name__ == "__main__":
    os.chdir(ROOT)
    server = http.server.HTTPServer(("", PORT), Handler)
    print(f"MapTools corriendo en http://localhost:{PORT}")
    print("Ctrl+C para detener.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServidor detenido.")
