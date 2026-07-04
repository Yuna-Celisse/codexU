#include "CodexDataProvider.h"
#include "MockDataSource.h"
#include "RealDataSource.h"
#include "FormatUtils.h"
#include <QtCore/QDateTime>
#include <QtCore/QTimer>
#include <QtCore/QDebug>
#include <QtConcurrent/QtConcurrent>
#include <QtCore/QFuture>
#include <QtCore/QThreadPool>

CodexDataProvider::CodexDataProvider(bool useMock, QObject *parent)
    : QObject(parent)
    , m_useMock(useMock)
    , m_taskModel(new TaskListModel(this))
{
    if (m_useMock) {
        m_source.reset(new MockDataSource);
        m_dataSource = "mock";
    } else {
        // Default: attempt real data from local Codex installation
        m_source.reset(new RealDataSource);
        m_dataSource = "empty";
    }

    m_refreshTimer = new QTimer(this);
    m_refreshTimer->setInterval(60'000);   // 60-second auto-refresh
    connect(m_refreshTimer, &QTimer::timeout, this, &CodexDataProvider::refresh);
    m_refreshTimer->start();

    // Initial refresh after event loop starts
    QTimer::singleShot(500, this, &CodexDataProvider::refresh);
}

void CodexDataProvider::refresh()
{
    if (m_loading) return;
    m_loading = true;
    emit loadingChanged();

    auto f = QtConcurrent::run([this]() {
        UsageSnapshot snap;
        try {
            snap = m_source->load();
            if (!validateSnapshot(snap)) {
                // Validation failed — keep old data, mark as partial
                QMetaObject::invokeMethod(this, [this]() {
                    m_dataSource = "partial";
                    m_loading = false;
                    emit loadingChanged();
                    emit dataChanged();
                }, Qt::QueuedConnection);
                return;
            }
        } catch (...) {
            // Load threw — keep old data, mark error
            QMetaObject::invokeMethod(this, [this]() {
                m_dataSource = "error";
                m_loading = false;
                emit loadingChanged();
                emit dataChanged();
            }, Qt::QueuedConnection);
            return;
        }

        QMetaObject::invokeMethod(this, [this, snap]() {
            applySnapshot(snap);
            if (m_useMock)
                m_dataSource = "mock";
            else if (snap.messages.isEmpty() || !snap.accountPlan.isEmpty())
                m_dataSource = "real";
            else
                m_dataSource = "partial";
            m_loading = false;
            emit loadingChanged();
            emit dataChanged();
        }, Qt::QueuedConnection);
    });
}

void CodexDataProvider::applySnapshot(const UsageSnapshot &snap)
{
    // Merge: keep old valid quota/token fields if new ones are invalid
    if (snap.primaryRemainingPercent >= 0 && snap.primaryRemainingPercent <= 100)
        m_snapshot.primaryRemainingPercent = snap.primaryRemainingPercent;
    // else keep old value

    if (snap.secondaryRemainingPercent >= 0 && snap.secondaryRemainingPercent <= 100)
        m_snapshot.secondaryRemainingPercent = snap.secondaryRemainingPercent;

    if (snap.primaryResetsAt.isValid())
        m_snapshot.primaryResetsAt = snap.primaryResetsAt;

    if (snap.secondaryResetsAt.isValid())
        m_snapshot.secondaryResetsAt = snap.secondaryResetsAt;

    if (!snap.accountPlan.isEmpty())
        m_snapshot.accountPlan = snap.accountPlan;

    // Token/task fields: only overwrite if new data has substance
    if (snap.lifetimeTokens > 0) {
        m_snapshot.todayTokens    = snap.todayTokens;
        m_snapshot.sevenDayTokens = snap.sevenDayTokens;
        m_snapshot.lifetimeTokens = snap.lifetimeTokens;
        m_snapshot.todayUsage     = snap.todayUsage;
        m_snapshot.sevenDayUsage  = snap.sevenDayUsage;
        m_snapshot.lifetimeUsage  = snap.lifetimeUsage;
        m_snapshot.monthUsage     = snap.monthUsage;
        m_snapshot.monthEstimatedCost = snap.monthEstimatedCost;
        m_snapshot.parsedFiles    = snap.parsedFiles;
        m_snapshot.tokenEventCount = snap.tokenEventCount;
    }

    if (!snap.tasks.isEmpty()) {
        m_snapshot.tasks          = snap.tasks;
        m_snapshot.activeTasks    = snap.activeTasks;
        m_snapshot.pendingTasks   = snap.pendingTasks;
        m_snapshot.scheduledTasks = snap.scheduledTasks;
        m_snapshot.doneTasks      = snap.doneTasks;
    }

    m_snapshot.refreshedAt = snap.refreshedAt;
    m_snapshot.messages    = snap.messages;
    m_taskModel->setTasks(m_snapshot.tasks);
}

bool CodexDataProvider::validateSnapshot(const UsageSnapshot &snap) const
{
    // If all data is default/empty, that's OK for empty source
    // For mock source, anything goes (it's explicitly opted-in)
    if (m_useMock) return true;

    // For real source: basic sanity checks
    if (snap.primaryRemainingPercent < -1 || snap.primaryRemainingPercent > 100) return false;
    if (snap.secondaryRemainingPercent < -1 || snap.secondaryRemainingPercent > 100) return false;
    if (snap.todayTokens < 0 || snap.sevenDayTokens < 0 || snap.lifetimeTokens < 0) return false;
    if (snap.monthEstimatedCost < 0) return false;
    if (snap.parsedFiles < 0 || snap.tokenEventCount < 0) return false;

    return true;
}

// ── Getters ────────────────────────────────────────────────

QString CodexDataProvider::accountPlan() const
{
    QString p = m_snapshot.accountPlan.trimmed().toUpper();
    if (p.isEmpty()) return "N/A";
    if (p == "PLUS" || p == "PRO" || p == "MAX" || p == "FREE" || p == "TEAM")
        return FormatUtils::planLabel(m_snapshot.accountPlan);
    return "N/A";
}

QString CodexDataProvider::refreshedAt() const
{
    if (!m_snapshot.refreshedAt.isValid()) return "--:--";
    return m_snapshot.refreshedAt.toLocalTime().toString("HH:mm");
}

bool CodexDataProvider::loading() const { return m_loading; }

double CodexDataProvider::primaryRemainingPercent() const   { return m_snapshot.primaryRemainingPercent; }
double CodexDataProvider::secondaryRemainingPercent() const { return m_snapshot.secondaryRemainingPercent; }

QString CodexDataProvider::primaryResetText() const   { return FormatUtils::resetTime(m_snapshot.primaryResetsAt); }
QString CodexDataProvider::secondaryResetText() const { return FormatUtils::resetTime(m_snapshot.secondaryResetsAt); }

QVariantMap CodexDataProvider::todayUsage() const     { return tokenUsageToMap(m_snapshot.todayUsage); }
QVariantMap CodexDataProvider::sevenDayUsage() const  { return tokenUsageToMap(m_snapshot.sevenDayUsage); }
QVariantMap CodexDataProvider::lifetimeUsage() const  { return tokenUsageToMap(m_snapshot.lifetimeUsage); }

double CodexDataProvider::monthEstimatedCost() const  { return m_snapshot.monthEstimatedCost; }

QString CodexDataProvider::valueProgressText() const
{
    return FormatUtils::usd(m_snapshot.monthEstimatedCost) % " / $2.0K";
}

int CodexDataProvider::activeTasks() const    { return m_snapshot.activeTasks; }
int CodexDataProvider::pendingTasks() const   { return m_snapshot.pendingTasks; }
int CodexDataProvider::scheduledTasks() const { return m_snapshot.scheduledTasks; }
int CodexDataProvider::doneTasks() const      { return m_snapshot.doneTasks; }

QStringList CodexDataProvider::messages() const { return m_snapshot.messages; }

QString CodexDataProvider::dataSource() const { return m_dataSource; }

QVariantMap CodexDataProvider::tokenUsageToMap(const TokenUsageSummary &u)
{
    QVariantMap m;
    qint64 uncached = u.tokens.uncachedInput();
    qint64 cached   = u.tokens.billableCachedInput();
    qint64 output   = u.tokens.output;

    // Formatted display strings
    m["tokens"]          = FormatUtils::tokens(u.tokens.visibleTotal());
    m["cost"]            = FormatUtils::usd(u.estimatedCostUSD);
    m["uncached"]        = FormatUtils::tokens(uncached);
    m["cached"]          = FormatUtils::tokens(cached);
    m["output"]          = FormatUtils::tokens(output);
    m["estimatedCostUSD"] = u.estimatedCostUSD;

    // Raw numeric values — used for proportional bar rendering
    m["uncachedNum"] = static_cast<double>(uncached);
    m["cachedNum"]   = static_cast<double>(cached);
    m["outputNum"]   = static_cast<double>(output);

    return m;
}
