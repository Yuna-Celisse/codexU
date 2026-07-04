import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Item {
    id: root
    implicitHeight: 58

    property bool windowPinned: false
    signal togglePin()

    // ── Left brand area ──
    Row {
        id: brandRow
        anchors.left: parent.left
        anchors.verticalCenter: parent.verticalCenter
        spacing: 14

        Rectangle {
            width: 40; height: 40; radius: 10
            gradient: Gradient {
                GradientStop { position: 0.0; color: "#5b8cff" }
                GradientStop { position: 1.0; color: "#8b5cf6" }
            }
            Rectangle { width: 14; height: 14; radius: 7; x: 7; y: 7; color: Qt.rgba(1,1,1,0.35) }
        }

        Column {
            spacing: 0
            Text {
                text: "codexU"
                font.pixelSize: 28; font.weight: Font.ExtraBold; color: Theme.textPrimary
            }
            Text {
                text: "刷新 " + CodexDataProvider.refreshedAt
                font.pixelSize: 11; color: Theme.textMuted
            }
        }
    }

    // ── Right action buttons: lang(76) + plan(58) + pin(34) + refresh(34) + close(34) ──
    Row {
        id: actionsRow
        anchors.right: parent.right
        anchors.verticalCenter: parent.verticalCenter
        spacing: 8

        PillButton { width: 76; height: 34; text: "中 / EN" }
        PillButton { width: 58; height: 34; text: CodexDataProvider.accountPlan }
        PillButton {
            width: 34; height: 34
            text: root.windowPinned ? "锁" : "固"
            active: root.windowPinned
            onClicked: root.togglePin()
        }
        PillButton {
            width: 34; height: 34; text: "↻"
            onClicked: CodexDataProvider.refresh()
        }
        PillButton {
            width: 34; height: 34; text: "×"
            onClicked: Qt.quit()
        }
    }
}
