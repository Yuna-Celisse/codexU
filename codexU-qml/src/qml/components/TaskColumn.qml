import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Rectangle {
    id: root
    radius: Theme.radiusTask
    color: bgColor
    border.color: Qt.rgba(accentColor.r, accentColor.g, accentColor.b, 0.16)
    border.width: 1
    clip: true

    property string colTitle: ""; property int colCount: 0
    property color accentColor: "#000"; property color bgColor: "#fff"
    property int taskKind: 0

    ColumnLayout {
        anchors.fill: parent; anchors.margins: 10; spacing: 6

        // Header
        Row {
            spacing: 6; Layout.fillWidth: true
            Rectangle { width:6; height:6; radius:3; color:accentColor; anchors.verticalCenter:parent.verticalCenter }
            Text { text:colTitle+"  "+colCount; font.pixelSize:10; font.weight:Font.Bold; color:Theme.textPrimary }
        }

        // Task cards in Flickable
        Flickable {
            Layout.fillWidth: true; Layout.fillHeight: true
            contentWidth: width; contentHeight: taskList.height
            clip: true; boundsBehavior: Flickable.StopAtBounds
            Column {
                id: taskList; width: parent.width; spacing: 6
                Repeater {
                    model: CodexDataProvider.taskList
                    delegate: TaskCard {
                        width: taskList.width; height: 62
                        visible: model.taskKind === root.taskKind
                    }
                }
            }
        }

        // Empty
        Text {
            text:"暂无"; font.pixelSize:12; font.weight:Font.Bold; color:Theme.textMuted
            visible: colCount===0
            Layout.fillWidth:true; Layout.fillHeight:true
            horizontalAlignment:Text.AlignHCenter; verticalAlignment:Text.AlignVCenter
        }
    }
}
