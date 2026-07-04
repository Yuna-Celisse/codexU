#include "RealDataSource.h"
#include "Models.h"
#include <QtCore/QProcess>
#include <QtCore/QJsonDocument>
#include <QtCore/QJsonObject>
#include <QtCore/QJsonArray>
#include <QtCore/QFile>
#include <QtCore/QDir>
#include <QtCore/QDirIterator>
#include <QtCore/QStandardPaths>
#include <QtCore/QDateTime>
#include <QtCore/QElapsedTimer>
#include <QtCore/QDate>
#include <QtCore/QTime>
#include <QtCore/QFileInfo>
#include <QtCore/QTextStream>
#include <QtCore/QDebug>
#include <QtSql/QSqlDatabase>
#include <QtSql/QSqlQuery>
#include <QtSql/QSqlError>

// ── Logging ──────────────────────────────────────────────────
QString RealDataSource::logPath()
{
    QString dir = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation);
    QDir().mkpath(dir);
    return dir + "/codexu.log";
}

void RealDataSource::log(const QString &msg)
{
    QString line = QDateTime::currentDateTime().toString("HH:mm:ss.zzz") + " " + msg + "\n";
    QFile f(logPath());
    if (f.open(QIODevice::Append | QIODevice::Text)) {
        f.write(line.toUtf8());
        f.close();
    }
    qDebug().noquote() << msg;
}

// ── Main entry ───────────────────────────────────────────────
UsageSnapshot RealDataSource::load()
{
    m_log.clear();
    log("[refresh] start");

    UsageSnapshot snap;
    snap.refreshedAt = QDateTime::currentDateTime();

    QString codexDir = findCodexDir();
    log("[paths] codexDir=" + codexDir);

    // Stage 1: app-server (quota + account)
    QString codexExe = findCodexExe();
    log("[codex] exe=" + codexExe);
    if (!codexExe.isEmpty()) {
        AppServerData as = readAppServer(codexExe);
        snap.accountPlan = as.accountPlan;
        snap.primaryRemainingPercent = as.primaryRemaining;
        snap.secondaryRemainingPercent = as.secondaryRemaining;
        snap.primaryResetsAt = as.primaryResets;
        snap.secondaryResetsAt = as.secondaryResets;
        snap.messages.append(as.errors);
        log("[codex] plan=" + as.accountPlan
            + " primary=" + QString::number(as.primaryRemaining)
            + " secondary=" + QString::number(as.secondaryRemaining));
    } else {
        snap.messages.append("找不到 codex 命令，请检查 PATH 或安装 Codex");
        log("[codex] NOT FOUND");
    }

    // Stage 2: local data (SQLite + JSONL + automations)
    if (!codexDir.isEmpty() && QDir(codexDir).exists()) {
        LocalData ld = readLocalData(codexDir);
        snap.todayTokens    = ld.todayTokens;
        snap.sevenDayTokens = ld.sevenDayTokens;
        snap.lifetimeTokens = ld.lifetimeTokens;
        snap.todayUsage     = ld.todayUsage;
        snap.sevenDayUsage  = ld.sevenDayUsage;
        snap.lifetimeUsage  = ld.lifetimeUsage;
        snap.monthUsage     = ld.monthUsage;
        snap.monthEstimatedCost = ld.monthCost;
        snap.parsedFiles    = ld.parsedFiles;
        snap.tokenEventCount = ld.tokenEvents;
        snap.tasks          = ld.tasks;
        snap.activeTasks    = ld.active;
        snap.pendingTasks   = ld.pending;
        snap.scheduledTasks = ld.scheduled;
        snap.doneTasks      = ld.done;
        snap.messages.append(ld.errors);

        if (ld.parsedFiles > 0) {
            snap.messages.append(QString("已解析 session %1 个，token_count 事件 %2 条")
                                     .arg(ld.parsedFiles).arg(ld.tokenEvents));
        }
        log(QString("[jsonl] parsedFiles=%1 events=%2 today=%3 week=%4 lifetime=%5")
                .arg(ld.parsedFiles).arg(ld.tokenEvents)
                .arg(ld.todayTokens).arg(ld.sevenDayTokens).arg(ld.lifetimeTokens));
        log(QString("[tokens.today] input=%1 cached=%2 output=%3")
                .arg(ld.todayUsage.tokens.input)
                .arg(ld.todayUsage.tokens.cachedInput)
                .arg(ld.todayUsage.tokens.output));
        log(QString("[tokens.lifetime] input=%1 cached=%2 output=%3")
                .arg(ld.lifetimeUsage.tokens.input)
                .arg(ld.lifetimeUsage.tokens.cachedInput)
                .arg(ld.lifetimeUsage.tokens.output));
    } else {
        snap.messages.append("找不到 .codex 数据目录");
        log("[paths] .codex dir NOT FOUND");
    }

    log("[refresh] done messages=" + QString::number(snap.messages.size()));
    return snap;
}

