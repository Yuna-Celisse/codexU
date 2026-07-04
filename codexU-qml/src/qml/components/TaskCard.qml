import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Rectangle {
    id: root
    radius: 10
    color: Theme.taskCardBg
    border.color: Theme.taskCardBorder
    border.width: 1
    visible: true

    Column {
        anchors.fill: parent; anchors.margins: 8; spacing: 3

        // Code + age
        RowLayout {
            width: parent.width
            Text { text:taskCode||"COD-0000"; font.pixelSize:11; font.weight:Font.Bold; color:Theme.textSecondary }
            Item { Layout.fillWidth:true }
            Text { text:taskAge||"--"; font.pixelSize:10; color:Theme.textMuted }
        }

        // Title
        Text {
            width: parent.width
            text: taskTitle||"Codex 会话"
            font.pixelSize:12; font.weight:Font.Bold; color:Theme.textPrimary
            elide: Text.ElideRight; maximumLineCount: 1
        }

        // Chip
        Rectangle {
            radius:7; height:17; width:chipText.implicitWidth+14
            color: Qt.rgba(245/255, 158/255, 11/255, 0.14)
            Text {
                id:chipText; anchors.centerIn:parent
                text:taskChip||"Active"; font.pixelSize:10; font.weight:Font.Bold
                color: Theme.brandOrange
            }
        }
    }
}
