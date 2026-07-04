#include "FormatUtils.h"
#include <QtCore/QLocale>
#include <QtCore/QDateTime>
#include <QtCore/QStringBuilder>

QString FormatUtils::tokens(qint64 value)
{
    double absVal = qAbs(static_cast<double>(value));
    if (absVal >= 1'000'000'000)
        return QString::number(value / 1'000'000'000.0, 'f', 1) % "B";
    if (absVal >= 1'000'000)
        return QString::number(value / 1'000'000.0, 'f', 1) % "M";
    if (absVal >= 1'000)
        return QString::number(value / 1'000.0, 'f', 1) % "K";
    return QString::number(value);
}

QString FormatUtils::percent(double pct)
{
    if (pct < 0) return "--";
    if (pct > 0 && pct < 1) return "<1%";
    return QString::number(static_cast<int>(qRound(pct))) % "%";
}

QString FormatUtils::usd(double value)
{
    double absVal = qAbs(value);
    if (absVal >= 1'000'000)
        return "$" + QString::number(value / 1'000'000.0, 'f', 1) + "M";
    if (absVal >= 1'000)
        return "$" + QString::number(value / 1'000.0, 'f', 1) + "K";
    if (absVal >= 100)
        return "$" + QString::number(value, 'f', 0);
    return "$" + QString::number(value, 'f', 2);
}

QString FormatUtils::compactUsd(double value)
{
    double absVal = qAbs(value);
    if (absVal >= 1'000'000)
        return "$" + QString::number(value / 1'000'000.0, 'f', 1) + "M";
    if (absVal >= 10'000)
        return "$" + QString::number(value / 1'000.0, 'f', 1) + "K";
    if (absVal >= 1'000)
        return "$" + QString::number(value, 'f', 0);
    return "$" + QString::number(value, 'f', 0);
}

QString FormatUtils::resetTime(const QDateTime &dt)
{
    if (!dt.isValid()) return "--";
    return dt.toLocalTime().toString("M/d HH:mm");
}

QString FormatUtils::relativeTime(const QDateTime &dt)
{
    if (!dt.isValid()) return "--";
    qint64 secs = dt.secsTo(QDateTime::currentDateTime());
    if (secs < 0) secs = 0;
    if (secs < 60) return QStringLiteral("刚刚");
    qint64 mins = secs / 60;
    if (mins < 60) return QString::number(mins) % " 分钟前";
    qint64 hours = mins / 60;
    if (hours < 24) return QString::number(hours) % " 小时前";
    return QString::number(hours / 24) % " 天前";
}

QString FormatUtils::planLabel(const QString &rawPlan)
{
    QString plan = rawPlan.toUpper();
    if (plan.contains("PLUS")) return "PLUS";
    if (plan.contains("PRO"))  return "PRO";
    if (plan.contains("TEAM")) return "PRO";
    return "PLUS";
}
