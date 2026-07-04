#include <QtCore/QCommandLineParser>
#include <QtCore/QDebug>
#include <QtCore/QJsonDocument>
#include <QtCore/QJsonObject>
#include <QtCore/QJsonArray>
#include <QtWidgets/QApplication>
#include <QtQml/QQmlApplicationEngine>
#include <QtQml/QQmlContext>
#include <QtQml/qqml.h>
#include <QtWidgets/QSystemTrayIcon>
#include <QtWidgets/QMenu>

#include "app/CodexDataProvider.h"
#include "app/FormatUtils.h"
#include "app/MockDataSource.h"
#include "platform/WindowHelper.h"

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    app.setApplicationName("codexU");
    app.setApplicationVersion("0.3.0");
    app.setQuitOnLastWindowClosed(false);

    // ── CLI flags ──
    QCommandLineParser parser;
    QCommandLineOption dumpOpt("dump-json", "Dump current snapshot as JSON to stdout");
    QCommandLineOption mockOpt("mock", "Use mock/demo data (development only)");
    parser.addOption(dumpOpt);
    parser.addOption(mockOpt);
    parser.process(app);

    bool useMock = parser.isSet(mockOpt)
                   || qEnvironmentVariableIntValue("CODEXU_USE_MOCK") == 1;

    // ── --dump-json mode ──
    if (parser.isSet(dumpOpt)) {
        UsageSnapshot snap;
        if (useMock) {
            MockDataSource ds;
            snap = ds.load();
        } else {
            // Null data when no mock
            snap.messages.append("无真实数据源");
        }

        QJsonObject root;
        root["accountPlan"] = snap.accountPlan;
        root["primaryRemainingPercent"] = snap.primaryRemainingPercent;
        root["secondaryRemainingPercent"] = snap.secondaryRemainingPercent;
        root["todayTokens"] = snap.todayTokens;
        root["sevenDayTokens"] = snap.sevenDayTokens;
        root["lifetimeTokens"] = snap.lifetimeTokens;
        root["monthEstimatedCost"] = snap.monthEstimatedCost;
        root["messages"] = QJsonArray::fromStringList(snap.messages);

        QJsonDocument doc(root);
        printf("%s\n", doc.toJson(QJsonDocument::Compact).constData());
        return 0;
    }

    // ── Register C++ singletons for QML ──
    CodexDataProvider *dataProvider = nullptr;
    qmlRegisterSingletonType<CodexDataProvider>("codexu", 1, 0, "CodexDataProvider",
        [&dataProvider, useMock](QQmlEngine *, QJSEngine *) -> QObject * {
            dataProvider = new CodexDataProvider(useMock);
            return dataProvider;
        });
    qmlRegisterSingletonType<FormatUtils>("codexu", 1, 0, "FormatUtils",
        [](QQmlEngine *, QJSEngine *) -> QObject * {
            return new FormatUtils;
        });

    // ── Load QML ──
    QQmlApplicationEngine engine;
    engine.loadFromModule("codexu", "Main");

    if (engine.rootObjects().isEmpty()) {
        qWarning() << "Failed to load QML module 'codexu'";
        return 1;
    }

    QWindow *window = qobject_cast<QWindow *>(engine.rootObjects().first());
    if (!window) {
        qWarning() << "Root QML object is not a Window";
        return 1;
    }

    WindowHelper::enableDwmShadow(window);

    // Apply WS_EX_NOACTIVATE from QML onWindowPinnedChanged
    // We watch the window's flags — when WindowDoesNotAcceptFocus is added/removed,
    // also apply the native Windows style for reliability.
    QObject::connect(window, &QWindow::flagsChanged, [window]() {
        bool noFocus = window->flags().testFlag(Qt::WindowDoesNotAcceptFocus);
        WindowHelper::setNoActivate(window, noFocus);
    });

    // ── System tray icon ──
    QSystemTrayIcon *tray = new QSystemTrayIcon(&app);
    tray->setIcon(QIcon(":/codexU-icon.png"));
    tray->setToolTip("codexU");

    QMenu *trayMenu = new QMenu;
    trayMenu->addAction("显示/隐藏", [window]() {
        window->setVisible(!window->isVisible());
    });
    trayMenu->addAction("刷新", [dataProvider]() {
        if (dataProvider) dataProvider->refresh();
    });
    trayMenu->addAction("退出", &app, &QCoreApplication::quit);
    tray->setContextMenu(trayMenu);
    tray->show();

    WindowHelper::registerHotKey(window, [window]() {
        window->setVisible(!window->isVisible());
    });

    QObject::connect(tray, &QSystemTrayIcon::activated, [window](QSystemTrayIcon::ActivationReason reason) {
        if (reason == QSystemTrayIcon::DoubleClick)
            window->setVisible(!window->isVisible());
    });

    return app.exec();
}
