#pragma once

#include "Models.h"

// Pure virtual data-source interface.
// Swap MockDataSource ↔ RealDataSource without touching QML.
class IDataSource {
public:
    virtual ~IDataSource() = default;
    virtual UsageSnapshot load() = 0;
};
