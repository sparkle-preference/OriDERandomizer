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

- **libdatachannel needs OpenSSL**, and the vcpkg port exposes no mbedtls
  option, so the dll now links both TLS stacks. That is most of its size.
  If it ever matters, upstream libdatachannel does have `USE_MBEDTLS` and
  a port overlay could reach it.
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
  -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
cmake --build build --config Release
```

Output: `build/bin/Release/NativeWebSocket.dll`, self-contained (static
CRT, static ixwebsocket + mbedtls). Copy it here, re-embed via dnSpy.
