#pragma once

#include <QtCore/QObject>
#include <QtGui/QWindow>
#include <functional>

// Platform-specific window utilities (Win32 DWM shadow, hotkey, backdrop).
class WindowHelper {
public:
    // Add DWM shadow to a frameless window (Windows 10/11)
    static void enableDwmShadow(QWindow *window);

    // Register Ctrl+Alt+U global hotkey
    static bool registerHotKey(QWindow *window, std::function<void()> callback);
    static void unregisterHotKey(QWindow *window);

    // Prevent click-to-activate (WS_EX_NOACTIVATE on Windows)
    static void setNoActivate(QWindow *window, bool enabled);
};
