import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Rectangle {
    id: root
    radius: Theme.radiusSmall; color: Theme.cardFill
    border.color: Theme.cardBorder; border.width: 1

    property double pct: Math.min(1, Math.max(0, CodexDataProvider.monthEstimatedCost / 2000))

    Column {
        anchors.fill: parent
        anchors.leftMargin: 14; anchors.rightMargin: 14
        anchors.topMargin: 8; anchors.bottomMargin: 8

        // Header: "羊毛进度" left, "$X / $2.0K" right
        Item {
            width: parent.width; height: 22
            Text {
                id: titleText
                text: "羊毛进度"
                anchors.left: parent.left
                anchors.right: amountText.left
                anchors.rightMargin: 12
                anchors.verticalCenter: parent.verticalCenter
                font.pixelSize: 14; font.weight: Font.Bold; color: Theme.textSecondary
                elide: Text.ElideRight; maximumLineCount: 1
                verticalAlignment: Text.AlignVCenter
            }
            Text {
                id: amountText
                text: CodexDataProvider.valueProgressText
                anchors.right: parent.right
                anchors.verticalCenter: parent.verticalCenter
                width: Math.min(180, implicitWidth)
                font.pixelSize: 19; font.weight: Font.Bold; color: Theme.textPrimary
                horizontalAlignment: Text.AlignRight
                verticalAlignment: Text.AlignVCenter; maximumLineCount: 1
            }
        }

        Item { width: 1; height: 5 }

        // Progress bar
        Rectangle {
            width: parent.width; height: 7; radius: 4; color: Theme.trackFill
            Rectangle {
                anchors.left: parent.left; anchors.top: parent.top; anchors.bottom: parent.bottom
                width: Math.max(5, parent.width * root.pct)
                radius: 4; color: Theme.brandBlue
                gradient: Gradient {
                    GradientStop { position: 0.0; color: Theme.brandBlue }
                    GradientStop { position: 1.0; color: Theme.brandPurple }
                }
            }
        }

        Item { width: 1; height: 4 }

        // Tick row — 4 columns
        Row {
            width: parent.width; height: 22; spacing: 0
            Column {
                width: parent.width / 4; height: parent.height
                Rectangle { width: 5; height: 5; radius: 2; color: Theme.brandBlue; anchors.horizontalCenter: parent.horizontalCenter }
                Item { width: 1; height: 2 }
                Text { text: "Plus"; font.pixelSize: 10; font.weight: Font.Bold; color: Theme.textSecondary; anchors.horizontalCenter: parent.horizontalCenter }
            }
            Column {
                width: parent.width / 4; height: parent.height
                Rectangle { width: 5; height: 5; radius: 2; color: Theme.brandPurple; anchors.horizontalCenter: parent.horizontalCenter }
                Item { width: 1; height: 2 }
                Text { text: "Pro100"; font.pixelSize: 10; font.weight: Font.Bold; color: Theme.textSecondary; anchors.horizontalCenter: parent.horizontalCenter }
            }
            Column {
                width: parent.width / 4; height: parent.height
                Rectangle { width: 5; height: 5; radius: 2; color: Theme.brandBlue; anchors.horizontalCenter: parent.horizontalCenter }
                Item { width: 1; height: 2 }
                Text { text: "Pro200"; font.pixelSize: 10; font.weight: Font.Bold; color: Theme.textSecondary; anchors.horizontalCenter: parent.horizontalCenter }
            }
            Column {
                width: parent.width / 4; height: parent.height
                Rectangle { width: 5; height: 5; radius: 2; color: Theme.textWeak; anchors.horizontalCenter: parent.horizontalCenter }
                Item { width: 1; height: 2 }
                Text { text: "Max"; font.pixelSize: 10; font.weight: Font.Bold; color: Theme.textSecondary; anchors.horizontalCenter: parent.horizontalCenter }
            }
        }
    }
}
