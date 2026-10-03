#!/usr/bin/env python3
"""Serve the heist site and Unity Brotli WebGL files with the right headers."""

from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import mimetypes
import os

ROOT = os.path.dirname(os.path.abspath(__file__))


class HeistHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def end_headers(self):
        path = self.translate_path(self.path.split("?", 1)[0])
        if path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
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


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8765"))
    server = ThreadingHTTPServer(("127.0.0.1", port), HeistHandler)
    print(f"Serving heist site at http://127.0.0.1:{port}/index.html")
    server.serve_forever()
