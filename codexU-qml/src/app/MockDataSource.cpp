#include "MockDataSource.h"
#include <QtCore/QRandomGenerator>
#include <QtCore/QDateTime>

UsageSnapshot MockDataSource::load()
{
    UsageSnapshot s;

    auto rng = QRandomGenerator::global();

    // ── Account ──
    s.accountPlan = (rng->bounded(2) == 0) ? "PLUS" : "PRO";

    // ── Rate limits ──
    s.primaryRemainingPercent   = rng->bounded(50, 96);
    s.secondaryRemainingPercent = rng->bounded(30, 86);

    auto now = QDateTime::currentDateTime();
    s.primaryResetsAt   = now.addSecs(rng->bounded(600, 10800));   // 10 min – 3h
    s.secondaryResetsAt = now.addSecs(rng->bounded(3600, 86400));  // 1h – 24h

    // ── Token breakdowns ──
    auto makeBreakdown = [&](qint64 scale) -> TokenBreakdown {
        TokenBreakdown tb;
        tb.output  = rng->bounded(scale / 4,  scale / 2);
        tb.input   = rng->bounded(scale / 2,  scale);
        tb.cachedInput = tb.input / 3;
        tb.total   = tb.input + tb.output;
        return tb;
    };

    s.todayUsage.tokens = makeBreakdown(24'000'000);
    s.todayUsage.estimatedCostUSD = rng->bounded(2, 8) + rng->bounded(100) / 100.0;
    s.sevenDayUsage.tokens = makeBreakdown(25'000'000);
    s.sevenDayUsage.estimatedCostUSD = s.todayUsage.estimatedCostUSD * 7 * 0.8;
    s.lifetimeUsage.tokens = makeBreakdown(177'000'000);
    s.lifetimeUsage.estimatedCostUSD = 47.75;
    s.monthUsage.tokens = makeBreakdown(36'000'000);
    s.monthEstimatedCost = s.todayUsage.estimatedCostUSD * QDateTime::currentDateTime().date().day();

    s.todayTokens    = s.todayUsage.tokens.visibleTotal();
    s.sevenDayTokens = s.sevenDayUsage.tokens.visibleTotal();
    s.lifetimeTokens = s.lifetimeUsage.tokens.visibleTotal();

    // ── Tasks ──
    auto makeTask = [&](MiniTaskSnapshot::Kind kind, const QString &code,
                         const QString &title, const QString &detail,
                         const QString &chip, int ageMins) {
        MiniTaskSnapshot t;
        t.kind      = kind;
        t.code      = code;
        t.title     = title;
        t.detail    = detail;
        t.chip      = chip;
        t.updatedAt = now.addSecs(-ageMins * 60);
        return t;
    };

    s.tasks.append(makeTask(MiniTaskSnapshot::Active,   "COD-3F7A", "Add user auth middleware",   "server · 5.2M", "High",   25));
    s.tasks.append(makeTask(MiniTaskSnapshot::Active,   "COD-9B12", "Fix SQL query timeout",       "api · 840K",    "Active", 45));
    s.tasks.append(makeTask(MiniTaskSnapshot::Pending,  "COD-C4DE", "Refactor config module",      "shared · 1.1M", "Idle",   180));
    s.tasks.append(makeTask(MiniTaskSnapshot::Pending,  "COD-1A2B", "Update README screenshots",   "docs · 12K",    "Idle",   240));
    s.tasks.append(makeTask(MiniTaskSnapshot::Scheduled,"AUTO-CRON","Nightly dependency scan",     "CRON · 02:00",  "Cron",   0));
    s.tasks.append(makeTask(MiniTaskSnapshot::Done,     "COD-7E8F", "Bump version to 0.3.0",      "release · 32K", "Done",   360));
    s.tasks.append(makeTask(MiniTaskSnapshot::Done,     "COD-2D3E", "Merge PR #42",                "repo · 180K",   "Done",   480));

    s.activeTasks    = 2;
    s.pendingTasks   = 2;
    s.scheduledTasks = 1;
    s.doneTasks      = 2;

    // ── Diagnostics ──
    s.parsedFiles     = 27;
    s.tokenEventCount = 1630;
    s.messages.append(QStringLiteral("已解析 session %1 个，token_count 事件 %2 条")
                          .arg(s.parsedFiles).arg(s.tokenEventCount));

    return s;
}
