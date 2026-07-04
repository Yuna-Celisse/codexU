cmake_minimum_required(VERSION 3.16)
project(codexU-qml VERSION 0.3.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)

find_package(Qt6 6.5 REQUIRED COMPONENTS Quick Qml Sql Network Widgets)

# ── Source files ──────────────────────────────────────────────
set(SOURCES
    src/main.cpp
    src/app/CodexDataProvider.cpp
    src/app/TaskListModel.cpp
    src/app/FormatUtils.cpp
    src/app/MockDataSource.cpp
    src/app/RealDataSource.cpp
    src/platform/WindowHelper.cpp
)

set(HEADERS
    src/app/Models.h
    src/app/DataSourceInterface.h
    src/app/CodexDataProvider.h
    src/app/TaskListModel.h
    src/app/FormatUtils.h
    src/app/MockDataSource.h
    src/app/RealDataSource.h
    src/platform/WindowHelper.h
)

qt_add_executable(codexU-qml ${SOURCES} ${HEADERS})

# ── QML module ────────────────────────────────────────────────
qt_add_qml_module(codexU-qml
    URI codexu
    VERSION 1.0
    NO_RESOURCE_TARGET_PATH
    QML_FILES
        src/qml/Main.qml
        src/qml/Theme.js
        src/qml/components/HeaderBar.qml
        src/qml/components/PillButton.qml
        src/qml/components/QuotaRings.qml
        src/qml/components/TokenCard.qml
        src/qml/components/TokenSummaryCards.qml
        src/qml/components/ValueProgressCard.qml
        src/qml/components/TaskBoard.qml
        src/qml/components/TaskColumn.qml
        src/qml/components/TaskCard.qml
        src/qml/components/StatusFooter.qml
        src/qml/components/GlassCard.qml
        src/qml/components/RoundedProgressBar.qml
)

target_link_libraries(codexU-qml PRIVATE
    Qt6::Quick
    Qt6::Qml
    Qt6::Sql
    Qt6::Network
    Qt6::Widgets
    dwmapi
)

qt_import_qml_plugins(codexU-qml)

install(TARGETS codexU-qml
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)
