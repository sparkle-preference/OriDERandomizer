cmake_minimum_required(VERSION 3.20)

project(NativeWebSocket CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# oriDE.exe is 32-bit; build with -A Win32 and the x86-windows-static
# triplet so the CRT and all deps link into one self-contained dll.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

add_library(NativeWebSocket SHARED library.cpp)

find_package(ixwebsocket CONFIG REQUIRED)
target_link_libraries(NativeWebSocket PRIVATE ixwebsocket::ixwebsocket ws2_32 bcrypt)
target_include_directories(NativeWebSocket PRIVATE .)
