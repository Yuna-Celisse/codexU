#pragma once

#include "DataSourceInterface.h"
#include <QtCore/QString>
#include <QtCore/QStringList>

class RealDataSource : public IDataSource {
public:
    UsageSnapshot load() override;

private:
    // ── Path detection ──
    QString findCodexExe() const;
    QString findCodexDir() const;
    QString findSqliteDb(const QString &codexDir) const;

    // ── app-server stages ──
    struct AppServerData {
        QString accountPlan;
        double primaryRemaining   = -1;
        double secondaryRemaining = -1;
        QDateTime primaryResets;
        QDateTime secondaryResets;
        QStringList errors;
    };
    AppServerData readAppServer(const QString &codexExe) const;

    QString normalizePlan(const QJsonObject &accountResult) const;

    // ── Local data stages ──
    struct LocalData {
        qint64 todayTokens    = 0;
        qint64 sevenDayTokens = 0;
        qint64 lifetimeTokens = 0;
        TokenUsageSummary todayUsage;
        TokenUsageSummary sevenDayUsage;
        TokenUsageSummary lifetimeUsage;
        TokenUsageSummary monthUsage;
        double monthCost = 0;
        int parsedFiles  = 0;
        int tokenEvents  = 0;
        QVector<MiniTaskSnapshot> tasks;
        int active = 0, pending = 0, scheduled = 0, done = 0;
        QStringList errors;
    };
    LocalData readLocalData(const QString &codexDir) const;

    // ── SQLite helpers ──
    struct SessionSource {
        QString rolloutPath;
        QString model;
    };
    QVector<SessionSource> readSessionSources(const QString &dbPath) const;
    void readThreadTasks(const QString &dbPath, qint64 dayEpoch, qint64 activeCutoffEpoch,
                         LocalData &out) const;
    qint64 readSqliteAggregate(const QString &dbPath, const QString &sql) const;

    // ── JSONL helpers ──
    void parseJsonlFiles(const QVector<SessionSource> &sources, const QDateTime &dayStart,
                         const QDateTime &sevenDayStart, const QDateTime &monthStart,
                         LocalData &out) const;
    bool tryParseTokenCount(const QByteArray &line, TokenBreakdown &tokens,
                            QDateTime &timestamp) const;
    double estimateCost(const TokenBreakdown &tokens, const QString &model) const;

    // ── Automation helpers ──
    int readAutomations(const QString &automationsDir, QVector<MiniTaskSnapshot> &out) const;

    // ── Logging ──
    static void log(const QString &msg);
    static QString logPath();
    mutable QStringList m_log;
};