// ── Path detection ───────────────────────────────────────────

QString RealDataSource::findCodexExe() const
{
    // Check environment override first
    QString envPath = qEnvironmentVariable("CODEXU_CODEX_EXE");
    if (!envPath.isEmpty() && QFile::exists(envPath)) return envPath;

    // Windows common install locations
    QStringList candidates = {
        QDir::fromNativeSeparators(qEnvironmentVariable("LOCALAPPDATA"))
            + "/Programs/OpenAI/Codex/bin/codex.exe",
        QDir::fromNativeSeparators(qEnvironmentVariable("LOCALAPPDATA"))
            + "/Programs/codex/codex.exe",
    };

    for (const auto &c : candidates) {
        if (QFile::exists(c)) return QDir::toNativeSeparators(c);
    }

    // Try PATH
    QString pathEnv = qEnvironmentVariable("PATH");
    for (const auto &part : pathEnv.split(QDir::listSeparator())) {
        QString candidate = QDir(part.trimmed()).filePath("codex.exe");
        if (QFile::exists(candidate)) return candidate;
        candidate = QDir(part.trimmed()).filePath("codex.cmd");
        if (QFile::exists(candidate)) return candidate;
    }

    // Fallback: just "codex" and hope it's on PATH
    return "codex";
}

QString RealDataSource::findCodexDir() const
{
    QString envDir = qEnvironmentVariable("CODEXU_CODEX_DIR");
    if (!envDir.isEmpty()) return envDir;

    QString home = qEnvironmentVariable("USERPROFILE");
    if (home.isEmpty()) home = QDir::homePath();

    QString dotCodex = QDir(home).filePath(".codex");
    return QDir::toNativeSeparators(dotCodex);
}

QString RealDataSource::findSqliteDb(const QString &codexDir) const
{
    QString a = QDir(codexDir).filePath("state_5.sqlite");
    if (QFile::exists(a)) return a;
    QString b = QDir(codexDir).filePath("sqlite/state_5.sqlite");
    if (QFile::exists(b)) return b;
    return {};
}

// ── app-server JSON-RPC ──────────────────────────────────────

