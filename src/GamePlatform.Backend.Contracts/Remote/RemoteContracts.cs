using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public readonly struct AccessTokenSnapshot
    {
        public AccessTokenSnapshot(string value, long generation)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentException("A bounded opaque access token is required.", nameof(value));
            if (value.Length > 16_384) throw new ArgumentOutOfRangeException(nameof(value));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Value = value; Generation = generation;
        }
        public string Value { get; }
        public long Generation { get; }
    }

    public interface IAuthSession
    {
        string SessionKey { get; }
        Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken);
        Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken);
    }

    public enum RemoteFailureKind { Cancelled, NotSent, OutcomeUncertain, Authentication, Authorization, Validation, Conflict, RateLimited, Dependency, Protocol, Server, Unavailable }

    public readonly struct RemoteFailure
    {
        public RemoteFailure(RemoteFailureKind kind, int? statusCode = null, TimeSpan? retryAfter = null) { Kind = kind; StatusCode = statusCode; RetryAfter = retryAfter; }
        public RemoteFailureKind Kind { get; }
        public int? StatusCode { get; }
        public TimeSpan? RetryAfter { get; }
    }

    public readonly struct RemoteResult<T>
    {
        private RemoteResult(T value) { IsSuccess = true; Value = value; Failure = default; }
        private RemoteResult(RemoteFailure failure) { IsSuccess = false; Value = default; Failure = failure; }
        public bool IsSuccess { get; }
        public T? Value { get; }
        public RemoteFailure Failure { get; }
        public static RemoteResult<T> Success(T value) => new RemoteResult<T>(value ?? throw new ArgumentNullException(nameof(value)));
        public static RemoteResult<T> Failed(RemoteFailure failure) => new RemoteResult<T>(failure);
    }

    public sealed class ProvisioningSnapshot
    {
        public ProvisioningSnapshot(PlatformUserId accountId, AppId appId, string membershipStatus, ClientStreamId streamId, long nextSequence, long serverTime)
        {
            if (!accountId.IsValid || !appId.IsValid || !streamId.IsValid) throw new ArgumentException("Valid provisioned identities are required.");
            if (string.IsNullOrWhiteSpace(membershipStatus) || membershipStatus.Length > 64) throw new ArgumentOutOfRangeException(nameof(membershipStatus));
            if (nextSequence <= 0) throw new ArgumentOutOfRangeException(nameof(nextSequence));
            if (serverTime < 0 || serverTime > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(serverTime));
            AccountId = accountId; AppId = appId; MembershipStatus = membershipStatus; StreamId = streamId; NextSequence = nextSequence; ServerTime = serverTime;
        }
        public PlatformUserId AccountId { get; }
        public AppId AppId { get; }
        public string MembershipStatus { get; }
        public ClientStreamId StreamId { get; }
        public long NextSequence { get; }
        public long ServerTime { get; }
    }

    public interface IProvisioningRemote
    {
        Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken);
    }

    public sealed class RemoteCommand
    {
        private readonly byte[] semanticBody;private readonly byte[] fingerprint;
        public RemoteCommand(OperationId operationId,ClientStreamId streamId,long sequence,string operationKind,int schemaVersion,int fingerprintVersion,byte[] semanticBody,byte[] fingerprint){if(!operationId.IsValid||!streamId.IsValid||sequence<=0)throw new ArgumentException("Valid command identity is required.");if(string.IsNullOrWhiteSpace(operationKind)||schemaVersion<=0||fingerprintVersion<=0||semanticBody==null||semanticBody.Length==0||fingerprint==null||fingerprint.Length==0)throw new ArgumentException("Complete command semantics are required.");OperationId=operationId;StreamId=streamId;Sequence=sequence;OperationKind=operationKind;SchemaVersion=schemaVersion;FingerprintVersion=fingerprintVersion;this.semanticBody=(byte[])semanticBody.Clone();this.fingerprint=(byte[])fingerprint.Clone();}
        public OperationId OperationId{get;} public ClientStreamId StreamId{get;} public long Sequence{get;} public string OperationKind{get;} public int SchemaVersion{get;} public int FingerprintVersion{get;} public byte[] SemanticBody=>(byte[])semanticBody.Clone(); public byte[] Fingerprint=>(byte[])fingerprint.Clone();
    }
    public enum RemoteCommandStatus{Accepted,TerminalRejected}
    public sealed class RemoteCommandOutcome{private readonly byte[]? result;public RemoteCommandOutcome(RemoteCommandStatus status,byte[]? result){if((result?.Length??0)>65_536)throw new ArgumentOutOfRangeException(nameof(result));Status=status;this.result=result==null?null:(byte[])result.Clone();}public RemoteCommandStatus Status{get;}public byte[]? Result=>result==null?null:(byte[])result.Clone();}
    public interface ICommandRemote{Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command,CancellationToken cancellationToken);}
}
