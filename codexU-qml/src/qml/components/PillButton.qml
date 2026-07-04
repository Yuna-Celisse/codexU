import QtQuick
import codexu 1.0
import "../Theme.js" as Theme

Rectangle {
    id: root
    property alias text: label.text
    property bool active: false
    signal clicked()

    radius: height / 2
    border.width: 1

    // Active/pinned state uses blue tint; normal uses white translucent
    color: mouse.pressed
           ? (root.active ? Qt.rgba(59/255, 130/255, 246/255, 0.30) : Qt.rgba(1, 1, 1, 0.90))
           : mouse.containsMouse
             ? (root.active ? Qt.rgba(59/255, 130/255, 246/255, 0.22) : Qt.rgba(1, 1, 1, 0.74))
             : (root.active ? Qt.rgba(59/255, 130/255, 246/255, 0.14) : Qt.rgba(1, 1, 1, 0.55))

    border.color: root.active ? Qt.rgba(59/255, 130/255, 246/255, 0.35) : Qt.rgba(1, 1, 1, 0.50)

    Text {
        id: label
        anchors.centerIn: parent
        color: root.active ? Theme.brandBlue : Theme.textPrimary
        font.pixelSize: root.width <= 36 ? 15 : 13
        font.weight: Font.Bold
        horizontalAlignment: Text.AlignHCenter
        verticalAlignment: Text.AlignVCenter
        elide: Text.ElideRight
        maximumLineCount: 1
    }

    MouseArea {
        id: mouse
        anchors.fill: parent
        hoverEnabled: true
        onClicked: root.clicked()
        onPressed: mouse.accepted = true
    }
}
