using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// xUnit Collection：禁用跨类并行
///
/// 背景：DefaultCloudSyncOrchestratorTests + MainViewModelOrchestratorIntegrationTests
/// 都在 per-test temp .db 文件上跑 SQLite，跨类并行时 Windows 上偶发
/// "The process cannot access the file ... because it is being used by another process"
/// （SQLite 文件锁竞争 + xUnit 默认跨类并行 = 偶发 IOException）。
///
/// Fix：两 class 都加 [Collection("SequentialSQLite")]，
/// xUnit 把同 collection 的 tests 串行执行（同一时间只跑一个 test instance）。
/// 其他 tests 不受影响，继续并行。
/// </summary>
[CollectionDefinition("SequentialSQLite", DisableParallelization = true)]
public class SequentialSQLiteCollection
{
}