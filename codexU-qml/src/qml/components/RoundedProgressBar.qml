import QtQuick
import codexu 1.0
import "../Theme.js" as Theme

// Simple rounded progress bar: track + gradient fill.
Item {
    id: root
    property double percent: 0.4          // 0..1
    property color fillColor: Theme.brandBlue

    implicitHeight: 7

    // Track
    Rectangle {
        anchors.fill: parent
        radius: parent.height / 2
        color: Theme.trackFill
    }

    // Fill
    Rectangle {
        anchors.left: parent.left
        anchors.top: parent.top
        anchors.bottom: parent.bottom
        width: Math.max(parent.height, parent.width * Math.min(1, Math.max(0, root.percent)))
        radius: parent.height / 2
        color: root.fillColor
    }
}
