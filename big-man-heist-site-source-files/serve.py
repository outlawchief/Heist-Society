#!/usr/bin/env python3
"""Serve the heist site, Unity Brotli files, and a tiny co-op session relay."""

from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
import os
import socket
from threading import Lock
from urllib.parse import urlparse

ROOT = os.path.dirname(os.path.abspath(__file__))
SESSIONS = {}
LOCK = Lock()


class HeistHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def log_message(self, fmt, *args):
        if self.path.startswith("/coop/"):
            super().log_message(fmt, *args)

    def end_headers(self):
        path = self.translate_path(self.path.split("?", 1)[0])
        if path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

    def guess_type(self, path):
        if path.endswith(".wasm") or path.endswith(".wasm.br"):
            return "application/wasm"
        if path.endswith(".js") or path.endswith(".js.br"):
            return "text/javascript"
        if path.endswith(".data") or path.endswith(".data.br"):
            return "application/octet-stream"
        guessed, _ = mimetypes.guess_type(path.replace(".br", ""))
        return guessed or "application/octet-stream"

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/lan":
            self.send_json({"url": lan_url(self.server.server_address[1])})
            return
        if parsed.path.startswith("/coop/"):
            self.handle_coop_get(parsed.path)
            return
        super().do_GET()

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path.startswith("/coop/"):
            length = int(self.headers.get("Content-Length", "0"))
            body = self.rfile.read(length).decode("utf-8") if length else "{}"
            self.handle_coop_post(parsed.path, body)
            return
        self.send_error(404)

    def session(self, path):
        parts = [p for p in path.split("/") if p]
        if len(parts) < 2:
            return None, None
        return parts[1].upper(), parts[2] if len(parts) > 2 else ""

    def handle_coop_get(self, path):
        code, kind = self.session(path)
        with LOCK:
            data = SESSIONS.get(code)
        if not data:
            self.send_error(404)
            return
        if kind == "launch":
            payload = data.get("launch", "{}")
        elif kind == "state":
            payload = data.get("state", "{}")
        elif kind == "input":
            payload = json.dumps({"items": list(data.get("inputs", {}).values())})
        else:
            payload = json.dumps({"code": code, "hasLaunch": "launch" in data})
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(payload.encode("utf-8"))

    def handle_coop_post(self, path, body):
        code, kind = self.session(path)
        with LOCK:
            data = SESSIONS.setdefault(code, {"inputs": {}})
            if kind == "launch":
                data["launch"] = body
            elif kind == "state":
                data["state"] = body
            elif kind == "input":
                try:
                    parsed = json.loads(body)
                    pid = parsed.get("id", "guest")
                    data.setdefault("inputs", {})[pid] = parsed
                except json.JSONDecodeError:
                    pass
        self.send_response(204)
        self.end_headers()

    def send_json(self, payload):
        body = json.dumps(payload).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def lan_ip():
    probe = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        probe.connect(("8.8.8.8", 80))
        return probe.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        probe.close()


def lan_url(port):
    return f"http://{lan_ip()}:{port}/index.html"


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8765"))
    server = ThreadingHTTPServer(("0.0.0.0", port), HeistHandler)
    print(f"Serving heist site at http://127.0.0.1:{port}/index.html")
    print(f"Other players on your network: {lan_url(port)}")
    print("Allow Python / TCP 8765 on the Windows firewall if teammates cannot load the page.")
    server.serve_forever()