RealDataSource::AppServerData RealDataSource::readAppServer(const QString &codexExe) const
{
    AppServerData out;
    QProcess proc;
    proc.setProgram(codexExe);
    proc.setArguments({"app-server"});
    proc.setProcessChannelMode(QProcess::SeparateChannels);
    proc.start();

    if (!proc.waitForStarted(5000)) {
        out.errors.append("app-server 启动失败: " + proc.errorString());
        log("[codex] start FAILED: " + proc.errorString());
        return out;
    }
    log("[codex] app-server started pid=" + QString::number(proc.processId()));

    auto writeJson = [&](const QJsonObject &obj) {
        QByteArray data = QJsonDocument(obj).toJson(QJsonDocument::Compact) + "\n";
        proc.write(data);
        proc.waitForBytesWritten(1000);
    };

    // initialize
    QJsonObject initParams;
    initParams["clientInfo"] = QJsonObject{
        {"name", "codexu-qml"}, {"title", "codexU QML"}, {"version", "0.3.0"}
    };
    initParams["capabilities"] = QJsonObject{
        {"experimentalApi", true},
        {"optOutNotificationMethods", QJsonArray{}}
    };
    writeJson({{"id", 1}, {"method", "initialize"}, {"params", initParams}});

    // Read initialize response, then send data requests
    bool sentRequests = false;
    QSet<int> completed;
    QByteArray buffer;
    QElapsedTimer deadline;
    deadline.start();

    while (deadline.elapsed() < 12000 && completed.size() < 3) {
        if (proc.state() != QProcess::Running) break;

        proc.waitForReadyRead(200);
        buffer.append(proc.readAllStandardOutput());

        while (true) {
            int nl = buffer.indexOf('\n');
            if (nl < 0) break;
            QByteArray line = buffer.left(nl).trimmed();
            buffer.remove(0, nl + 1);
            if (line.isEmpty()) continue;

            QJsonParseError err;
            QJsonDocument doc = QJsonDocument::fromJson(line, &err);
            if (err.error != QJsonParseError::NoError) continue;

            QJsonObject obj = doc.object();
            int id = obj.value("id").toInt(-1);

            // Error response
            if (obj.contains("error")) {
                QString errMsg = obj["error"].toObject().value("message").toString();
                out.errors.append(QString("app-server id=%1: %2").arg(id).arg(errMsg));
                log("[codex] ERROR id=" + QString::number(id) + " " + errMsg);
                completed.insert(id);
                continue;
            }

            // Initialize response → send data requests
            if (id == 1 && !sentRequests) {
                sentRequests = true;
                writeJson({{"method", "initialized"}});
                writeJson({{"id", 2}, {"method", "account/read"}, {"params", QJsonObject{{"refreshToken", false}}}});
                writeJson({{"id", 3}, {"method", "account/rateLimits/read"}});
                writeJson({{"id", 4}, {"method", "account/usage/read"}});
                log("[codex] sent account+rateLimits+usage requests");
                continue;
            }

            QJsonObject result = obj.value("result").toObject();
            if (result.isEmpty()) { completed.insert(id); continue; }

            if (id == 2) {
                QJsonObject account = result.value("account").toObject();
                out.accountPlan = normalizePlan(account.isEmpty() ? result : account);
                log("[account] raw keys=" + QStringList(result.keys()).join(",")
                    + " planLabel=" + out.accountPlan);
            }
            if (id == 3) {
                log("[quota] rateLimits/read raw keys=" + QStringList(result.keys()).join(","));

                auto parseWindow = [&](const QJsonObject &w, double &remaining, QDateTime &reset,
                                        const QString &name) {
                    if (w.isEmpty()) { log("[quota] " + name + " window empty"); return; }
                    double used = w.value("usedPercent").toDouble(-999);
                    if (used == -999) used = w.value("used_percent").toDouble(-999);
                    if (used >= 0 && used <= 100) {
                        remaining = qMax(0.0, qMin(100.0, 100.0 - used));
                    }
                    double ts = w.value("resetsAt").toDouble(0);
                    if (ts == 0) ts = w.value("resets_at").toDouble(0);
                    if (ts > 0) reset = QDateTime::fromSecsSinceEpoch(static_cast<qint64>(ts));
                    log(QString("[quota] %1 used=%2 remaining=%3 reset=%4")
                            .arg(name).arg(used).arg(remaining).arg(reset.toString()));
                };

                QJsonObject limits;
                QJsonObject byId = result.value("rateLimitsByLimitId").toObject();
                if (!byId.isEmpty()) {
                    limits = byId.value("codex").toObject();
                    log("[quota] found rateLimitsByLimitId.codex keys=" + QStringList(limits.keys()).join(","));
                }
                if (limits.isEmpty()) {
                    limits = result.value("rateLimits").toObject();
                    log("[quota] trying rateLimits keys=" + QStringList(limits.keys()).join(","));
                }

                if (!limits.isEmpty()) {
                    parseWindow(limits.value("primary").toObject(), out.primaryRemaining, out.primaryResets, "primary");
                    parseWindow(limits.value("secondary").toObject(), out.secondaryRemaining, out.secondaryResets, "secondary");
                } else {
                    log("[quota] no limits object found");
                }

                // Fallback: top-level windows
                if (out.primaryRemaining < 0) {
                    parseWindow(result.value("primary").toObject(), out.primaryRemaining, out.primaryResets, "primary(fb)");
                }
                if (out.secondaryRemaining < 0) {
                    parseWindow(result.value("secondary").toObject(), out.secondaryRemaining, out.secondaryResets, "secondary(fb)");
                }

                log(QString("[quota] final primary=%1% secondary=%2%")
                        .arg(out.primaryRemaining >= 0 ? QString::number(out.primaryRemaining) : "invalid")
                        .arg(out.secondaryRemaining >= 0 ? QString::number(out.secondaryRemaining) : "invalid"));
            }
            if (id == 4) {
                // usage/read — could extract lifetime tokens, but JSONL is more detailed
                log("[usage] received");
            }
            if (id >= 2 && id <= 4) completed.insert(id);
        }
    }

    if (completed.size() < 2) {
        out.errors.append("app-server 响应超时");
        log("[codex] TIMEOUT completed=" + QString::number(completed.size()));
    }

    proc.kill();
    proc.waitForFinished(2000);
    return out;
}

