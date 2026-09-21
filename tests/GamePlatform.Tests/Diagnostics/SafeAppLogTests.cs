using System;
using System.Collections.Generic;
using GamePlatform.Diagnostics.Abstractions;

namespace GamePlatform.Tests.Diagnostics;

public sealed class SafeAppLogTests
{
    private static AppLogContext Context(long generation = 1) => new(Guid.NewGuid(), generation);

    [Fact]
    public void DisabledSinkDoesNotEvaluateFactory()
    {
        var called = false;
        var log = new SafeAppLog(new NullStructuredAppLogSink(), Context());
        log.Write(AppLogLevel.Debug, "Storage", "Commit", () => { called = true; throw new Exception(); });
        Assert.False(called);
        Assert.False(log.IsEnabled(AppLogLevel.Debug, "Storage"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SinkFailureCannotChangeCommittedResult(bool failEnabled)
    {
        var log = new SafeAppLog(new ThrowingSink(failEnabled), Context());
        var committed = true;
        log.Write(AppLogLevel.Information, "Storage", "Committed", () => new Dictionary<string, object?>());
        Assert.True(committed);
    }

    [Fact]
    public void FactoryAndEnumeratorFailuresAreContained()
    {
        var sink = new CollectingSink();
        var log = new SafeAppLog(sink, Context());
        log.Write(AppLogLevel.Error, "Storage", "Failure", () => throw new InvalidOperationException());
        log.Write(AppLogLevel.Error, "Storage", "Failure", BrokenFields);
        Assert.Empty(sink.Records);
    }

    private static IEnumerable<KeyValuePair<string, object?>> BrokenFields()
    {
        yield return new("count", 1);
        throw new InvalidOperationException();
    }

    [Fact]
    public void RedactsStringsSensitiveScalarsObjectsAndExceptionsWithoutFormatting()
    {
        var sink = new CollectingSink();
        var log = new SafeAppLog(sink, Context());
        var fields = new Dictionary<string, object?>
        {
            ["accessToken"] = 123L,
            ["RECEIPT"] = "test-receipt",
            ["description"] = new string('x', 10000),
            ["unknown"] = new ExplodingText(),
            ["count"] = long.MaxValue,
            ["ok"] = true,
            ["notFinite"] = double.NaN,
            ["nothing"] = null
        };
        log.Write(AppLogLevel.Error, "Sync", "Rejected", () => fields, new Exception("private-message"));
        var record = Assert.Single(sink.Records);
        foreach (var name in new[] { "accessToken", "RECEIPT", "description", "unknown", "notFinite" })
            Assert.Equal(SafeAppLog.Redacted, record.Fields[name]);
        Assert.Equal(long.MaxValue, record.Fields["count"]);
        Assert.Equal(true, record.Fields["ok"]);
        Assert.Null(record.Fields["nothing"]);
        Assert.True(record.HasException);
    }

    [Fact]
    public void UnknownNumericSecretsAreDeniedByDefault()
    {
        var sink = new CollectingSink();
        var fields = new Dictionary<string, object?>
        {
            ["apiKey"] = 1234,
            ["sessionId"] = 1234,
            ["deviceId"] = 1234,
            ["pin"] = 1234,
            ["arbitraryNewSecret"] = 1234,
            ["durationMs"] = 25L,
            ["count"] = "test-private"
        };
        new SafeAppLog(sink, Context()).Write(AppLogLevel.Information, "Sync", "Done", () => fields);
        var record = Assert.Single(sink.Records);
        foreach (var name in new[] { "apiKey", "sessionId", "deviceId", "pin", "arbitraryNewSecret", "count" })
            Assert.Equal(SafeAppLog.Redacted, record.Fields[name]);
        Assert.Equal(25L, record.Fields["durationMs"]);
    }

    [Fact]
    public void NullFactoriesAreContainedAndDuplicateKeysUseLastInspectedValue()
    {
        var sink = new CollectingSink();
        var log = new SafeAppLog(sink, Context());
        log.Write(AppLogLevel.Error, "Sync", "Done", null!);
        log.Write(AppLogLevel.Error, "Sync", "Done", () => null!);
        Assert.Empty(sink.Records);
        log.Write(AppLogLevel.Error, "Sync", "Done", () => new[]
        {
            new KeyValuePair<string, object?>("count", 1), new KeyValuePair<string, object?>("count", 2)
        });
        Assert.Equal(2, Assert.Single(sink.Records).Fields["count"]);
    }

    [Fact]
    public void CapturesOwnerAndCopiesFieldsBeforeDelayedDelivery()
    {
        var sink = new CollectingSink();
        var ownerA = Context(1);
        var ownerB = Context(2);
        var fields = new Dictionary<string, object?> { ["count"] = 1L };
        new SafeAppLog(sink, ownerA).Write(AppLogLevel.Information, "Sync", "Done", () => fields);
        fields["count"] = 9L;
        new SafeAppLog(sink, ownerB).Write(AppLogLevel.Information, "Sync", "Done", () => fields);
        Assert.Same(ownerA, sink.Records[0].Context);
        Assert.Same(ownerB, sink.Records[1].Context);
        Assert.Equal(1L, sink.Records[0].Fields["count"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object?>)sink.Records[0].Fields).Add("mutation", 1));
    }

    [Fact]
    public void BoundsEnumerationEvenWithInvalidNames()
    {
        var sink = new CollectingSink();
        var visited = 0;
        IEnumerable<KeyValuePair<string, object?>> Infinite()
        {
            while (true) { visited++; yield return new(new string('x', 1000), 1); }
        }
        new SafeAppLog(sink, Context()).Write(AppLogLevel.Information, "Sync", "Done", Infinite);
        Assert.Equal(SafeAppLog.MaximumFields, visited);
        Assert.Empty(Assert.Single(sink.Records).Fields);
    }

    [Fact]
    public void BoundsValidFieldsAndRejectsInvalidMetadata()
    {
        var sink = new CollectingSink();
        var log = new SafeAppLog(sink, Context());
        var fields = new Dictionary<string, object?>();
        for (var i = 0; i < 100; i++) fields.Add("field" + i, i);
        log.Write(AppLogLevel.Debug, "Sync", "Done", () => fields);
        Assert.Equal(SafeAppLog.MaximumFields, Assert.Single(sink.Records).Fields.Count);
        log.Write((AppLogLevel)99, "Sync", "Done", () => throw new Exception());
        log.Write(AppLogLevel.Debug, "Sync\nprivate", "Done", () => throw new Exception());
        log.Write(AppLogLevel.Debug, "Sync", new string('a', 65), () => throw new Exception());
        Assert.Single(sink.Records);
    }

    [Fact]
    public void LegacyCallersRemainCompatibleAndSanitized()
    {
        var sink = new CollectingSink();
        IAppLog log = new SafeAppLog(sink, Context());
        log.Write(AppLogLevel.Error, "Failed", new Dictionary<string, object?> { ["message"] = "private" });
        Assert.Equal("Legacy", Assert.Single(sink.Records).Category);
        Assert.Equal(SafeAppLog.Redacted, sink.Records[0].Fields["message"]);
        NullAppLog.Instance.Write(AppLogLevel.Error, "Failed", new Dictionary<string, object?>());
    }

    [Fact]
    public void InvalidConstructionFailsBeforeBusinessWork()
    {
        Assert.Throws<ArgumentException>(() => new AppLogContext(Guid.Empty, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppLogContext(Guid.NewGuid(), -1));
        Assert.Throws<ArgumentNullException>(() => new SafeAppLog(null!, Context()));
        Assert.Throws<ArgumentNullException>(() => new SafeAppLog(new CollectingSink(), null!));
    }

    private sealed class CollectingSink : IStructuredAppLogSink
    {
        public List<AppLogRecord> Records { get; } = new();
        public bool IsEnabled(AppLogLevel level, string category) => true;
        public void Write(AppLogRecord record) => Records.Add(record);
    }
    private sealed class ThrowingSink(bool failEnabled) : IStructuredAppLogSink
    {
        public bool IsEnabled(AppLogLevel level, string category) => failEnabled ? throw new Exception() : true;
        public void Write(AppLogRecord record) => throw new Exception();
    }
    private sealed class ExplodingText
    {
        public override string ToString() => throw new Exception("Must never format payloads");
    }
}
