import QtQuick
import QtQuick.Layouts
import codexu 1.0
import "../Theme.js" as Theme

RowLayout {
    id: root; spacing: 10
    TokenCard { Layout.fillWidth:true; Layout.fillHeight:true; cardTitle:"今日";     tokenUsage:CodexDataProvider.todayUsage }
    TokenCard { Layout.fillWidth:true; Layout.fillHeight:true; cardTitle:"近 7 天"; tokenUsage:CodexDataProvider.sevenDayUsage }
    TokenCard { Layout.fillWidth:true; Layout.fillHeight:true; cardTitle:"累计";     tokenUsage:CodexDataProvider.lifetimeUsage }
}