QString RealDataSource::normalizePlan(const QJsonObject &obj) const
{
    // Try all common field paths
    QString raw;
    if (raw.isEmpty()) raw = obj.value("planType").toString();
    if (raw.isEmpty()) raw = obj.value("plan").toString();
    if (raw.isEmpty()) raw = obj.value("subscription").toString();
    if (raw.isEmpty()) raw = obj.value("plan_type").toString();

    // Nested: account.planType, entitlements.plan
    if (raw.isEmpty()) raw = obj.value("account").toObject().value("planType").toString();
    if (raw.isEmpty()) raw = obj.value("account").toObject().value("plan").toString();
    if (raw.isEmpty()) raw = obj.value("entitlements").toObject().value("plan").toString();
    if (raw.isEmpty()) raw = obj.value("user").toObject().value("plan").toString();

    raw = raw.trimmed().toUpper();
    if (raw == "PLUS" || raw == "PRO" || raw == "MAX" || raw == "FREE") return raw;
    if (raw == "TEAM") return "PRO";
    if (raw.isEmpty()) return "";  // connected but no plan field
    return "";  // unknown value
}

// ── Local data ───────────────────────────────────────────────

RealDataSource::LocalData RealDataSource::readLocalData(const QString &codexDir) const
{
    LocalData out;
    QString dbPath = findSqliteDb(codexDir);
    log("[sqlite] db=" + dbPath + " exists=" + (QFile::exists(dbPath) ? "true" : "false"));

    QDateTime now = QDateTime::currentDateTime();
    QDateTime dayStart(now.date(), QTime(0, 0));
    QDateTime sevenDayStart = dayStart.addDays(-6);
    QDateTime monthStart(QDate(now.date().year(), now.date().month(), 1), QTime(0, 0));
    qint64 dayEpoch = dayStart.toSecsSinceEpoch();
    qint64 activeCutoff = now.addSecs(-2 * 3600).toSecsSinceEpoch();
    qint64 sevenDayEpoch = sevenDayStart.toSecsSinceEpoch();

    // ── SQLite: aggregate tokens ──
    if (!dbPath.isEmpty() && QFile::exists(dbPath)) {
        QString aggSql = QString("SELECT "
            "COALESCE(SUM(tokens_used),0) AS lt,"
            "COALESCE(SUM(CASE WHEN updated_at>=%1 THEN tokens_used ELSE 0 END),0) AS td,"
            "COALESCE(SUM(CASE WHEN updated_at>=%2 THEN tokens_used ELSE 0 END),0) AS wk "
            "FROM threads;").arg(dayEpoch).arg(sevenDayEpoch);

        qint64 total = readSqliteAggregate(dbPath, aggSql);
        // We'll get precise numbers from JSONL parsing instead
        // Just note that SQLite is accessible
        log(QString("[sqlite] aggregate raw=%1").arg(total));

        // ── Thread tasks ──
        readThreadTasks(dbPath, dayEpoch, activeCutoff, out);

        // ── Session sources for JSONL ──
        QVector<SessionSource> sources = readSessionSources(dbPath);
        log(QString("[jsonl] sessionSources=%1").arg(sources.size()));

        if (!sources.isEmpty()) {
            parseJsonlFiles(sources, dayStart, sevenDayStart, monthStart, out);
        } else {
            // No JSONL paths — use SQLite aggregates as fallback
            QString tokensSql = QString("SELECT "
                "COALESCE(SUM(CASE WHEN updated_at>=%1 THEN tokens_used ELSE 0 END),0),"
                "COALESCE(SUM(CASE WHEN updated_at>=%2 THEN tokens_used ELSE 0 END),0),"
                "COALESCE(SUM(tokens_used),0) FROM threads;")
                .arg(dayEpoch).arg(sevenDayEpoch);
            QFile f(dbPath);
            out.errors.append("未找到 session JSONL — token 数据仅来自 SQLite 汇总");
        }
    } else {
        out.errors.append("未找到 state_5.sqlite");
    }

    // ── Automations ──
    QString autoDir = QDir(codexDir).filePath("automations");
    if (QDir(autoDir).exists()) {
        out.scheduled = readAutomations(autoDir, out.tasks);
        log(QString("[tasks] scheduled=%1").arg(out.scheduled));
    }

    return out;
}

