"""Builds a slice of the client into a bridge exe (bridge/Bridge.cs) and calls into it from Python."""
import atexit
import base64
import collections
import hashlib
import os
import queue
import shutil
import struct
import subprocess
import threading
import json

HERE = os.path.dirname(os.path.abspath(__file__))
CLIENT = os.environ.get("ORI_CLIENT_REPO", os.path.dirname(HERE))
SDKS = r"C:\Program Files\dotnet\sdk"


def newest_csc():
    """The newest installed SDK's Roslyn, as typecheck.ps1 picks it."""
    found = []
    for name in os.listdir(SDKS) if os.path.isdir(SDKS) else []:
        path = os.path.join(SDKS, name, "Roslyn", "bincore", "csc.dll")
        if os.path.exists(path):
            found.append((tuple(int(p) for p in name.split("-")[0].split(".") if p.isdigit()), path))
    return max(found)[1] if found else None


CSC = os.environ.get("ORI_CSC") or newest_csc()
FX =r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
OUT = os.path.join(os.environ.get("TEMP", HERE), "client_props")
FX_REFS = ("mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll")
NOWARN = "CS0436,CS1701,CS0162,CS0414,CS0649,CS0169,CS0168,CS0219,CS0618,CS0109,CS0108,CS0114"


class BuildError(Exception):
    pass


class CsError(Exception):
    """The C# side threw; .type is the exception's full name."""

    def __init__(self, reply):
        self.type = reply["err"]
        self.msg = reply.get("msg", "")
        self.at = reply.get("at", [])
        self.inner = reply.get("inner")
        super().__init__("%s: %s%s\n  %s" % (self.type, self.msg, " (" + self.inner + ")" if self.inner else "",
                                              "\n  ".join(self.at[:3])))


class BridgeDied(Exception):
    """The process crashed (a stack overflow can't be caught) or hung past its timeout."""


class Handle:
    def __init__(self, bridge, hid, type_name):
        self.bridge, self.id, self.type, self.gen = bridge, hid, type_name, bridge.gen

    def call(self, method, *args, **kw):
        return self.bridge.call(method, *args, on=self, **kw)

    def get(self, member):
        return self.bridge.get(member, on=self)

    def set(self, member, value):
        return self.bridge.set(member, value, on=self)

    def __repr__(self):
        return "<%s #%d>" % (self.type, self.id)


def _bits(x):
    return format(struct.unpack("<q", struct.pack("<d", x))[0] & 0xFFFFFFFFFFFFFFFF, "016x")


def _enc(v):
    if isinstance(v, bool) or v is None or isinstance(v, (int, str)):
        return v
    if isinstance(v, float):
        return {"$d64": _bits(v)}
    if isinstance(v, (bytes, bytearray)):
        return {"$b": base64.b64encode(bytes(v)).decode("ascii")}
    if isinstance(v, Handle):
        return {"$h": v.id}
    if isinstance(v, dict):
        return {"$map": {str(k): _enc(x) for k, x in v.items()}}
    if isinstance(v, (list, tuple)):
        return [_enc(x) for x in v]
    raise TypeError("can't send %r to the bridge" % (v,))


def _hash(paths, extra):
    h = hashlib.sha256(extra.encode())
    for p in paths:
        h.update(p.encode())
        with open(p, "rb") as f:
            h.update(f.read())
    return h.hexdigest()[:12]


def build(name, sources, extra=(), game=False, refs=()):
    """Compiles Bridge.cs, the slice's own files (stubs, glue) and client sources; cached by content."""
    files = [os.path.join(HERE, "bridge", "Bridge.cs")] + [os.path.abspath(p) for p in extra]
    files += [os.path.join(CLIENT, s) for s in sources]
    missing = [p for p in files if not os.path.exists(p)]
    if missing:
        raise BuildError("missing sources: %s" % missing)
    if not CSC:
        raise BuildError("no Roslyn under %s; install a .NET SDK or set ORI_CSC" % SDKS)
    dlls =[os.path.join(FX, r) for r in FX_REFS + tuple(refs)] + [os.path.join(CLIENT, "Managed", "UnityEngine.dll")]
    if game:
        dlls += [os.path.join(CLIENT, "Assembly-CSharp.dll"), os.path.join(CLIENT, "Managed", "Assembly-CSharp-firstpass.dll")]
    key = _hash(files + dlls, CSC + NOWARN)
    folder = os.path.join(OUT, "%s-%s" % (name, key))
    exe = os.path.join(folder, name + ".exe")
    if os.path.exists(exe):
        return exe
    os.makedirs(folder, exist_ok=True)
    args = ["dotnet", CSC, "-nologo", "-noconfig", "-nostdlib", "-langversion:latest", "-t:exe", "-debug:portable",
            "-nowarn:" + NOWARN, "-out:" + exe] + ["-r:" + d for d in dlls] + files
    res = subprocess.run(args, capture_output=True, text=True)
    if res.returncode != 0:
        shutil.rmtree(folder, ignore_errors=True)
        errors = [l for l in res.stdout.splitlines() if ": error " in l]
        raise BuildError("%s failed to build:\n%s" % (name, "\n".join(errors[:40]) or res.stdout[-4000:]))
    for d in dlls[len(FX_REFS) + len(refs):]:
        shutil.copy(d, folder)
    return exe


