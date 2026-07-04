import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import codexu 1.0
import "Theme.js" as Theme

Window {
    id: mainWindow
    width: 720; height: 620
    visible: true
    flags: Qt.FramelessWindowHint | Qt.Window
    color: "transparent"
    minimumWidth: 720; minimumHeight: 620
    maximumWidth: 720; maximumHeight: 620

    property bool windowPinned: false

    function applyWindowFlags() {
        var f = Qt.FramelessWindowHint | Qt.Window
        if (windowPinned) {
            f = f | Qt.WindowDoesNotAcceptFocus
        }
        mainWindow.flags = f
    }

    onWindowPinnedChanged: applyWindowFlags()

    Rectangle {
        id: appShell
        anchors.fill: parent
        radius: Theme.windowRadius
        gradient: Gradient {
            GradientStop { position:0.0; color:Qt.rgba(210/255,235/255,242/255,0.985) }
            GradientStop { position:1.0; color:Qt.rgba(175/255,212/255,226/255,0.985) }
        }
        border.color: Theme.windowBorder
        border.width: 1
        clip: true

        DragHandler { onActiveChanged: if(active && !mainWindow.windowPinned) mainWindow.startSystemMove() }

        Rectangle { width:160; height:140; radius:70; x:-50; y:-30; color:Qt.rgba(160/255,210/255,255/255,0.08) }
        Rectangle { width:160; height:120; radius:60; x:parent.width-100; y:parent.height-50; color:Qt.rgba(140/255,140/255,255/255,0.06) }

        // Layout: 18m + 58h + 12s + 250d + 12s + fill(t) + 12s + 22f ≈ 620
        ColumnLayout {
            anchors.fill: parent; anchors.margins: 18; spacing: 12

            HeaderBar {
                Layout.fillWidth:true; Layout.preferredHeight:58
                windowPinned: mainWindow.windowPinned
                onTogglePin: mainWindow.windowPinned = !mainWindow.windowPinned
            }

            // ── Dashboard card ──
            GlassCard {
                Layout.fillWidth: true; Layout.preferredHeight: 276

                Item {
                    id: dashContent
                    anchors.fill: parent; anchors.margins: 14

                    readonly property int qw: 190   // quota width
                    readonly property int g: 10     // gap
                    readonly property int th: 168   // top row height
                    readonly property int vh: 70    // value card height
                    readonly property int tw: dashContent.width - dashContent.qw - dashContent.g

                    // Left: quota rings
                    QuotaRings {
                        x: 0; y: 0
                        width: dashContent.qw; height: dashContent.th
                    }

                    // Right top: token cards
                    TokenSummaryCards {
                        id: tokenCards
                        x: dashContent.qw + dashContent.g; y: 0
                        width: dashContent.tw; height: dashContent.th
                    }

                    // Right bottom: value progress — same x/width as token cards
                    ValueProgressCard {
                        x: tokenCards.x
                        y: dashContent.th + dashContent.g
                        width: tokenCards.width; height: dashContent.vh
                    }
                }
            }

            // ── Task board (fills remaining) ──
            GlassCard {
                Layout.fillWidth: true; Layout.fillHeight: true
                TaskBoard { anchors.fill:parent; anchors.margins:14 }
            }

            // ── Status footer (shows data source) ──
            StatusFooter { Layout.fillWidth:true; Layout.preferredHeight:22 }
        }
    }
}
