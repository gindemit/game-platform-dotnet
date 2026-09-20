#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Transport.Http
{
    /// <summary>Read-only production transport for the approved G3 reward-receipt v2 carrier.</summary>
    public sealed class G3RewardReceiptHttpProvider
    {
        private readonly AppId appId; private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration; private readonly IHttpExecutor executor;
        private readonly G3RewardReceiptMessagePackCodec codec;private readonly IWireCodec errorCodec; private readonly IAuthSession auth; private readonly AuthRefreshCoordinator refresh;
        public G3RewardReceiptHttpProvider(AppId appId,PlatformUserId accountId,BackendHttpConfiguration configuration,IHttpExecutor executor,G3RewardReceiptMessagePackCodec codec,IWireCodec errorCodec,IAuthSession auth,AuthRefreshCoordinator refresh)
        {
            if(!appId.IsValid)throw new ArgumentException("A valid app ID is required.",nameof(appId));
            if(!accountId.IsValid)throw new ArgumentException("A valid account ID is required.",nameof(accountId));
            this.appId=appId;this.accountId=accountId;this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));this.executor=executor??throw new ArgumentNullException(nameof(executor));this.codec=codec??throw new ArgumentNullException(nameof(codec));this.errorCodec=errorCodec??throw new ArgumentNullException(nameof(errorCodec));this.auth=auth??throw new ArgumentNullException(nameof(auth));this.refresh=refresh??throw new ArgumentNullException(nameof(refresh));
        }
        public async Task<RemoteResult<IG3RewardReceiptResponse>> LookupAsync(OperationId operationId,CancellationToken cancellationToken)
        {
            if(!operationId.IsValid)throw new ArgumentException("A valid operation is required.",nameof(operationId));
            var attempt=await SendAuthenticatedAsync(operationId,cancellationToken).ConfigureAwait(false);
            if(attempt.Failure.HasValue)return RemoteResult<IG3RewardReceiptResponse>.Failed(attempt.Failure.Value);
            var response=attempt.Response!;
            if(response.StatusCode!=200)return RemoteResult<IG3RewardReceiptResponse>.Failed(DecodeFailure(response));
            if(!Valid(response))return Protocol(response.StatusCode);
            try
            {
                var value=codec.DecodeResponse(response.CopyBody());
                var returned=value is G3RewardReceiptNotFoundResponse missing?missing.OperationId:value is G3RewardReceiptCompletionResponse completed?completed.OperationId:Guid.Empty;
                if(returned!=operationId.Value)return Protocol(response.StatusCode);
                return RemoteResult<IG3RewardReceiptResponse>.Success(value);
            }
            catch{return Protocol(response.StatusCode);}
        }
        private async Task<Attempt> SendAuthenticatedAsync(OperationId operationId,CancellationToken token)
        {
            AccessTokenSnapshot access;
            try{access=await auth.GetAsync(token).ConfigureAwait(false);}catch(OperationCanceledException)when(token.IsCancellationRequested){return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled));}catch{return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication));}
            var result=await SendOnce(operationId,access,token).ConfigureAwait(false);if(result.Failure.HasValue||result.Response!.StatusCode!=401)return result;
            try{access=await refresh.RefreshAsync(auth,accountId,access.Generation,token).ConfigureAwait(false);}catch(OperationCanceledException)when(token.IsCancellationRequested){return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled));}catch{return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication,401));}
            return await SendOnce(operationId,access,token).ConfigureAwait(false);
        }
        private async Task<Attempt> SendOnce(OperationId operationId,AccessTokenSnapshot access,CancellationToken token)
        {
            var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"Authorization","Bearer "+access.Value},{"Accept",PrivateSyncHttpProvider.MessagePackMediaType}};
            var request=new HttpRequestData("GET",configuration.Resolve("v1/apps/"+appId+"/gameplay/reward-receipts/"+operationId).AbsoluteUri,headers,Array.Empty<byte>());
            try{return new Attempt(await executor.SendAsync(request,token).ConfigureAwait(false));}catch(OperationCanceledException)when(token.IsCancellationRequested){return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled));}catch(HttpExecutionException error){return new Attempt(new RemoteFailure(error.Certainty==HttpDeliveryCertainty.NotSent?RemoteFailureKind.NotSent:RemoteFailureKind.Dependency));}catch{return new Attempt(new RemoteFailure(RemoteFailureKind.Dependency));}
        }
        private bool Valid(HttpResponseData response)=>response.BodyLength>0&&response.BodyLength<=configuration.MaximumResponseBytes&&response.TryGetHeader("Content-Type",out var type)&&string.Equals(type,PrivateSyncHttpProvider.MessagePackMediaType,StringComparison.OrdinalIgnoreCase)&&(!response.TryGetHeader("Content-Encoding",out var encoding)||string.Equals(encoding,"identity",StringComparison.OrdinalIgnoreCase))&&response.TryGetHeader("X-Correlation-Id",out var correlation)&&!string.IsNullOrWhiteSpace(correlation)&&correlation.Length<=128;
        private RemoteFailure DecodeFailure(HttpResponseData response){var mapped=MapFailure(response.StatusCode);if(!Valid(response))return new RemoteFailure(RemoteFailureKind.Protocol,response.StatusCode);try{var error=errorCodec.Decode<ErrorResponse>(response.CopyBody());if(error==null||error.ProtocolVersion!=1||string.IsNullOrWhiteSpace(error.CorrelationId)||!response.TryGetHeader("X-Correlation-Id",out var correlation)||!string.Equals(error.CorrelationId,correlation,StringComparison.Ordinal))return new RemoteFailure(RemoteFailureKind.Protocol,response.StatusCode);return mapped;}catch{return new RemoteFailure(RemoteFailureKind.Protocol,response.StatusCode);}}
        private static RemoteFailure MapFailure(int status)=>new RemoteFailure(status==401?RemoteFailureKind.Authentication:status==403?RemoteFailureKind.Authorization:status==404?RemoteFailureKind.Protocol:status==409?RemoteFailureKind.Conflict:status==429?RemoteFailureKind.RateLimited:status>=500?RemoteFailureKind.Server:RemoteFailureKind.Validation,status);
        private static RemoteResult<IG3RewardReceiptResponse> Protocol(int? status=null)=>RemoteResult<IG3RewardReceiptResponse>.Failed(new RemoteFailure(RemoteFailureKind.Protocol,status));
        private readonly struct Attempt{public Attempt(HttpResponseData response){Response=response;Failure=null;}public Attempt(RemoteFailure failure){Response=null;Failure=failure;}public HttpResponseData? Response{get;}public RemoteFailure? Failure{get;}}
    }
}
