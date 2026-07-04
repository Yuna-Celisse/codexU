#include "WindowHelper.h"

#ifdef Q_OS_WIN
#include <Windows.h>
#include <dwmapi.h>

static int g_hotkeyId = 1;
static std::function<void()> g_hotkeyCallback;

void WindowHelper::enableDwmShadow(QWindow *window)
{
    if (!window) return;
    HWND hwnd = reinterpret_cast<HWND>(window->winId());

    // Use DWM to extend the frame into the client area — this restores
    // the native window shadow on frameless windows.
    MARGINS margins = {1, 1, 1, 1};
    DwmExtendFrameIntoClientArea(hwnd, &margins);

    // Optional: enable dark mode title bar / immersive backdrop
    BOOL useDarkMode = TRUE;
    DwmSetWindowAttribute(hwnd, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/,
                          &useDarkMode, sizeof(useDarkMode));
}

// Minimal Win32 window proc hook for hotkey messages
static WNDPROC g_oldWndProc = nullptr;

static LRESULT CALLBACK HotKeyWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    if (msg == WM_HOTKEY && wp == g_hotkeyId) {
        if (g_hotkeyCallback) g_hotkeyCallback();
        return 0;
    }
    return CallWindowProc(g_oldWndProc, hwnd, msg, wp, lp);
}

bool WindowHelper::registerHotKey(QWindow *window, std::function<void()> callback)
{
    if (!window) return false;
    HWND hwnd = reinterpret_cast<HWND>(window->winId());
    g_hotkeyCallback = callback;

    // Subclass the window to receive WM_HOTKEY
    g_oldWndProc = reinterpret_cast<WNDPROC>(
        SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                         reinterpret_cast<LONG_PTR>(HotKeyWndProc)));

    return RegisterHotKey(hwnd, g_hotkeyId,
                          MOD_CONTROL | MOD_ALT, 'U');
}

void WindowHelper::unregisterHotKey(QWindow *window)
{
    if (!window) return;
    HWND hwnd = reinterpret_cast<HWND>(window->winId());
    UnregisterHotKey(hwnd, g_hotkeyId);
}

void WindowHelper::setNoActivate(QWindow *window, bool enabled)
{
    if (!window) return;
    HWND hwnd = reinterpret_cast<HWND>(window->winId());

    LONG_PTR exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
    if (enabled)
        exStyle |= WS_EX_NOACTIVATE;
    else
        exStyle &= ~WS_EX_NOACTIVATE;
    SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);

    // Apply without changing z-order or position
    SetWindowPos(hwnd, nullptr, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER |
                 SWP_FRAMECHANGED | SWP_NOACTIVATE);
}

#else // non-Windows stubs

void WindowHelper::enableDwmShadow(QWindow *) {}
bool WindowHelper::registerHotKey(QWindow *, std::function<void()>) { return false; }
void WindowHelper::unregisterHotKey(QWindow *) {}
void WindowHelper::setNoActivate(QWindow *, bool) {}

#endif