class Bridge:
    """One long-lived bridge process; restarts after a crash or hang (old handles go stale)."""

    def __init__(self, exe, timeout=20):
        self.exe, self.timeout, self.proc, self.gen = exe, timeout, None, 0
        self.stderr = collections.deque(maxlen=200)

    def _start(self):
        self.gen += 1
        self.proc = subprocess.Popen([self.exe], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                     cwd=os.path.dirname(self.exe))
        self.lines = queue.Queue()
        threading.Thread(target=self._pump, args=(self.proc.stdout, self.lines), daemon=True).start()
        threading.Thread(target=self._drain, args=(self.proc.stderr,), daemon=True).start()
        atexit.register(self._stop, self.proc)

    @staticmethod
    def _pump(stream, lines):
        for line in iter(stream.readline, b""):
            lines.put(line)
        lines.put(None)

    def _drain(self, stream):
        for line in iter(stream.readline, b""):
            self.stderr.append(line.decode("utf-8", "replace").rstrip())

    @staticmethod
    def _stop(proc):
        if proc.poll() is None:
            proc.kill()
        proc.communicate()

    def restart(self):
        if self.proc is not None:
            self._stop(self.proc)
        self.proc = None

    def _dec(self, v):
        if isinstance(v, dict):
            if "$map" in v:
                return {k: self._dec(x) for k, x in v["$map"].items()}
            if "$d64" in v:
                return struct.unpack("<d", struct.pack("<Q", int(v["$d64"], 16)))[0]
            if "$b" in v:
                return base64.b64decode(v["$b"])
            if "$h" in v:
                return Handle(self, v["$h"], v.get("type"))
            return {k: self._dec(x) for k, x in v.items()}
        if isinstance(v, list):
            return [self._dec(x) for x in v]
        return v

    def send(self, req, timeout=None):
        if self.proc is None or self.proc.poll() is not None:
            self._start()
        on = req.get("on")
        if isinstance(on, Handle):
            if on.gen != self.gen:
                raise BridgeDied("handle %r is from a bridge that has since restarted" % (on,))
            req["on"] = on.id
        wire = dict(req, args=_enc(req.get("args", [])))
        if "value" in req:
            wire["value"] = _enc(req["value"])
        self.proc.stdin.write((json.dumps(wire) + "\n").encode("ascii"))
        self.proc.stdin.flush()
        try:
            line = self.lines.get(timeout=timeout or self.timeout)
        except queue.Empty:
            self.restart()
            raise BridgeDied("hung past %ss on %s" % (timeout or self.timeout, json.dumps(wire)[:400]))
        if line is None:
            code = self.proc.wait()
            tail = "\n".join(list(self.stderr)[-8:])
            self.proc = None
            raise BridgeDied("exited %s on %s\n%s" % (code, json.dumps(wire)[:400], tail))
        reply = json.loads(line)
        if "err" in reply:
            raise CsError(reply)
        return reply

    def call(self, path, *args, on=None, keep=False, timeout=None):
        """path(*args); a method with out/ref params returns (ret, [outs])."""
        reply = self.send({"call": path, "args": list(args), "on": on, "keep": keep}, timeout)
        ok = self._dec(reply["ok"])
        return (ok, self._dec(reply["out"])) if "out" in reply else ok

    def new(self, type_name, *args):
        return self.call(type_name + ".new", *args, keep=True)

    def keep(self, path, *args, on=None):
        return self.call(path, *args, on=on, keep=True)

    def get(self, path, on=None):
        return self._dec(self.send({"get": path, "on": on})["ok"])

    def set(self, path, value, on=None):
        self.send({"set": path, "value": value, "on": on})

    def culture(self, name):
        self.call("$culture", name)


def outcome(fn, *args, **kw):
    """("ok", value) or ("err", exception type) for a call: the shape totality properties compare."""
    try:
        return ("ok", fn(*args, **kw))
    except CsError as e:
        return ("err", e.type)


_bridges = {}


def slice_bridge(name, sources, extra=(), game=False, refs=(), timeout=20):
    """The shared bridge for a slice, built on first use."""
    if name not in _bridges:
        _bridges[name] = Bridge(build(name, sources, extra, game, refs), timeout)
    return _bridges[name]
