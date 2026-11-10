using Portal.Api;
using Xunit;

namespace Portal.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "portal-tests-" + Guid.NewGuid());
    private readonly Store store;
    public StoreTests() { store = new Store(Path.Combine(directory, "test.db")); store.Initialize(); }
    private static CreateRequest Input(string reference = "REF-1") => new(reference, "C-1", "VISIT", "Revisar equipo");
    [Fact] public void DuplicateReturnsSameRequestAndConflictRejectsDifferentPayload()
    {
        var first = store.Create(Input()); var retry = store.Create(Input());
        Assert.True(first.Created); Assert.False(retry.Created); Assert.Equal(first.Request.Id, retry.Request.Id);
        Assert.Throws<ReferenceConflictException>(() => store.Create(Input() with { CustomerId = "OTHER" }));
    }
    [Fact] public async Task ConcurrentDuplicateCreatesOnlyOneRow()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => store.Create(Input()))));
        Assert.Single(results, r => r.Created); Assert.Single(store.List(null));
    }
    [Fact] public async Task ConcurrentWorkersHaveOnlyOneClaim()
    {
        store.Create(Input());
        var claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => store.Take(1000))));
        Assert.Single(claims, c => c is not null);
    }
    [Fact] public void CrashAfterProviderEffectRecoversWithoutDuplicateAndFencesStaleWorker()
    {
        var request = store.Create(Input()).Request;
        var first = store.Take(1000)!; Assert.True(store.InvokeProvider(first, 1000));
        Assert.Null(store.Take(2000));
        var recovered = store.Take(31_001)!;
        Assert.False(store.Finish(first, false, 31_002));
        Assert.True(store.InvokeProvider(recovered, 31_003)); Assert.True(store.Finish(recovered, true, 31_004));
        Assert.Equal(1, store.EffectCount()); Assert.Equal("Completed", store.Get(request.Id)!.Status);
        Assert.Null(store.Take(99_000)); Assert.False(store.Finish(recovered, false, 99_001));
    }
    [Theory] [InlineData("RETRY-1", "Completed", 1)] [InlineData("FAIL-1", "Failed", 0)]
    public void TransientFailuresRespectBackoffAndAttemptLimit(string reference, string expected, int effects)
    {
        var request = store.Create(Input(reference)).Request;
        long now = 1000;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var claim = store.Take(now)!; Assert.Equal(attempt, claim.Request.Attempts);
            store.Finish(claim, store.InvokeProvider(claim, now), now);
            Assert.Null(store.Take(now + 1)); now += 10_000;
        }
        Assert.Equal(expected, store.Get(request.Id)!.Status); Assert.Equal(effects, store.EffectCount()); Assert.Null(store.Take(now));
    }
    [Fact] public void RestartPreservesRequestAndEffect()
    {
        var request = store.Create(Input()).Request; var claim = store.Take(1000)!;
        store.Finish(claim, store.InvokeProvider(claim, 1000), 1000);
        var reopened = new Store(Path.Combine(directory, "test.db")); reopened.Initialize();
        Assert.Equal("Completed", reopened.Get(request.Id)!.Status); Assert.Equal(1, reopened.EffectCount());
    }
    [Fact] public void DatabaseRejectsCompletedRegression()
    {
        var request = store.Create(Input()).Request; var claim = store.Take(1000)!;
        store.Finish(claim, true, 1000);
        using var cn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(directory, "test.db")}");
        cn.Open(); using var cmd = cn.CreateCommand(); cmd.CommandText = "UPDATE requests SET status='Pending'";
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
        Assert.Equal("Completed", store.Get(request.Id)!.Status);
    }
    [Fact] public void ListFiltersAndPages()
    {
        store.Create(Input()); store.Create(Input("REF-2")); var claim = store.Take(1000)!;
        store.Finish(claim, true, 1000); Assert.Single(store.List("Completed")); Assert.Single(store.List("Pending"));
        Assert.Single(store.List(null, 1, 1)); Assert.Empty(store.List("Failed"));
    }
    [Fact] public void ValidationRejectsMissingOversizedAndControlCharacters()
    {
        Assert.Empty(Portal.Api.Input.Validate(Input()));
        Assert.Equal(3, Portal.Api.Input.Validate(Input() with { ExternalReference = "", CustomerId = new string('a', 101), Description = "bad\ntext" }).Count);
        Assert.Equal("REF-1", Portal.Api.Input.Normalize(Input(" REF-1 ")).ExternalReference);
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
}
