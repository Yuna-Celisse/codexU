import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Rectangle {
    id: root
    radius: Theme.radiusSmall
    color: Theme.cardFill
    border.color: Theme.cardBorder
    border.width: 1

    property string cardTitle: ""
    property var tokenUsage: ({})

    property real uv: tokenUsage.uncachedNum !== undefined ? tokenUsage.uncachedNum : 0
    property real cv: tokenUsage.cachedNum   !== undefined ? tokenUsage.cachedNum   : 0
    property real ov: tokenUsage.outputNum   !== undefined ? tokenUsage.outputNum   : 0
    property real bt: Math.max(0, uv + cv + ov)

    // Computed segment widths with 2px minimum for non-zero values
    property real rawBlue:   bt > 0 ? (parent ? parent.width * (uv / bt) : 0) : 0
    property real rawPurple: bt > 0 ? (parent ? parent.width * (cv / bt) : 0) : 0
    property real rawOrange: bt > 0 ? (parent ? parent.width * (ov / bt) : 0) : 0

    Column {
        anchors.fill: parent; anchors.margins: 12; spacing: 5

        // Title row
        RowLayout {
            width: parent.width
            Text { text:root.cardTitle; font.pixelSize:14; font.weight:Font.Bold; color:Theme.textSecondary }
            Item { Layout.fillWidth:true }
            Text { text:tokenUsage.cost||"--"; font.pixelSize:11; font.weight:Font.Bold; color:Theme.textPrimary }
        }

        // Big number
        Text {
            text: tokenUsage.tokens||"--"
            font.pixelSize:23; font.weight:Font.Bold; color:Theme.textPrimary
        }

        // Proportional segment bar
        Item {
            id: bar
            width: parent.width; height: 6

            // Compute widths — min 2px for non-zero, largest segment absorbs the adjustment
            function clampW(raw, isMax, isNonZero) {
                if (!isNonZero) return 0
                if (isMax) return Math.max(2, raw)
                return Math.max(2, raw)
            }

            property real maxVal: Math.max(root.uv, root.cv, root.ov)
            property real bw: root.bt > 0 ? bar.clampW(bar.width * (root.uv / root.bt), root.uv === maxVal, root.uv > 0) : 0
            property real pw: root.bt > 0 ? bar.clampW(bar.width * (root.cv / root.bt), root.cv === maxVal, root.cv > 0) : 0
            property real ow: root.bt > 0 ? bar.clampW(bar.width * (root.ov / root.bt), root.ov === maxVal, root.ov > 0) : 0

            // Cap total to parent width
            property real totalW: bw + pw + ow
            property real scale: totalW > bar.width ? bar.width / totalW : 1.0

            // Track
            Rectangle {
                anchors.fill: parent; radius: 3
                color: Theme.trackFill
                visible: root.bt <= 0
            }

            // Blue segment (uncached)
            Rectangle {
                x: 0; height: parent.height
                width: root.bt > 0 && root.uv > 0 ? bar.bw * bar.scale : 0
                radius: width > 0 ? 3 : 0
                color: Theme.brandBlue
                visible: width > 0
            }
            // Purple segment (cached)
            Rectangle {
                x: root.bt > 0 && root.uv > 0 ? bar.bw * bar.scale : 0
                height: parent.height
                width: root.bt > 0 && root.cv > 0 ? bar.pw * bar.scale : 0
                color: Theme.brandPurple
                visible: width > 0
            }
            // Orange segment (output)
            Rectangle {
                x: root.bt > 0 ? ((root.uv > 0 ? bar.bw * bar.scale : 0) + (root.cv > 0 ? bar.pw * bar.scale : 0)) : 0
                height: parent.height
                width: root.bt > 0 && root.ov > 0 ? bar.ow * bar.scale : 0
                radius: width > 0 ? 3 : 0
                color: Theme.brandOrange
                visible: width > 0
            }
        }

        // Legend rows
        TokenL { l:"未缓存"; c:Theme.brandBlue;   v:tokenUsage.uncached||"--" }
        TokenL { l:"缓存";   c:Theme.brandPurple; v:tokenUsage.cached||"--" }
        TokenL { l:"输出";   c:Theme.brandOrange; v:tokenUsage.output||"--" }
    }

    component TokenL: RowLayout {
        property string l:""; property color c:"#000"; property string v:"--"
        spacing:5
        Rectangle { width:6; height:6; radius:3; color:c }
        Text { text:l; font.pixelSize:11; color:Theme.textSecondary; Layout.preferredWidth:38 }
        Item { Layout.fillWidth:true }
        Text { text:v; font.pixelSize:11; font.weight:Font.Bold; color:Theme.textSecondary; minimumPixelSize:10; fontSizeMode:Text.HorizontalFit }
    }
}
