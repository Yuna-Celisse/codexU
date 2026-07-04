#pragma once

#include <QtCore/QString>
#include <QtCore/QDateTime>
#include <QtCore/QVector>

// ---------------------------------------------------------------------------
// POD data structures matching the C# models in Program.cs.
// Zero Qt meta-object overhead — just plain data.
// ---------------------------------------------------------------------------

struct TokenBreakdown {
    qint64 input            = 0;
    qint64 cachedInput      = 0;
    qint64 output           = 0;
    qint64 reasoningOutput  = 0;
    qint64 total            = 0;

    qint64 billableCachedInput() const {
        auto c = qMax(cachedInput, qint64(0));
        auto i = qMax(input, qint64(0));
        return qMin(c, i);
    }

    qint64 uncachedInput() const {
        return qMax(input - billableCachedInput(), qint64(0));
    }

    qint64 visibleTotal() const {
        return qMax(total, input + output);
    }

    bool isZero() const {
        return input == 0 && cachedInput == 0 && output == 0
            && reasoningOutput == 0 && total == 0;
    }

    bool hasNegativeValue() const {
        return input < 0 || cachedInput < 0 || output < 0
            || reasoningOutput < 0 || total < 0;
    }

    TokenBreakdown delta(const TokenBreakdown &prev) const {
        TokenBreakdown d;
        d.input           = input           - prev.input;
        d.cachedInput     = cachedInput     - prev.cachedInput;
        d.output          = output          - prev.output;
        d.reasoningOutput = reasoningOutput - prev.reasoningOutput;
        d.total           = total           - prev.total;
        if (d.total == 0)
            d.total = d.input + d.output;
        return d;
    }
};

struct TokenUsageSummary {
    TokenBreakdown tokens;
    double estimatedCostUSD = 0.0;
};

struct MiniTaskSnapshot {
    enum Kind { Active = 0, Pending, Scheduled, Done };
    Kind kind        = Pending;
    QString code;          // e.g. "COD-ABCD"
    QString title;
    QString detail;        // e.g. "project-name · 2.4M"
    QString chip;          // e.g. "Active", "High", "Idle", "Cron", "Done"
    qint64 tokens = 0;
    QDateTime updatedAt;
};

struct UsageSnapshot {
    QDateTime refreshedAt      = QDateTime::currentDateTime();
    QString accountPlan;       // "PLUS" / "PRO" / ""
    double primaryRemainingPercent   = -1.0;   // -1 = unavailable
    double secondaryRemainingPercent = -1.0;
    QDateTime primaryResetsAt;
    QDateTime secondaryResetsAt;

    // Aggregate token counts (from SQLite threads table)
    qint64 todayTokens     = 0;
    qint64 sevenDayTokens  = 0;
    qint64 lifetimeTokens  = 0;

    // Detailed per-model usage (from JSONL token_count events)
    TokenUsageSummary todayUsage;
    TokenUsageSummary sevenDayUsage;
    TokenUsageSummary lifetimeUsage;
    TokenUsageSummary monthUsage;
    double monthEstimatedCost = 0.0;

    // Task board
    int activeTasks    = 0;
    int pendingTasks   = 0;
    int scheduledTasks = 0;
    int doneTasks      = 0;
    QVector<MiniTaskSnapshot> tasks;

    // Diagnostics
    int parsedFiles     = 0;
    int tokenEventCount = 0;
    QStringList messages;
};
