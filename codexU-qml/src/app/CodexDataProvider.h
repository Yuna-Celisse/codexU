#pragma once

#include "Models.h"
#include "DataSourceInterface.h"
#include "TaskListModel.h"
#include <QtCore/QObject>
#include <QtCore/QTimer>
#include <QtCore/QVariantMap>
#include <QtCore/QScopedPointer>

class CodexDataProvider : public QObject {
    Q_OBJECT

    Q_PROPERTY(QString accountPlan   READ accountPlan   NOTIFY dataChanged)
    Q_PROPERTY(QString refreshedAt   READ refreshedAt   NOTIFY dataChanged)
    Q_PROPERTY(bool    loading       READ loading       NOTIFY loadingChanged)

    Q_PROPERTY(double primaryRemainingPercent   READ primaryRemainingPercent   NOTIFY dataChanged)
    Q_PROPERTY(double secondaryRemainingPercent READ secondaryRemainingPercent NOTIFY dataChanged)
    Q_PROPERTY(QString primaryResetText         READ primaryResetText          NOTIFY dataChanged)
    Q_PROPERTY(QString secondaryResetText       READ secondaryResetText        NOTIFY dataChanged)

    Q_PROPERTY(QVariantMap todayUsage     READ todayUsage     NOTIFY dataChanged)
    Q_PROPERTY(QVariantMap sevenDayUsage  READ sevenDayUsage  NOTIFY dataChanged)
    Q_PROPERTY(QVariantMap lifetimeUsage  READ lifetimeUsage  NOTIFY dataChanged)

    Q_PROPERTY(double monthEstimatedCost READ monthEstimatedCost NOTIFY dataChanged)
    Q_PROPERTY(QString valueProgressText READ valueProgressText  NOTIFY dataChanged)

    Q_PROPERTY(int activeTasks    READ activeTasks    NOTIFY dataChanged)
    Q_PROPERTY(int pendingTasks   READ pendingTasks   NOTIFY dataChanged)
    Q_PROPERTY(int scheduledTasks READ scheduledTasks NOTIFY dataChanged)
    Q_PROPERTY(int doneTasks      READ doneTasks      NOTIFY dataChanged)
    Q_PROPERTY(TaskListModel* taskList READ taskList CONSTANT)

    Q_PROPERTY(QStringList messages READ messages NOTIFY dataChanged)
    Q_PROPERTY(QString dataSource   READ dataSource   NOTIFY dataChanged)
    Q_PROPERTY(bool    useMockData  READ useMockData  CONSTANT)

public:
    explicit CodexDataProvider(bool useMock = false, QObject *parent = nullptr);

    // Getters
    QString  accountPlan() const;
    QString  refreshedAt() const;
    bool     loading() const;

    double   primaryRemainingPercent() const;
    double   secondaryRemainingPercent() const;
    QString  primaryResetText() const;
    QString  secondaryResetText() const;

    QVariantMap todayUsage() const;
    QVariantMap sevenDayUsage() const;
    QVariantMap lifetimeUsage() const;

    double   monthEstimatedCost() const;
    QString  valueProgressText() const;

    int      activeTasks() const;
    int      pendingTasks() const;
    int      scheduledTasks() const;
    int      doneTasks() const;
    TaskListModel* taskList() const { return m_taskModel; }

    QStringList messages() const;
    QString  dataSource() const;    // "real", "mock", "empty", "partial", "error"
    bool     useMockData() const { return m_useMock; }

public slots:
    void refresh();

signals:
    void dataChanged();
    void loadingChanged();

private:
    void applySnapshot(const UsageSnapshot &snap);
    bool validateSnapshot(const UsageSnapshot &snap) const;
    static QVariantMap tokenUsageToMap(const TokenUsageSummary &u);

    UsageSnapshot m_snapshot;
    bool m_loading = false;
    bool m_useMock = false;
    QString m_dataSource = "empty";
    QTimer *m_refreshTimer = nullptr;
    TaskListModel *m_taskModel = nullptr;
    QScopedPointer<IDataSource> m_source;
};
