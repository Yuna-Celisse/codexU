#pragma once

#include <QtCore/QString>
#include <QtCore/QDateTime>
#include <QtCore/QObject>
// ---------------------------------------------------------------------------
// QML singleton for number / date formatting.
// Matches the C# UiFormat and UiLabels classes from Program.cs.
//
// Usage from QML:
//   import codexu 1.0
//   Text { text: FormatUtils.tokens(1234567) }   // "1.2M"
// ---------------------------------------------------------------------------
class FormatUtils : public QObject {
    Q_OBJECT

public:
    // Token counts
    Q_INVOKABLE static QString tokens(qint64 value);
    Q_INVOKABLE static QString percent(double value);       // --, <1%, 85%
    Q_INVOKABLE static QString usd(double value);           // $0.00, $1.2K, $3.4M
    Q_INVOKABLE static QString compactUsd(double value);    // $1.2K, $3.4M
    Q_INVOKABLE static QString resetTime(const QDateTime &dt);
    Q_INVOKABLE static QString relativeTime(const QDateTime &dt);
    Q_INVOKABLE static QString planLabel(const QString &rawPlan);
};
