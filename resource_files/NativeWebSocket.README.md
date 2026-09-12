# NativeWebSocket.dll

The native websocket sidecar the randomizer's netcode uses (the game's
Mono can't speak modern TLS or websockets). Our fork of
[timoschwarzer/dotnet-native-websocket](https://github.com/timoschwarzer/dotnet-native-websocket)
(MIT) lives in the `dotnet-native-websocket` checkout next to this repo;
only the built binary lives here.

It also carries `http_download` / `get_last_http_error`, an HTTPS GET
straight to a file over the same mbedtls stack, which is how the updater
fetches the version and the new assembly. The body never crosses the
interop boundary, and the call blocks, so it runs on its own thread.
`randomizer/NativeWebSocket.cs` binds those two *optionally*: a wrapper
paired with an older dll loses the updater but keeps its socket.

## Data channels (`rtc_*`)

WebRTC data channels, for ghost multiplayer, over libdatachannel. Same
shape as the websocket half: libdatachannel calls back on its own threads,
everything lands in a queue behind a mutex, and the managed side polls.
Nothing calls into managed code — which is the entire reason this lives in
the sidecar rather than in the mod.

Signalling is **non-trickle**. Create a peer, poll `rtc_local_ready`, send
the one complete SDP (candidates already in it) through the website, feed
back whatever the far side answers. Trickle would save a second of setup
and cost a candidate-ordering protocol on the website; it can come later
behind the same exports.

Bound *optionally*, like the http exports: a mod newer than the extracted
dll loses ghost multiplayer and keeps everything else.

Two things to know before touching this:

- **One TLS stack, mbedtls, and it is shared.** A vcpkg port overlay builds
  libdatachannel against mbedtls, and mbedtls 3.6 keeps one process-wide
  PSA crypto core behind every TLS 1.3 handshake. Two things keep that
  safe with sockets on several threads, and both live in the sidecar's
  build: ixwebsocket is patched not to free the core on socket close, and
  mbedtls is built with its `pthreads` feature so the core is locked.
  Without either, two TLS sockets on two threads can crash the game.
- **We are a fork now, and that is fine** (Lapis, 2026-08-29). zre made the
  original for us and it has morphed into something else; merging upstream
  is a conversation to have eventually, not a constraint on what goes in.

Verified in-game by a loopback self-test — two peers in one process, real
offer/answer, real DTLS and SCTP: channel open in 0.05s, a 9-byte packet
back byte-identical in 0.07s. It runs with Dev on, the first time a ghost
is spawned, and needs no network. See `RandomizerGhostNet`.

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
  -DVCPKG_OVERLAY_PORTS=<checkout>/vcpkg-overlays
cmake --build build --config Release
```

Output: `build/bin/Release/NativeWebSocket.dll` and `.sidecar_ver`,
self-contained (static CRT, static ixwebsocket + mbedtls + libdatachannel).
Copy both here, re-embed via dnSpy. The build stamps the dll's version
resource with "<version>+<git commit>" and writes the same string to
`.sidecar_ver`; `NativeWebSocket.Load` reads the extracted dll's stamp and
rewrites the dll only when it differs from the embedded one, so no player
reads two MB at launch and no rebuild is mistaken for the last.
