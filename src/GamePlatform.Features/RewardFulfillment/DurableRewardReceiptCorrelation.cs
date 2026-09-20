#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>One value projection actually installed from a committed feed group. It deliberately carries no quantity.</summary>
    public sealed class InstalledRewardProjection
    {
        public InstalledRewardProjection(RewardReceiptLineKind kind,PlatformId resourceId,long projectionRevision){if(!Enum.IsDefined(typeof(RewardReceiptLineKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));if(!resourceId.IsValid)throw new ArgumentException("A resource is required.",nameof(resourceId));if(projectionRevision<=0)throw new ArgumentOutOfRangeException(nameof(projectionRevision));Kind=kind;ResourceId=resourceId;ProjectionRevision=projectionRevision;}
        public RewardReceiptLineKind Kind{get;}public PlatformId ResourceId{get;}public long ProjectionRevision{get;}
    }

    /// <summary>
    /// Durable join between an immutable receipt and the exact installed private-feed revision. Feed staging runs in
    /// the caller's projection/cursor transaction; confirmation runs only after that transaction commits. This type
    /// never increments a projection revision or applies receipt quantities to local value.
    /// </summary>
    public sealed class DurableRewardReceiptCorrelation
    {
        private const string Namespace="reward-correlation";private readonly ScopedOwnerContext owner;private readonly StorageScope scope;private readonly IDurableFeatureStateStore state;private readonly ISerializedStorageExecutor transactions;private readonly RewardFulfillmentService fulfillment;private readonly Func<long> now;private readonly DurableRewardFulfillmentStateCodec codec=new DurableRewardFulfillmentStateCodec();
        public DurableRewardReceiptCorrelation(ScopedOwnerContext owner,StorageScope scope,IDurableFeatureStateStore state,ISerializedStorageExecutor transactions,RewardFulfillmentService fulfillment,Func<long> nowMilliseconds){if(!owner.IsValid)throw new ArgumentException("A captured owner is required.",nameof(owner));this.owner=owner;this.scope=scope;this.state=state??throw new ArgumentNullException(nameof(state));this.transactions=transactions??throw new ArgumentNullException(nameof(transactions));this.fulfillment=fulfillment??throw new ArgumentNullException(nameof(fulfillment));this.now=nowMilliseconds??throw new ArgumentNullException(nameof(nowMilliseconds));}
        public async Task ObserveReceiptAsync(RewardReceiptObservation observation,CancellationToken token)
        {
            if(observation==null)throw new ArgumentNullException(nameof(observation));EnsureOwner(observation.Owner);await fulfillment.ObserveAcceptedReceiptAsync(observation,token).ConfigureAwait(false);
            await transactions.ExecuteAsync(scope,transaction=>{var payload=codec.EncodeRecord(new RewardPresentationRecord(observation.OperationId,observation.Receipt.BusinessSource,RewardPresentationStatus.AcceptedAwaitingPull,observation.Receipt,null,false));InsertOrVerify(transaction,ReceiptKey(observation.Receipt.FeedRevision),observation.Receipt.FeedRevision,payload);var feed=state.Read(transaction,owner,Namespace,FeedKey(observation.Receipt.FeedRevision));if(feed!=null)fulfillment.StageProjectionGroup(transaction,Join(observation,DecodeFeed(feed.CopyPayload()),observation.Receipt.FeedRevision));return true;},token).ConfigureAwait(false);
            await ReconcileAsync(observation.Receipt.FeedRevision,token).ConfigureAwait(false);
        }
        /// <summary>Call after all listed component upserts are installed, inside the same feed/cursor transaction.</summary>
        public void StageInstalledFeed(ILocalStorageTransaction transaction,long feedRevision,IReadOnlyList<InstalledRewardProjection> projections)
        {
            if(transaction==null)throw new ArgumentNullException(nameof(transaction));if(!transaction.Scope.Equals(scope))throw new RewardFulfillmentOwnerMismatchException();if(feedRevision<=0)throw new ArgumentOutOfRangeException(nameof(feedRevision));var exact=Validate(projections);InsertOrVerify(transaction,FeedKey(feedRevision),feedRevision,EncodeFeed(exact));var receipt=state.Read(transaction,owner,Namespace,ReceiptKey(feedRevision));if(receipt!=null){var record=codec.DecodeRecord(receipt.Revision,receipt.CopyPayload());fulfillment.StageProjectionGroup(transaction,Join(new RewardReceiptObservation(owner,record.OperationId,record.Receipt!),exact,feedRevision));}
        }
        /// <summary>Call only after the feed transaction committed; exact replay is safe.</summary>
        public async Task ReconcileAsync(long feedRevision,CancellationToken token)
        {
            if(feedRevision<=0)throw new ArgumentOutOfRangeException(nameof(feedRevision));var receipt=await state.ReadAsync(owner,Namespace,ReceiptKey(feedRevision),token).ConfigureAwait(false);var feed=await state.ReadAsync(owner,Namespace,FeedKey(feedRevision),token).ConfigureAwait(false);if(receipt==null||feed==null)return;var record=codec.DecodeRecord(receipt.Revision,receipt.CopyPayload());await fulfillment.ObserveProjectionGroupAsync(Join(new RewardReceiptObservation(owner,record.OperationId,record.Receipt!),DecodeFeed(feed.CopyPayload()),feedRevision),token).ConfigureAwait(false);
        }
        private RewardProjectionGroup Join(RewardReceiptObservation observation,IReadOnlyList<InstalledRewardProjection> projections,long feedRevision)
        {
            if(observation.Receipt.FeedRevision!=feedRevision||projections.Count!=observation.Receipt.Lines.Count)throw new RewardFulfillmentConflictException("The exact installed feed revision does not match the receipt.");var remaining=projections.ToList();var lines=new List<RewardProjectionLine>();foreach(var receiptLine in observation.Receipt.Lines){var matches=remaining.Where(value=>value.Kind==receiptLine.Kind&&value.ResourceId.Equals(receiptLine.ResourceId)).ToArray();if(matches.Length!=1)throw new RewardFulfillmentConflictException("The installed feed group is partial or ambiguous.");var match=matches[0];remaining.Remove(match);lines.Add(new RewardProjectionLine(receiptLine.LineIndex,receiptLine.Kind,receiptLine.ResourceId,match.ProjectionRevision));}if(remaining.Count!=0)throw new RewardFulfillmentConflictException("The installed feed group contains unrelated value projections.");return new RewardProjectionGroup(owner,observation.OperationId,observation.Receipt.GrantId,observation.Receipt.BusinessSource,feedRevision,lines);
        }
        private void InsertOrVerify(ILocalStorageTransaction transaction,string key,long revision,byte[] payload){var existing=state.Read(transaction,owner,Namespace,key);if(existing==null){state.Upsert(transaction,new DurableFeatureMutation(owner,Namespace,key,revision,Now(),payload,Array.Empty<byte>()));return;}if(existing.Revision!=revision||!Same(existing.CopyPayload(),payload))throw new RewardFulfillmentConflictException("Durable reward correlation cannot be replaced.");}
        private static InstalledRewardProjection[] Validate(IReadOnlyList<InstalledRewardProjection> values){if(values==null)throw new ArgumentNullException(nameof(values));if(values.Count==0||values.Count>64||values.Any(value=>value==null))throw new ArgumentException("Installed reward projections must be bounded.",nameof(values));var result=values.ToArray();if(result.GroupBy(value=>((int)value.Kind)+":"+value.ResourceId.Value,StringComparer.Ordinal).Any(group=>group.Count()!=1))throw new RewardFulfillmentConflictException("Installed reward projection identities must be unique.");return result;}
        private static byte[] EncodeFeed(IEnumerable<InstalledRewardProjection> values){using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true)){writer.Write(1);var array=values.ToArray();writer.Write(array.Length);foreach(var value in array){writer.Write((byte)value.Kind);var bytes=Encoding.UTF8.GetBytes(value.ResourceId.Value);writer.Write(bytes.Length);writer.Write(bytes);writer.Write(value.ProjectionRevision);}}return stream.ToArray();}
        private static InstalledRewardProjection[] DecodeFeed(byte[] payload){try{using var stream=new MemoryStream(payload,false);using var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true);if(reader.ReadInt32()!=1)throw new InvalidDataException();var count=reader.ReadInt32();if(count<=0||count>64)throw new InvalidDataException();var result=new InstalledRewardProjection[count];for(var i=0;i<count;i++){var kind=(RewardReceiptLineKind)reader.ReadByte();var length=reader.ReadInt32();if(length<=0||length>512)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new InvalidDataException();result[i]=new InstalledRewardProjection(kind,new PlatformId(new UTF8Encoding(false,true).GetString(bytes)),reader.ReadInt64());}if(stream.Position!=stream.Length)throw new InvalidDataException();return Validate(result);}catch(RewardFulfillmentConflictException){throw;}catch(Exception error){throw new RewardFulfillmentConflictException("Durable feed correlation is invalid: "+error.GetType().Name);}}
        private void EnsureOwner(ScopedOwnerContext value){if(value!=owner)throw new RewardFulfillmentOwnerMismatchException();}private long Now(){var value=now();if(value<0||value>253402300799999L)throw new ArgumentOutOfRangeException("nowMilliseconds");return value;}private static string ReceiptKey(long revision)=>"receipt/"+revision;private static string FeedKey(long revision)=>"feed/"+revision;private static bool Same(byte[] left,byte[] right){if(left.Length!=right.Length)return false;var difference=0;for(var i=0;i<left.Length;i++)difference|=left[i]^right[i];return difference==0;}
    }
}