// ── SQLite helpers ───────────────────────────────────────────

qint64 RealDataSource::readSqliteAggregate(const QString &dbPath, const QString &sql) const
{
    // Use QSqlDatabase with a unique connection name
    QString connName = "codexu_sqlite_" + QString::number(quintptr(this), 16);
    {
        QSqlDatabase db = QSqlDatabase::addDatabase("QSQLITE", connName);
        db.setDatabaseName(dbPath);
        if (!db.open()) {
            log("[sqlite] open failed: " + db.lastError().text());
            return 0;
        }
        QSqlQuery q(db);
        if (q.exec(sql) && q.next()) {
            qint64 val = q.value(0).toLongLong();
            db.close();
            QSqlDatabase::removeDatabase(connName);
            return val;
        }
        db.close();
    }
    QSqlDatabase::removeDatabase(connName);
    return 0;
}

QVector<RealDataSource::SessionSource> RealDataSource::readSessionSources(const QString &dbPath) const
{
    QVector<SessionSource> out;
    QSet<QString> seen;
    QString connName = "codexu_src_" + QString::number(quintptr(this), 16);
    {
        QSqlDatabase db = QSqlDatabase::addDatabase("QSQLITE", connName);
        db.setDatabaseName(dbPath);
        if (!db.open()) return out;

        QSqlQuery q(db);
        if (q.exec("SELECT rollout_path, model FROM threads WHERE rollout_path IS NOT NULL "
                    "AND rollout_path<>'' AND tokens_used>0 ORDER BY updated_at ASC;")) {
            while (q.next()) {
                QString path = q.value(0).toString();
                if (path.isEmpty() || seen.contains(path)) continue;
                seen.insert(path);
                SessionSource ss;
                ss.rolloutPath = path;
                ss.model = q.value(1).toString();
                out.append(ss);
            }
        }
        db.close();
    }
    QSqlDatabase::removeDatabase(connName);
    return out;
}

void RealDataSource::readThreadTasks(const QString &dbPath, qint64 dayEpoch,
                                      qint64 activeCutoff, LocalData &out) const
{
    QString connName = "codexu_tsk_" + QString::number(quintptr(this), 16);
    {
        QSqlDatabase db = QSqlDatabase::addDatabase("QSQLITE", connName);
        db.setDatabaseName(dbPath);
        if (!db.open()) return;

        // Active + pending (unarchived, today)
        QString todaySql = QString(
            "SELECT id, title, preview, cwd, tokens_used, updated_at, recency_at, archived "
            "FROM threads WHERE archived=0 AND preview<>'' "
            "AND (updated_at>=%1 OR recency_at>=%1 OR created_at>=%1) "
            "ORDER BY recency_at DESC, updated_at DESC LIMIT 12;").arg(dayEpoch);

        QSqlQuery q(db);
        if (q.exec(todaySql)) {
            while (q.next()) {
                QString id = q.value(0).toString();
                QString title = q.value(1).toString();
                if (title.isEmpty()) title = q.value(2).toString();
                if (title.isEmpty()) title = "Codex 会话";
                qint64 tokens = q.value(4).toLongLong();
                qint64 recency = q.value(6).toLongLong();
                if (recency == 0) recency = q.value(5).toLongLong();

                MiniTaskSnapshot t;
                t.title = title.left(60).replace('\n', ' ').replace('\r', ' ');
                t.tokens = tokens;
                t.updatedAt = QDateTime::fromSecsSinceEpoch(recency);

                // Generate code
                QString compact = id.replace("-", "").right(4).toUpper();
                t.code = "COD-" + compact;

                if (recency >= activeCutoff) {
                    t.kind = MiniTaskSnapshot::Active;
                    t.chip = tokens >= 5000000 ? "High" : "Active";
                    out.active++;
                } else {
                    t.kind = MiniTaskSnapshot::Pending;
                    t.chip = tokens >= 2000000 ? "Medium" : "Idle";
                    out.pending++;
                }
                out.tasks.append(t);
            }
        }

        // Done tasks (archived today)
        QString doneSql = QString(
            "SELECT id, title, preview, cwd, tokens_used, "
            "COALESCE(archived_at, updated_at), archived "
            "FROM threads WHERE archived=1 "
            "AND COALESCE(archived_at, updated_at)>=%1 "
            "ORDER BY COALESCE(archived_at, updated_at) DESC LIMIT 6;").arg(dayEpoch);

        if (q.exec(doneSql)) {
            while (q.next()) {
                MiniTaskSnapshot t;
                t.kind = MiniTaskSnapshot::Done;
                t.title = (q.value(1).toString().isEmpty() ? q.value(2).toString() : q.value(1).toString());
                if (t.title.isEmpty()) t.title = "Codex 会话";
                t.title = t.title.left(60).replace('\n', ' ').replace('\r', ' ');
                t.tokens = q.value(4).toLongLong();
                t.updatedAt = QDateTime::fromSecsSinceEpoch(q.value(5).toLongLong());
                t.code = "COD-" + q.value(0).toString().replace("-", "").right(4).toUpper();
                t.chip = "Done";
                t.kind = MiniTaskSnapshot::Done;
                out.tasks.append(t);
                out.done++;
            }
        }
        db.close();
    }
    QSqlDatabase::removeDatabase(connName);
}

