using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Store;
namespace GamePlatform.Tests.Features.StoreQuest
{
    public sealed class StorefrontServiceTests
    {
        [Fact]
        public async Task RetiredServiceFencesLateRemoteResult()
        {
            var owner = new ScopedOwnerContext(new OwnerScope(new BackendNamespace("test"),
                new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001")),
                new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"))), new SemanticId("private"), 1);
            var remote = new DelayedRemote();
            var service = new StorefrontService(owner, remote);
            var pending = service.FetchAsync(owner, CancellationToken.None);
            service.Dispose();
            remote.Complete();
            await Assert.ThrowsAnyAsync<Exception>(() => pending);
        }
        private sealed class DelayedRemote : IStorefrontRemote
        {
            private readonly TaskCompletionSource<RemoteResult<IReadOnlyList<StorefrontOffer>>> result =
                new TaskCompletionSource<RemoteResult<IReadOnlyList<StorefrontOffer>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<RemoteResult<IReadOnlyList<StorefrontOffer>>> FetchAsync(CancellationToken cancellationToken) => result.Task;
            public void Complete() => result.SetResult(RemoteResult<IReadOnlyList<StorefrontOffer>>.Success(Array.Empty<StorefrontOffer>()));
        }
    }
}
