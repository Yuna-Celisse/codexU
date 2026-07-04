import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

Item {
    id: root; implicitHeight: 22

    RowLayout {
        anchors.fill: parent

        Text {
            text: CodexDataProvider.messages.length > 0
                  ? CodexDataProvider.messages[CodexDataProvider.messages.length-1]
                  : "本地就绪"
            font.pixelSize:11; font.weight:Font.Bold; color:Theme.textSecondary
            elide:Text.ElideRight; Layout.fillWidth:true
        }

        Text {
            text: "刷新 "+CodexDataProvider.refreshedAt
                  + (CodexDataProvider.useMockData ? "  MOCK" : "")
                  + "  xU"
            font.pixelSize:11; font.weight:Font.Bold; color:Theme.textSecondary
        }
    }
}
