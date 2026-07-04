import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

// Today's task board: title bar + 4 columns.
Item {
    id: root

    // ── Title bar ──
    RowLayout {
        id: titleBar
        anchors.left: parent.left
        anchors.right: parent.right
        height: 28

        Text {
            text: "今日任务看板"
            font.pixelSize: 20; font.weight: Font.Bold
            color: Theme.textPrimary
        }
        Item { Layout.fillWidth: true }
        Text {
            text: (CodexDataProvider.activeTasks + CodexDataProvider.pendingTasks
                   + CodexDataProvider.scheduledTasks + CodexDataProvider.doneTasks)
                  + " 事项 · " + CodexDataProvider.refreshedAt
            font.pixelSize: 12
            color: Theme.textSecondary
        }
    }

    // ── 4 columns ──
    RowLayout {
        anchors.top: titleBar.bottom
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        spacing: 10

        TaskColumn {
            Layout.fillWidth: true
            Layout.fillHeight: true
            colTitle: "进行中"
            colCount: CodexDataProvider.activeTasks
            accentColor: Theme.brandOrange
            bgColor: Theme.taskActiveBg
            taskKind: 0
        }
        TaskColumn {
            Layout.fillWidth: true
            Layout.fillHeight: true
            colTitle: "待处理"
            colCount: CodexDataProvider.pendingTasks
            accentColor: Theme.textWeak
            bgColor: Theme.taskPendingBg
            taskKind: 1
        }
        TaskColumn {
            Layout.fillWidth: true
            Layout.fillHeight: true
            colTitle: "定时"
            colCount: CodexDataProvider.scheduledTasks
            accentColor: Theme.brandPurple
            bgColor: Theme.taskScheduledBg
            taskKind: 2
        }
        TaskColumn {
            Layout.fillWidth: true
            Layout.fillHeight: true
            colTitle: "完成"
            colCount: CodexDataProvider.doneTasks
            accentColor: Theme.brandGreen
            bgColor: Theme.taskDoneBg
            taskKind: 3
        }
    }
}
