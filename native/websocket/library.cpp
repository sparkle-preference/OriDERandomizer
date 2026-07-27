// Native websocket sidecar for the randomizer netcode. Forked from
// timoschwarzer/dotnet-native-websocket (MIT); wraps IXWebSocket + mbedtls
// so the game's ancient Mono can speak wss://.
//
// Threading contract: the game thread calls every export. IXWebSocket's
// receive thread only runs the message callback; everything both threads
// touch is guarded by g_lock. The pointer returned by get_pending_message
// stays valid until pop_pending_message — only the game thread pops, and
// deque pushes never move the front element.

#include <mutex>
#include <queue>
#include <string>

#include <ixwebsocket/IXNetSystem.h>
#include <ixwebsocket/IXWebSocket.h>

#include "macros.h"

namespace {
ix::WebSocket g_socket;
std::mutex g_lock;
std::queue<std::string> g_messages;
std::string g_last_error;
int g_error_count = 0;
int g_open_count = 0;
int g_close_count = 0;
}  // namespace

C_DLLEXPORT void initialize_network() { ix::initNetSystem(); }

C_DLLEXPORT void finalize_network() { ix::uninitNetSystem(); }

C_DLLEXPORT void set_url(const char* url) { g_socket.setUrl(url); }

// mbedtls does not read the Windows cert store; wss:// needs a CA bundle
// on disk (the managed side extracts one next to this dll).
C_DLLEXPORT void set_ca_file(const char* path) {
    ix::SocketTLSOptions tls;
    tls.caFile = path;
    g_socket.setTLSOptions(tls);
}

C_DLLEXPORT void set_ping_interval(int seconds) { g_socket.setPingInterval(seconds); }

C_DLLEXPORT void set_auto_reconnect(bool enabled) {
    if (enabled)
        g_socket.enableAutomaticReconnection();
    else
        g_socket.disableAutomaticReconnection();
}

C_DLLEXPORT void start() {
    g_socket.setOnMessageCallback([](const ix::WebSocketMessagePtr& msg) {
        std::lock_guard<std::mutex> hold(g_lock);
        switch (msg->type) {
            case ix::WebSocketMessageType::Message:
                g_messages.emplace(msg->str);
                break;
            case ix::WebSocketMessageType::Open:
                g_open_count++;
                break;
            case ix::WebSocketMessageType::Error:
                g_error_count++;
                g_last_error = msg->errorInfo.reason;
                break;
            case ix::WebSocketMessageType::Close:
                g_close_count++;
                if (!msg->closeInfo.reason.empty())
                    g_last_error = "closed: " + msg->closeInfo.reason;
                break;
            default:
                break;
        }
    });
    g_socket.start();
}

C_DLLEXPORT void stop() { g_socket.stop(); }

C_DLLEXPORT void send_text(const char* data) { g_socket.sendText(data); }

C_DLLEXPORT void send_binary(const char* data, int length) {
    g_socket.sendBinary(std::string(data, length));
}

C_DLLEXPORT int get_state() { return static_cast<int>(g_socket.getReadyState()); }

C_DLLEXPORT int get_open_count() {
    std::lock_guard<std::mutex> hold(g_lock);
    return g_open_count;
}

C_DLLEXPORT int get_error_count() {
    std::lock_guard<std::mutex> hold(g_lock);
    return g_error_count;
}

C_DLLEXPORT int get_close_count() {
    std::lock_guard<std::mutex> hold(g_lock);
    return g_close_count;
}

C_DLLEXPORT int get_last_error(char* buf, int buflen) {
    std::lock_guard<std::mutex> hold(g_lock);
    if (buf == nullptr || buflen <= 0) return static_cast<int>(g_last_error.size());
    int n = static_cast<int>(g_last_error.copy(buf, buflen - 1));
    buf[n] = '\0';
    return n;
}

C_DLLEXPORT bool has_pending_message() {
    std::lock_guard<std::mutex> hold(g_lock);
    return !g_messages.empty();
}

C_DLLEXPORT const char* get_pending_message(int* length) {
    std::lock_guard<std::mutex> hold(g_lock);
    if (g_messages.empty()) {
        *length = 0;
        return nullptr;
    }
    const std::string& front = g_messages.front();
    *length = static_cast<int>(front.size());
    return front.c_str();
}

C_DLLEXPORT void pop_pending_message() {
    std::lock_guard<std::mutex> hold(g_lock);
    if (!g_messages.empty()) g_messages.pop();
}
