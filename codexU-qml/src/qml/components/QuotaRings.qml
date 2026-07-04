import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Item {
    id: root
    width: 190; height: 168

    function isValidPercent(v) {
        return typeof v === "number" && isFinite(v) && v >= 0 && v <= 100
    }

    // ── Ring area 160×160 — z:0 ──
    Canvas {
        id: ringCanvas
        anchors.horizontalCenter: parent.horizontalCenter
        y: 0; z: 0
        width: 160; height: 160

        property real fiveHPct: {
            var v = CodexDataProvider.primaryRemainingPercent
            return (typeof v === "number" && isFinite(v)) ? v : -1
        }
        property real sevenDPct: {
            var v = CodexDataProvider.secondaryRemainingPercent
            return (typeof v === "number" && isFinite(v)) ? v : -1
        }

        onFiveHPctChanged: requestPaint()
        onSevenDPctChanged: requestPaint()
        onWidthChanged: if (width > 0) requestPaint()
        onHeightChanged: if (height > 0) requestPaint()
        onVisibleChanged: if (visible) requestPaint()
        Component.onCompleted: requestPaint()

        Connections {
            target: CodexDataProvider
            function onDataChanged() { ringCanvas.requestPaint() }
            function onLoadingChanged() { ringCanvas.requestPaint() }
        }

        onPaint: {
            var ctx = getContext("2d")
            ctx.clearRect(0, 0, width, height)
            if (width <= 0 || height <= 0) return

            var trackColor = Qt.rgba(148/255, 163/255, 184/255, 0.18)
            var cx = 80, cy = 80

            function drawTrack(r) {
                ctx.beginPath()
                ctx.lineWidth = 8; ctx.lineCap = "round"
                ctx.strokeStyle = trackColor
                ctx.arc(cx, cy, r, 0, Math.PI * 2)
                ctx.stroke()
            }
            function drawArc(r, rawPercent, color) {
                if (!isValidPercent(rawPercent)) return
                var pct = Math.max(0, Math.min(100, rawPercent)) / 100
                if (!isFinite(pct) || pct <= 0.0001) return
                var startAngle = -Math.PI * 0.5
                var sweep = Math.PI * 2 * pct
                if (!isFinite(sweep)) return
                ctx.beginPath()
                ctx.lineWidth = 8; ctx.lineCap = "round"
                ctx.strokeStyle = color
                ctx.arc(cx, cy, r, startAngle, startAngle + sweep)
                ctx.stroke()
            }

            // Outer: r=68  → inner edge at 64, outer at 72
            // Inner: r=50  → inner edge at 46, outer at 54
            // Center text at r≤42 — well clear of inner ring edge
            drawTrack(68)
            drawArc(68, fiveHPct, Theme.brandBlue)
            drawTrack(50)
            drawArc(50, sevenDPct, Theme.brandTeal)
        }
    }

    // ── Center text overlay — z:10, always above rings ──
    Item {
        id: centerTextOverlay
        anchors.centerIn: ringCanvas
        z: 10
        width: 80; height: 56

        Column {
            anchors.centerIn: parent
            spacing: 2; width: 80

            Row {
                anchors.horizontalCenter: parent.horizontalCenter
                spacing: 5
                Text {
                    text: "5h"; width: 24
                    horizontalAlignment: Text.AlignRight
                    font.pixelSize: 12; font.weight: Font.Bold
                    color: Theme.brandBlue
                    anchors.verticalCenter: parent.verticalCenter
                }
                Text {
                    text: isValidPercent(CodexDataProvider.primaryRemainingPercent)
                          ? FormatUtils.percent(CodexDataProvider.primaryRemainingPercent) : "--"
                    font.pixelSize: 21; font.weight: Font.Bold
                    color: Theme.textPrimary
                    width: 46; horizontalAlignment: Text.AlignLeft
                    anchors.verticalCenter: parent.verticalCenter
                }
            }
            Row {
                anchors.horizontalCenter: parent.horizontalCenter
                spacing: 5
                Text {
                    text: "7d"; width: 24
                    horizontalAlignment: Text.AlignRight
                    font.pixelSize: 12; font.weight: Font.Bold
                    color: Theme.brandTeal
                    anchors.verticalCenter: parent.verticalCenter
                }
                Text {
                    text: isValidPercent(CodexDataProvider.secondaryRemainingPercent)
                          ? FormatUtils.percent(CodexDataProvider.secondaryRemainingPercent) : "--"
                    font.pixelSize: 21; font.weight: Font.Bold
                    color: Theme.textPrimary
                    width: 46; horizontalAlignment: Text.AlignLeft
                    anchors.verticalCenter: parent.verticalCenter
                }
            }
        }
    }

    // ── Reset times ──
    Column {
        anchors.horizontalCenter: parent.horizontalCenter
        y: ringCanvas.y + ringCanvas.height + 6; width: 160; spacing: 4

        RowLayout {
            spacing: 6; width: parent.width
            Rectangle { width: 5; height: 5; radius: 2; color: Theme.brandBlue }
            Text { text: "5h 重置"; font.pixelSize: 11; color: Theme.textSecondary }
            Item { Layout.fillWidth: true }
            Text {
                text: CodexDataProvider.primaryResetText
                font.pixelSize: 11; font.weight: Font.Bold; color: Theme.textPrimary
            }
        }
        RowLayout {
            spacing: 6; width: parent.width
            Rectangle { width: 5; height: 5; radius: 2; color: Theme.brandTeal }
            Text { text: "7d 重置"; font.pixelSize: 11; color: Theme.textSecondary }
            Item { Layout.fillWidth: true }
            Text {
                text: CodexDataProvider.secondaryResetText
                font.pixelSize: 11; font.weight: Font.Bold; color: Theme.textPrimary
            }
        }
    }
}
