"""Serve the RE2 room viewer on http://127.0.0.1:8765 and, when RE2 (GOG, LeonU.exe) is running,
expose the live player position / room / camera by READING (never writing) its memory.

Usage: python -I live.py "<RE2 install dir>"
Addresses were found by disassembling the GOG LeonU.exe; other builds (ClaireU.exe, other
releases) are not supported for live mode.
"""
import ctypes
import ctypes.wintypes as wt
import json
import os
import re
import struct
import sys
import threading
import webbrowser
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

PORT = 8765
EXE = "leonu.exe"
IMAGE_BASE = 0x400000
ADDR = {
    "stage": 0x999114, "room": 0x999116, "cut": 0x999118,      # u16 each
    "pos": 0x994528,                                             # i32 x, y, z
    "floor": 0x9945F6,                                           # u8
    "file": 0x688370,                                            # "Pl0\Rdu\room1030.rdt"
}

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
TH32CS_SNAPPROCESS, TH32CS_SNAPMODULE, TH32CS_SNAPMODULE32 = 0x2, 0x8, 0x10
PROCESS_VM_READ, PROCESS_QUERY_INFORMATION = 0x10, 0x400


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("cntUsage", wt.DWORD), ("th32ProcessID", wt.DWORD),
                ("th32DefaultHeapID", ctypes.c_void_p), ("th32ModuleID", wt.DWORD), ("cntThreads", wt.DWORD),
                ("th32ParentProcessID", wt.DWORD), ("pcPriClassBase", ctypes.c_long), ("dwFlags", wt.DWORD),
                ("szExeFile", ctypes.c_wchar * 260)]


class MODULEENTRY32W(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("th32ModuleID", wt.DWORD), ("th32ProcessID", wt.DWORD),
                ("GlblcntUsage", wt.DWORD), ("ProccntUsage", wt.DWORD), ("modBaseAddr", ctypes.c_void_p),
                ("modBaseSize", wt.DWORD), ("hModule", wt.HMODULE), ("szModule", ctypes.c_wchar * 256),
                ("szExePath", ctypes.c_wchar * 260)]


k32.CreateToolhelp32Snapshot.restype = wt.HANDLE
k32.OpenProcess.restype = wt.HANDLE
k32.ReadProcessMemory.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t,
                                  ctypes.POINTER(ctypes.c_size_t)]


def find_process():
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    try:
        ok = k32.Process32FirstW(snap, ctypes.byref(entry))
        while ok:
            if entry.szExeFile.lower() in (EXE, "claireu.exe"):
                return entry.th32ProcessID, entry.szExeFile.lower()
            ok = k32.Process32NextW(snap, ctypes.byref(entry))
    finally:
        k32.CloseHandle(snap)
    return None, None


def module_base(pid, name):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid)
    entry = MODULEENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    try:
        ok = k32.Module32FirstW(snap, ctypes.byref(entry))
        while ok:
            if entry.szModule.lower() == name:
                return entry.modBaseAddr
            ok = k32.Module32NextW(snap, ctypes.byref(entry))
    finally:
        k32.CloseHandle(snap)
    return IMAGE_BASE


class Game:
    def __init__(self):
        self.handle, self.base, self.pid = None, IMAGE_BASE, None
        self.lock = threading.Lock()

    def _attach(self):
        pid, name = find_process()
        if pid is None:
            return "RE2 no está abierto"
        if name != EXE:
            return f"{name} no soportado: las direcciones solo se verificaron en LeonU.exe"
        if pid != self.pid:
            if self.handle:
                k32.CloseHandle(self.handle)
            self.handle = k32.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, False, pid)
            if not self.handle:
                self.pid = None
                return f"No se pudo abrir el proceso (error {ctypes.get_last_error()})"
            self.pid, self.base = pid, module_base(pid, name)
        return None

    def _read(self, va, size):
        buf = ctypes.create_string_buffer(size)
        got = ctypes.c_size_t()
        addr = self.base + (va - IMAGE_BASE)
        if not k32.ReadProcessMemory(self.handle, ctypes.c_void_p(addr), buf, size, ctypes.byref(got)):
            raise OSError(ctypes.get_last_error())
        return buf.raw[:got.value]

    def state(self):
        with self.lock:
            err = self._attach()
            if err:
                return {"connected": False, "error": err}
            try:
                stage, room, cut = struct.unpack("<3H", self._read(ADDR["stage"], 6))
                x, y, z = struct.unpack("<3i", self._read(ADDR["pos"], 12))
                floor = self._read(ADDR["floor"], 1)[0]
                fname = self._read(ADDR["file"], 24).split(b"\0")[0].decode("ascii", "replace")
            except OSError as ex:
                self.pid = None
                return {"connected": False, "error": f"lectura fallida ({ex})"}
            m = re.search(r"room([0-9a-fA-F]{4})\.rdt", fname, re.I)
            room_id = m.group(1).upper() if m else f"{stage + 1:X}{room:02X}0"
            return {"connected": True, "room": room_id, "cut": cut, "x": x, "y": y, "z": z,
                    "floor": floor, "file": fname, "stage": stage, "room_index": room}


GAME = Game()


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=HERE, **kwargs)

    def do_GET(self):
        if self.path == "/":
            self.path = "/viewer.html"
        if self.path.startswith("/state"):
            body = json.dumps(GAME.state()).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path not in ("/viewer.html", "/data.json"):
            self.send_error(404)
            return
        super().do_GET()

    def log_message(self, *args):
        pass


def main():
    no_browser = "--no-browser" in sys.argv
    sys.argv = [a for a in sys.argv if a != "--no-browser"]
    data = os.path.join(HERE, "data.json")
    if not os.path.exists(data):
        if len(sys.argv) < 2:
            sys.exit('Falta data.json: ejecuta  python -I live.py "<carpeta de RE2>"')
        from build import build
        with open(data, "w", encoding="utf-8") as f:
            json.dump(build(sys.argv[1]), f, separators=(",", ":"))
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    url = f"http://127.0.0.1:{PORT}/"
    print(f"Visor en {url}  (Ctrl+C para salir)")
    print("Estado del juego:", GAME.state())
    if not no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