// ── JSONL token_count parsing ────────────────────────────────

void RealDataSource::parseJsonlFiles(const QVector<SessionSource> &sources,
                                      const QDateTime &dayStart, const QDateTime &sevenDayStart,
                                      const QDateTime &monthStart, LocalData &out) const
{
    for (const auto &src : sources) {
        QFileInfo fi(src.rolloutPath);
        if (!fi.exists()) continue;

        QFile f(src.rolloutPath);
        if (!f.open(QIODevice::ReadOnly | QIODevice::Text)) continue;

        bool hadEvent = false;
        TokenBreakdown prev;
        QTextStream ts(&f);

        while (!ts.atEnd()) {
            QByteArray line = ts.readLine().toUtf8();
            if (!line.contains("\"type\":\"token_count\"") &&
                !line.contains("\"type\": \"token_count\""))
                continue;

            TokenBreakdown curr;
            QDateTime ts;
            if (!tryParseTokenCount(line, curr, ts)) continue;

            TokenBreakdown delta = curr.delta(prev);
            if (delta.hasNegativeValue()) delta = curr;
            prev = curr;
            if (delta.isZero()) continue;

            double cost = estimateCost(delta, src.model);
            hadEvent = true;
            out.tokenEvents++;

            // Lifetime always accumulates
            out.lifetimeUsage.tokens.input       += delta.input;
            out.lifetimeUsage.tokens.cachedInput += delta.cachedInput;
            out.lifetimeUsage.tokens.output      += delta.output;
            out.lifetimeUsage.estimatedCostUSD   += cost;

            // Window-based accumulation
            if (ts.isValid()) {
                if (ts >= dayStart) {
                    out.todayUsage.tokens.input       += delta.input;
                    out.todayUsage.tokens.cachedInput += delta.cachedInput;
                    out.todayUsage.tokens.output      += delta.output;
                    out.todayUsage.estimatedCostUSD   += cost;
                }
                if (ts >= sevenDayStart) {
                    out.sevenDayUsage.tokens.input       += delta.input;
                    out.sevenDayUsage.tokens.cachedInput += delta.cachedInput;
                    out.sevenDayUsage.tokens.output      += delta.output;
                    out.sevenDayUsage.estimatedCostUSD   += cost;
                }
                if (ts >= monthStart) {
                    out.monthUsage.tokens.input       += delta.input;
                    out.monthUsage.tokens.cachedInput += delta.cachedInput;
                    out.monthUsage.tokens.output      += delta.output;
                    out.monthCost += cost;
                }
            }
        }
        f.close();
        if (hadEvent) out.parsedFiles++;
    }

    // Finalize totals (include cached input)
    out.todayUsage.tokens.total    = out.todayUsage.tokens.input    + out.todayUsage.tokens.output;
    out.sevenDayUsage.tokens.total = out.sevenDayUsage.tokens.input + out.sevenDayUsage.tokens.output;
    out.lifetimeUsage.tokens.total = out.lifetimeUsage.tokens.input + out.lifetimeUsage.tokens.output;
    out.monthUsage.tokens.total    = out.monthUsage.tokens.input    + out.monthUsage.tokens.output;

    out.todayTokens    = out.todayUsage.tokens.visibleTotal();
    out.sevenDayTokens = out.sevenDayUsage.tokens.visibleTotal();
    out.lifetimeTokens = out.lifetimeUsage.tokens.visibleTotal();
}

