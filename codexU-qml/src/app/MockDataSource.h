#pragma once

#include "DataSourceInterface.h"

// Generates plausible fake usage data for UI development and testing.
// This lets the entire QML UI be built and verified without requiring
// a real Codex installation or app-server.
class MockDataSource : public IDataSource {
public:
    UsageSnapshot load() override;
};
