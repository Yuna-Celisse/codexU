#pragma once

#include "Models.h"
#include <QtCore/QAbstractListModel>
#include <QtCore/QVector>

// QAbstractListModel exposing MiniTaskSnapshot for QML ListView / Repeater.
// Roles: kind, code, title, detail, chip, ageStr
class TaskListModel : public QAbstractListModel {
    Q_OBJECT

public:
    enum Role {
        KindRole    = Qt::UserRole + 1,
        CodeRole,
        TitleRole,
        DetailRole,
        ChipRole,
        AgeStrRole
    };

    explicit TaskListModel(QObject *parent = nullptr);

    int rowCount(const QModelIndex &parent = QModelIndex()) const override;
    QVariant data(const QModelIndex &index, int role = Qt::DisplayRole) const override;
    QHash<int, QByteArray> roleNames() const override;

    void setTasks(const QVector<MiniTaskSnapshot> &tasks);

    // Convenience: filter by kind
    Q_INVOKABLE QVariantList tasksForKind(int kind) const;

private:
    QVector<MiniTaskSnapshot> m_tasks;
};
