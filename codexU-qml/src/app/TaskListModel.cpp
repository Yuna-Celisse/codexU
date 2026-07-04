#include "TaskListModel.h"
#include "FormatUtils.h"
#include <QtCore/QVariantList>

TaskListModel::TaskListModel(QObject *parent)
    : QAbstractListModel(parent) {}

int TaskListModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : m_tasks.size();
}

QVariant TaskListModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() >= m_tasks.size())
        return {};

    const auto &t = m_tasks.at(index.row());

    switch (role) {
    case KindRole:    return static_cast<int>(t.kind);
    case CodeRole:    return t.code;
    case TitleRole:   return t.title;
    case DetailRole:  return t.detail;
    case ChipRole:    return t.chip;
    case AgeStrRole:  return FormatUtils::relativeTime(t.updatedAt);
    default:          return {};
    }
}

QHash<int, QByteArray> TaskListModel::roleNames() const
{
    return {
        { KindRole,   "taskKind" },
        { CodeRole,   "taskCode" },
        { TitleRole,  "taskTitle" },
        { DetailRole, "taskDetail" },
        { ChipRole,   "taskChip" },
        { AgeStrRole, "taskAge" }
    };
}

void TaskListModel::setTasks(const QVector<MiniTaskSnapshot> &tasks)
{
    beginResetModel();
    m_tasks = tasks;
    endResetModel();
}

QVariantList TaskListModel::tasksForKind(int kind) const
{
    QVariantList list;
    for (int i = 0; i < m_tasks.size(); ++i) {
        if (static_cast<int>(m_tasks[i].kind) == kind)
            list.append(QVariant::fromValue(i));
    }
    return list;
}
