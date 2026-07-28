# NativeWebSocket sidecar

Native (C++) websocket dll the randomizer's netcode P/Invokes, because the
game's Mono (v2.0.50727, .NET 3.5-era) can't speak modern TLS or websockets.
Forked from [timoschwarzer/dotnet-native-websocket](https://github.com/timoschwarzer/dotnet-native-websocket)
(MIT). Wraps [IXWebSocket](https://github.com/machinezone/IXWebSocket) with
mbedtls.

Changes from upstream: mutex around the message queue (the receive thread
and game thread race on it upstream), callback registered before `start()`,
TLS CA file + ping interval + reconnect exports, text frames, error/open/close
counters for the managed side's HTTP-fallback logic.

The dll ships *inside* Assembly-CSharp.dll as an embedded resource; the
managed side extracts it (and the CA bundle mbedtls needs — it does not read
the Windows cert store) next to `oriDE.exe` and LoadLibrary's it by full path
before the first P/Invoke.

## Building

Needs MSVC (x86 — the game exe is 32-bit), CMake, and vcpkg.

```
cmake -S . -B build -G "Visual Studio 17 2022" -A Win32 ^
  -DCMAKE_TOOLCHAIN_FILE=<vcpkg>/scripts/buildsystems/vcpkg.cmake ^
  -DVCPKG_TARGET_TRIPLET=x86-windows-static
cmake --build build --config Release
```

Output: `build/Release/NativeWebSocket.dll`, self-contained (static CRT,
static ixwebsocket + mbedtls).