bool RealDataSource::tryParseTokenCount(const QByteArray &line, TokenBreakdown &tokens,
                                         QDateTime &timestamp) const
{
    QJsonParseError err;
    QJsonDocument doc = QJsonDocument::fromJson(line, &err);
    if (err.error != QJsonParseError::NoError) return false;

    QJsonObject root = doc.object();

    // Timestamp
    QString tsStr = root.value("timestamp").toString();
    if (tsStr.isEmpty()) return false;
    timestamp = QDateTime::fromString(tsStr, Qt::ISODateWithMs);
    if (!timestamp.isValid())
        timestamp = QDateTime::fromString(tsStr, Qt::ISODate);

    // payload.info.total_token_usage
    QJsonObject payload = root.value("payload").toObject();
    QJsonObject info = payload.value("info").toObject();
    QJsonObject usage = info.value("total_token_usage").toObject();
    if (usage.isEmpty()) return false;

    tokens.input           = static_cast<qint64>(usage.value("input_tokens").toDouble());
    tokens.cachedInput     = static_cast<qint64>(usage.value("cached_input_tokens").toDouble());
    tokens.output          = static_cast<qint64>(usage.value("output_tokens").toDouble());
    tokens.reasoningOutput = static_cast<qint64>(usage.value("reasoning_output_tokens").toDouble());
    tokens.total           = static_cast<qint64>(usage.value("total_tokens").toDouble());
    if (tokens.total == 0)
        tokens.total = tokens.input + tokens.output;

    return tokens.total > 0 || tokens.input > 0 || tokens.output > 0;
}

double RealDataSource::estimateCost(const TokenBreakdown &tb, const QString &model) const
{
    // Model pricing lookup — matches C# ModelTokenPrice.For()
    struct Price { double in, cached, out; };
    static const QHash<QString, Price> prices = {
        {"gpt-5.5-pro", {30, 30, 180}},
        {"gpt-5.5",     {5, 0.5, 30}},
        {"gpt-5.4-mini",{0.75, 0.075, 4.5}},
        {"gpt-5.4-nano",{0.2, 0.02, 1.25}},
        {"gpt-5.4-pro", {30, 30, 180}},
        {"gpt-5.4",     {2.5, 0.25, 15}},
        {"gpt-5.3",     {1.75, 0.175, 14}},
        {"gpt-5.2",     {1.75, 0.175, 14}},
        {"gpt-5",       {1.25, 0.125, 10}},
        {"o3",          {2.0, 0.5, 8}},
        {"gpt-4.1",     {2.0, 0.5, 8}},
    };

    Price p{1.25, 0.125, 10}; // default: gpt-5
    QString norm = model.toLower();
    for (auto it = prices.begin(); it != prices.end(); ++it) {
        if (norm.contains(it.key())) { p = it.value(); break; }
    }

    qint64 cached = qMin(qMax(tb.cachedInput, qint64(0)), qMax(tb.input, qint64(0)));
    qint64 uncached = qMax(tb.input - cached, qint64(0));

    return uncached / 1000000.0 * p.in
         + cached   / 1000000.0 * p.cached
         + qMax(tb.output, qint64(0)) / 1000000.0 * p.out;
}

// ── Automations ──────────────────────────────────────────────

int RealDataSource::readAutomations(const QString &autoDir,
                                      QVector<MiniTaskSnapshot> &out) const
{
    int count = 0;
    QDirIterator it(autoDir, {"automation.toml"}, QDir::Files, QDirIterator::Subdirectories);
    while (it.hasNext()) {
        it.next();
        QFileInfo fi = it.fileInfo();
        QFile f(fi.absoluteFilePath());
        if (!f.open(QIODevice::ReadOnly | QIODevice::Text)) continue;

        QString name = fi.dir().dirName();
        MiniTaskSnapshot t;
        t.kind = MiniTaskSnapshot::Scheduled;
        t.title = name;
        t.code = "AUTO-" + name.left(4).toUpper();
        t.chip = "Cron";
        t.updatedAt = fi.lastModified();
        out.append(t);
        count++;
        f.close();
    }
    return count;
}
