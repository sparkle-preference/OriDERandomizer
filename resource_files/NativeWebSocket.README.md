# NativeWebSocket.dll

The native websocket sidecar the randomizer's netcode uses (the game's
Mono can't speak modern TLS or websockets). Built from
[timoschwarzer/dotnet-native-websocket](https://github.com/timoschwarzer/dotnet-native-websocket)
(MIT) — our changes (thread safety, TLS CA support, connection counters,
text frames, http downloads) are PR'd upstream rather than forked here;
only the built binary lives in this repo.

It also carries `http_download` / `get_last_http_error`, an HTTPS GET
straight to a file over the same mbedtls stack, which is how the updater
fetches the version and the new assembly. The body never crosses the
interop boundary, and the call blocks, so it runs on its own thread.
`randomizer/NativeWebSocket.cs` binds those two *optionally*: a wrapper
paired with an older dll loses the updater but keeps its socket.

Ships inside Assembly-CSharp.dll as an embedded resource named exactly
`NativeWebSocket.dll`, alongside `cacert.pem` (the Mozilla CA bundle —
mbedtls can't read the Windows cert store). `randomizer/NativeWebSocket.cs`
extracts both next to oriDE.exe at runtime and binds the exports by hand
(GetProcAddress — the game's Mono can't [DllImport]-resolve a dll extracted
mid-run). The dnSpy crack must re-embed both resources whenever they change.

## Rebuilding

Needs MSVC (x86 — oriDE.exe is 32-bit), CMake ≥3.28, and vcpkg (full
clone, not shallow — the manifest's version constraints need the git
history). From the dotnet-native-websocket checkout:

```
cmake -S . -B build -G "Visual Studio 17 2022" -A Win32 ^
  -DCMAKE_TOOLCHAIN_FILE=<vcpkg>/scripts/buildsystems/vcpkg.cmake ^
  -DVCPKG_TARGET_TRIPLET=x86-windows-static ^
  -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
cmake --build build --config Release
```

Output: `build/bin/Release/NativeWebSocket.dll`, self-contained (static
CRT, static ixwebsocket + mbedtls). Copy it here, re-embed via dnSpy.
