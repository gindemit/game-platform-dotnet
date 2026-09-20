#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
using Xunit;

namespace GamePlatform.Tests.Transport
{
    public sealed class G3RewardReceiptHttpProviderTests
    {
        private static readonly AppId App=new AppId(Guid.Parse("0199f9a0-0700-7000-8000-000000000001"));
        private static readonly PlatformUserId Account=new PlatformUserId(Guid.Parse("0199f9a0-0700-7000-8000-000000000002"));
        private static readonly OperationId Operation=new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000010"));
        [Fact] public async Task ProductionProviderReadsApprovedReceiptWithoutRequestBody()
        {
            var codec=new G3RewardReceiptMessagePackCodec();var executor=new Scripted(_=>Response(codec.EncodeResponse(Completed(Operation.Value))));using var refresh=new AuthRefreshCoordinator();var provider=Provider(executor,codec,refresh);
            var result=await provider.LookupAsync(Operation,CancellationToken.None);
            Assert.True(result.IsSuccess);var completed=Assert.IsType<G3RewardReceiptCompletionResponse>(result.Value);Assert.NotNull(completed.Reward);Assert.Single(executor.Requests);Assert.Equal("GET",executor.Requests[0].Method);Assert.Equal(0,executor.Requests[0].BodyLength);Assert.EndsWith("/gameplay/reward-receipts/"+Operation,executor.Requests[0].Uri,StringComparison.Ordinal);
        }
        [Fact] public async Task NotFoundAndCompletionWithoutRewardRemainDistinct()
        {
            var codec=new G3RewardReceiptMessagePackCodec();using var firstRefresh=new AuthRefreshCoordinator();var first=await Provider(new Scripted(_=>Response(codec.EncodeResponse(new G3RewardReceiptNotFoundResponse(Operation.Value)))),codec,firstRefresh).LookupAsync(Operation,CancellationToken.None);Assert.IsType<G3RewardReceiptNotFoundResponse>(first.Value);
            using var secondRefresh=new AuthRefreshCoordinator();var second=await Provider(new Scripted(_=>Response(codec.EncodeResponse(new G3RewardReceiptCompletionResponse(Operation.Value,null)))),codec,secondRefresh).LookupAsync(Operation,CancellationToken.None);Assert.Null(Assert.IsType<G3RewardReceiptCompletionResponse>(second.Value).Reward);
        }
        [Fact] public async Task WrongOperationAndMalformedAuthorityFailClosed()
        {
            var codec=new G3RewardReceiptMessagePackCodec();using var firstRefresh=new AuthRefreshCoordinator();var wrong=await Provider(new Scripted(_=>Response(codec.EncodeResponse(new G3RewardReceiptNotFoundResponse(Guid.Parse("0199f9a0-0700-7000-8000-000000000099"))))),codec,firstRefresh).LookupAsync(Operation,CancellationToken.None);Assert.False(wrong.IsSuccess);Assert.Equal(RemoteFailureKind.Protocol,wrong.Failure!.Kind);
            var malformed=new byte[]{0x81,0xa1,0x78,0x01};
            using var secondRefresh=new AuthRefreshCoordinator();var rejected=await Provider(new Scripted(_=>Response(malformed)),codec,secondRefresh).LookupAsync(Operation,CancellationToken.None);Assert.False(rejected.IsSuccess);Assert.Equal(RemoteFailureKind.Protocol,rejected.Failure!.Kind);
        }
        private static G3RewardReceiptHttpProvider Provider(IHttpExecutor executor,G3RewardReceiptMessagePackCodec codec,AuthRefreshCoordinator refresh)=>new G3RewardReceiptHttpProvider(App,Account,new BackendHttpConfiguration(new Uri("https://api.example.test/platform"),new BackendNamespace("test")),executor,codec,new MessagePackWireCodec(),new Auth(),refresh);
        private static G3RewardReceiptCompletionResponse Completed(Guid operation)=>new G3RewardReceiptCompletionResponse(operation,new LiveSliceRewardReceipt(Guid.Parse("0199f9a0-0700-7000-8000-000000000011"),operation,10,100,new LiveSliceRewardSource("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","completion","mrsquare.test.coin",1),"mrsquare.test.coin",1,new ILiveSliceRewardLine[]{new LiveSliceCurrencyRewardLine(0,1,"test.coin",1)}));
        private static HttpResponseData Response(byte[] body)=>new HttpResponseData(200,new Dictionary<string,string>{{"Content-Type",PrivateSyncHttpProvider.MessagePackMediaType},{"X-Correlation-Id","test"}},body);
        private sealed class Scripted:IHttpExecutor{private readonly Func<HttpRequestData,HttpResponseData> send;public Scripted(Func<HttpRequestData,HttpResponseData> send){this.send=send;}public List<HttpRequestData> Requests{get;}=new List<HttpRequestData>();public Task<HttpResponseData> SendAsync(HttpRequestData request,CancellationToken token){Requests.Add(request);return Task.FromResult(send(request));}}
        private sealed class Auth:IAuthSession{public string SessionKey=>"g3-receipt-test";public Task<AccessTokenSnapshot> GetAsync(CancellationToken token)=>Task.FromResult(new AccessTokenSnapshot("token",0));public Task<AccessTokenSnapshot> RefreshAsync(long generation,CancellationToken token)=>Task.FromResult(new AccessTokenSnapshot("token",generation+1));}
    }
}
