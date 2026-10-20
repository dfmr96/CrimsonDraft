"""Minimal read-only GDB remote-protocol client for DuckStation's GDB server.

Only the 'm' (read memory) packet is used: nothing is ever written to the emulator.
"""
import socket


class GdbMem:
    def __init__(self, host="127.0.0.1", port=2345, timeout=2.0):
        self.sock = socket.create_connection((host, port), timeout=timeout)
        self.buf = b""

    def close(self):
        self.sock.close()

    def _send(self, payload):
        cs = sum(payload.encode()) & 0xFF
        self.sock.sendall(f"${payload}#{cs:02x}".encode())

    def _recv_packet(self):
        while True:
            start = self.buf.find(b"$")
            end = self.buf.find(b"#", start + 1) if start != -1 else -1
            if start != -1 and end != -1 and len(self.buf) >= end + 3:
                body = self.buf[start + 1:end]
                self.buf = self.buf[end + 3:]
                self.sock.sendall(b"+")
                return body.decode()
            chunk = self.sock.recv(65536)
            if not chunk:
                raise ConnectionError("GDB server closed the connection")
            self.buf += chunk

    def read(self, addr, length):
        out = bytearray()
        while length > 0:
            n = min(length, 0x800)
            self._send(f"m{addr:x},{n:x}")
            reply = self._recv_packet()
            if reply.startswith("E") or not reply:
                raise IOError(f"read 0x{addr:X} failed: {reply}")
            out += bytes.fromhex(reply)
            addr += n
            length -= n
        return bytes(out)
