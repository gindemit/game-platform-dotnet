// Generated from the reviewed DTO catalog. Do not edit by hand.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
namespace GamePlatform.Serialization.MessagePack {
public sealed class TypedQualificationCodec {
private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
public byte[] Encode<T>(T value) where T : class { var diagnostic=ToDiagnostic(value); new MappingBudget(4).Reserve(diagnostic); return codec.Encode(Schema<T>(), diagnostic); }
public T Decode<T>(byte[] bytes) where T : class => FromDiagnostic<T>(codec.Decode(Schema<T>(), bytes));
public V ToDiagnostic<T>(T value) where T : class { if(value == null) throw new ArgumentNullException(nameof(value)); var b=new MappingBudget(); switch(value) {
case AccountResponse dto: return WriteAccountResponse(dto,b);
case BootstrapCollection dto: return WriteBootstrapCollection(dto,b);
case BootstrapStartResponse dto: return WriteBootstrapStartResponse(dto,b);
case BootstrapPageRequest dto: return WriteBootstrapPageRequest(dto,b);
case BootstrapPageResponse dto: return WriteBootstrapPageResponse(dto,b);
case BootstrapStartQuery dto: return WriteBootstrapStartQuery(dto,b);
case CommonError dto: return WriteCommonError(dto,b);
case CommonExtension dto: return WriteCommonExtension(dto,b);
case CommonActor dto: return WriteCommonActor(dto,b);
case CommonAccount dto: return WriteCommonAccount(dto,b);
case CommonMembership dto: return WriteCommonMembership(dto,b);
case CommonStreamState dto: return WriteCommonStreamState(dto,b);
case ErrorResponse dto: return WriteErrorResponse(dto,b);
case GameplayCompletionCommand dto: return WriteGameplayCompletionCommand(dto,b);
case ProfileProfile dto: return WriteProfileProfile(dto,b);
case ProfileResponse dto: return WriteProfileResponse(dto,b);
case ProfilePatchCommand dto: return WriteProfilePatchCommand(dto,b);
case ProjectionProfileKey dto: return WriteProjectionProfileKey(dto,b);
case ProjectionProgressionKey dto: return WriteProjectionProgressionKey(dto,b);
case ProjectionInventoryKey dto: return WriteProjectionInventoryKey(dto,b);
case ProjectionWalletKey dto: return WriteProjectionWalletKey(dto,b);
case ProjectionEntitlementKey dto: return WriteProjectionEntitlementKey(dto,b);
case ProjectionProgressionData dto: return WriteProjectionProgressionData(dto,b);
case ProjectionInventoryData dto: return WriteProjectionInventoryData(dto,b);
case ProjectionWalletData dto: return WriteProjectionWalletData(dto,b);
case ProjectionEntitlementData dto: return WriteProjectionEntitlementData(dto,b);
case ProjectionProfileSnapshot dto: return WriteProjectionProfileSnapshot(dto,b);
case ProjectionProgressionSnapshot dto: return WriteProjectionProgressionSnapshot(dto,b);
case ProjectionInventorySnapshot dto: return WriteProjectionInventorySnapshot(dto,b);
case ProjectionWalletSnapshot dto: return WriteProjectionWalletSnapshot(dto,b);
case ProjectionEntitlementSnapshot dto: return WriteProjectionEntitlementSnapshot(dto,b);
case ProjectionProfileUpsert dto: return WriteProjectionProfileUpsert(dto,b);
case ProjectionProgressionUpsert dto: return WriteProjectionProgressionUpsert(dto,b);
case ProjectionInventoryUpsert dto: return WriteProjectionInventoryUpsert(dto,b);
case ProjectionWalletUpsert dto: return WriteProjectionWalletUpsert(dto,b);
case ProjectionEntitlementUpsert dto: return WriteProjectionEntitlementUpsert(dto,b);
case ProjectionProfileRemovalValue dto: return WriteProjectionProfileRemovalValue(dto,b);
case ProjectionProgressionRemoval dto: return WriteProjectionProgressionRemoval(dto,b);
case ProjectionInventoryRemoval dto: return WriteProjectionInventoryRemoval(dto,b);
case ProjectionWalletRemoval dto: return WriteProjectionWalletRemoval(dto,b);
case ProjectionEntitlementRemoval dto: return WriteProjectionEntitlementRemoval(dto,b);
case ProvisionRequest dto: return WriteProvisionRequest(dto,b);
case ProvisionResponse dto: return WriteProvisionResponse(dto,b);
case PullRequest dto: return WritePullRequest(dto,b);
case PullGroup dto: return WritePullGroup(dto,b);
case PullPage dto: return WritePullPage(dto,b);
case PullReset dto: return WritePullReset(dto,b);
case PushOperation dto: return WritePushOperation(dto,b);
case PushRequest dto: return WritePushRequest(dto,b);
case PushAccepted dto: return WritePushAccepted(dto,b);
case PushTerminalRejected dto: return WritePushTerminalRejected(dto,b);
case PushWaiting dto: return WritePushWaiting(dto,b);
case PushRetryable dto: return WritePushRetryable(dto,b);
case PushUpgradeRequired dto: return WritePushUpgradeRequired(dto,b);
case PushResponse dto: return WritePushResponse(dto,b);
case RecoveryReceiptRequest dto: return WriteRecoveryReceiptRequest(dto,b);
case RecoveryReceiptResponse dto: return WriteRecoveryReceiptResponse(dto,b);
case RecoveryStreamRequest dto: return WriteRecoveryStreamRequest(dto,b);
case RecoveryStreamResponse dto: return WriteRecoveryStreamResponse(dto,b);
case RecoveryInspectRequest dto: return WriteRecoveryInspectRequest(dto,b);
case RecoveryInspectResponse dto: return WriteRecoveryInspectResponse(dto,b);
case PushProfileUpdatedResult dto: return WritePushProfileUpdatedResult(dto,b);
case PushGameplayCompletionRecordedResult dto: return WritePushGameplayCompletionRecordedResult(dto,b);
case GameplayValidation dto: return WriteGameplayValidation(dto,b);
default: throw new QualificationCodecException("Unsupported DTO implementation."); } }
public T FromDiagnostic<T>(V value) where T : class { codec.ValidateDiagnostic(Schema<T>(),value); new MappingBudget().Reserve(value); var b=new MappingBudget();
if(typeof(T) == typeof(AccountResponse)) return (T)(object)ReadAccountResponse(value,b);
if(typeof(T) == typeof(BootstrapCollection)) return (T)(object)ReadBootstrapCollection(value,b);
if(typeof(T) == typeof(BootstrapStartResponse)) return (T)(object)ReadBootstrapStartResponse(value,b);
if(typeof(T) == typeof(BootstrapPageRequest)) return (T)(object)ReadBootstrapPageRequest(value,b);
if(typeof(T) == typeof(BootstrapPageResponse)) return (T)(object)ReadBootstrapPageResponse(value,b);
if(typeof(T) == typeof(BootstrapStartQuery)) return (T)(object)ReadBootstrapStartQuery(value,b);
if(typeof(T) == typeof(CommonError)) return (T)(object)ReadCommonError(value,b);
if(typeof(T) == typeof(CommonExtension)) return (T)(object)ReadCommonExtension(value,b);
if(typeof(T) == typeof(CommonActor)) return (T)(object)ReadCommonActor(value,b);
if(typeof(T) == typeof(CommonAccount)) return (T)(object)ReadCommonAccount(value,b);
if(typeof(T) == typeof(CommonMembership)) return (T)(object)ReadCommonMembership(value,b);
if(typeof(T) == typeof(CommonStreamState)) return (T)(object)ReadCommonStreamState(value,b);
if(typeof(T) == typeof(ErrorResponse)) return (T)(object)ReadErrorResponse(value,b);
if(typeof(T) == typeof(GameplayCompletionCommand)) return (T)(object)ReadGameplayCompletionCommand(value,b);
if(typeof(T) == typeof(ProfileProfile)) return (T)(object)ReadProfileProfile(value,b);
if(typeof(T) == typeof(ProfileResponse)) return (T)(object)ReadProfileResponse(value,b);
if(typeof(T) == typeof(ProfilePatchCommand)) return (T)(object)ReadProfilePatchCommand(value,b);
if(typeof(T) == typeof(ProjectionProfileKey)) return (T)(object)ReadProjectionProfileKey(value,b);
if(typeof(T) == typeof(ProjectionProgressionKey)) return (T)(object)ReadProjectionProgressionKey(value,b);
if(typeof(T) == typeof(ProjectionInventoryKey)) return (T)(object)ReadProjectionInventoryKey(value,b);
if(typeof(T) == typeof(ProjectionWalletKey)) return (T)(object)ReadProjectionWalletKey(value,b);
if(typeof(T) == typeof(ProjectionEntitlementKey)) return (T)(object)ReadProjectionEntitlementKey(value,b);
if(typeof(T) == typeof(ProjectionProgressionData)) return (T)(object)ReadProjectionProgressionData(value,b);
if(typeof(T) == typeof(ProjectionInventoryData)) return (T)(object)ReadProjectionInventoryData(value,b);
if(typeof(T) == typeof(ProjectionWalletData)) return (T)(object)ReadProjectionWalletData(value,b);
if(typeof(T) == typeof(ProjectionEntitlementData)) return (T)(object)ReadProjectionEntitlementData(value,b);
if(typeof(T) == typeof(ProjectionProfileSnapshot)) return (T)(object)ReadProjectionProfileSnapshot(value,b);
if(typeof(T) == typeof(ProjectionProgressionSnapshot)) return (T)(object)ReadProjectionProgressionSnapshot(value,b);
if(typeof(T) == typeof(ProjectionInventorySnapshot)) return (T)(object)ReadProjectionInventorySnapshot(value,b);
if(typeof(T) == typeof(ProjectionWalletSnapshot)) return (T)(object)ReadProjectionWalletSnapshot(value,b);
if(typeof(T) == typeof(ProjectionEntitlementSnapshot)) return (T)(object)ReadProjectionEntitlementSnapshot(value,b);
if(typeof(T) == typeof(ProjectionProfileUpsert)) return (T)(object)ReadProjectionProfileUpsert(value,b);
if(typeof(T) == typeof(ProjectionProgressionUpsert)) return (T)(object)ReadProjectionProgressionUpsert(value,b);
if(typeof(T) == typeof(ProjectionInventoryUpsert)) return (T)(object)ReadProjectionInventoryUpsert(value,b);
if(typeof(T) == typeof(ProjectionWalletUpsert)) return (T)(object)ReadProjectionWalletUpsert(value,b);
if(typeof(T) == typeof(ProjectionEntitlementUpsert)) return (T)(object)ReadProjectionEntitlementUpsert(value,b);
if(typeof(T) == typeof(ProjectionProfileRemovalValue)) return (T)(object)ReadProjectionProfileRemovalValue(value,b);
if(typeof(T) == typeof(ProjectionProgressionRemoval)) return (T)(object)ReadProjectionProgressionRemoval(value,b);
if(typeof(T) == typeof(ProjectionInventoryRemoval)) return (T)(object)ReadProjectionInventoryRemoval(value,b);
if(typeof(T) == typeof(ProjectionWalletRemoval)) return (T)(object)ReadProjectionWalletRemoval(value,b);
if(typeof(T) == typeof(ProjectionEntitlementRemoval)) return (T)(object)ReadProjectionEntitlementRemoval(value,b);
if(typeof(T) == typeof(ProvisionRequest)) return (T)(object)ReadProvisionRequest(value,b);
if(typeof(T) == typeof(ProvisionResponse)) return (T)(object)ReadProvisionResponse(value,b);
if(typeof(T) == typeof(PullRequest)) return (T)(object)ReadPullRequest(value,b);
if(typeof(T) == typeof(PullGroup)) return (T)(object)ReadPullGroup(value,b);
if(typeof(T) == typeof(PullPage)) return (T)(object)ReadPullPage(value,b);
if(typeof(T) == typeof(PullReset)) return (T)(object)ReadPullReset(value,b);
if(typeof(T) == typeof(PushOperation)) return (T)(object)ReadPushOperation(value,b);
if(typeof(T) == typeof(PushRequest)) return (T)(object)ReadPushRequest(value,b);
if(typeof(T) == typeof(PushAccepted)) return (T)(object)ReadPushAccepted(value,b);
if(typeof(T) == typeof(PushTerminalRejected)) return (T)(object)ReadPushTerminalRejected(value,b);
if(typeof(T) == typeof(PushWaiting)) return (T)(object)ReadPushWaiting(value,b);
if(typeof(T) == typeof(PushRetryable)) return (T)(object)ReadPushRetryable(value,b);
if(typeof(T) == typeof(PushUpgradeRequired)) return (T)(object)ReadPushUpgradeRequired(value,b);
if(typeof(T) == typeof(PushResponse)) return (T)(object)ReadPushResponse(value,b);
if(typeof(T) == typeof(RecoveryReceiptRequest)) return (T)(object)ReadRecoveryReceiptRequest(value,b);
if(typeof(T) == typeof(RecoveryReceiptResponse)) return (T)(object)ReadRecoveryReceiptResponse(value,b);
if(typeof(T) == typeof(RecoveryStreamRequest)) return (T)(object)ReadRecoveryStreamRequest(value,b);
if(typeof(T) == typeof(RecoveryStreamResponse)) return (T)(object)ReadRecoveryStreamResponse(value,b);
if(typeof(T) == typeof(RecoveryInspectRequest)) return (T)(object)ReadRecoveryInspectRequest(value,b);
if(typeof(T) == typeof(RecoveryInspectResponse)) return (T)(object)ReadRecoveryInspectResponse(value,b);
if(typeof(T) == typeof(PushProfileUpdatedResult)) return (T)(object)ReadPushProfileUpdatedResult(value,b);
if(typeof(T) == typeof(PushGameplayCompletionRecordedResult)) return (T)(object)ReadPushGameplayCompletionRecordedResult(value,b);
if(typeof(T) == typeof(GameplayValidation)) return (T)(object)ReadGameplayValidation(value,b);
if(typeof(T) == typeof(IProjectionSnapshotEntity)) return (T)(object)ReadIProjectionSnapshotEntity(value,b);
if(typeof(T) == typeof(IProjectionChange)) return (T)(object)ReadIProjectionChange(value,b);
if(typeof(T) == typeof(IProjectionProfileChangeUnion)) return (T)(object)ReadIProjectionProfileChangeUnion(value,b);
if(typeof(T) == typeof(IProjectionProgressionChange)) return (T)(object)ReadIProjectionProgressionChange(value,b);
if(typeof(T) == typeof(IProjectionInventoryChange)) return (T)(object)ReadIProjectionInventoryChange(value,b);
if(typeof(T) == typeof(IProjectionWalletChange)) return (T)(object)ReadIProjectionWalletChange(value,b);
if(typeof(T) == typeof(IProjectionEntitlementChange)) return (T)(object)ReadIProjectionEntitlementChange(value,b);
if(typeof(T) == typeof(IPullResponse)) return (T)(object)ReadIPullResponse(value,b);
if(typeof(T) == typeof(IPushResult)) return (T)(object)ReadIPushResult(value,b);
if(typeof(T) == typeof(IPushAcceptedResult)) return (T)(object)ReadIPushAcceptedResult(value,b);
if(typeof(T) == typeof(IPushPayload)) return (T)(object)ReadIPushPayload(value,b);
if(typeof(T) == typeof(IPushFinalResult)) return (T)(object)ReadIPushFinalResult(value,b);
throw new QualificationCodecException("Unsupported DTO type."); }
public static string Schema<T>() where T : class {
if(typeof(T) == typeof(AccountResponse)) return "account.schema.json#/$defs/response";
if(typeof(T) == typeof(BootstrapCollection)) return "bootstrap.schema.json#/$defs/collection";
if(typeof(T) == typeof(BootstrapStartResponse)) return "bootstrap.schema.json#/$defs/startResponse";
if(typeof(T) == typeof(BootstrapPageRequest)) return "bootstrap.schema.json#/$defs/pageRequest";
if(typeof(T) == typeof(BootstrapPageResponse)) return "bootstrap.schema.json#/$defs/pageResponse";
if(typeof(T) == typeof(BootstrapStartQuery)) return "bootstrap.schema.json#/$defs/startQuery";
if(typeof(T) == typeof(CommonError)) return "common.schema.json#/$defs/error";
if(typeof(T) == typeof(CommonExtension)) return "common.schema.json#/$defs/extension";
if(typeof(T) == typeof(CommonActor)) return "common.schema.json#/$defs/actor";
if(typeof(T) == typeof(CommonAccount)) return "common.schema.json#/$defs/account";
if(typeof(T) == typeof(CommonMembership)) return "common.schema.json#/$defs/membership";
if(typeof(T) == typeof(CommonStreamState)) return "common.schema.json#/$defs/streamState";
if(typeof(T) == typeof(ErrorResponse)) return "error.schema.json#/$defs/response";
if(typeof(T) == typeof(GameplayCompletionCommand)) return "gameplay.schema.json#/$defs/completionCommand";
if(typeof(T) == typeof(ProfileProfile)) return "profile.schema.json#/$defs/profile";
if(typeof(T) == typeof(ProfileResponse)) return "profile.schema.json#/$defs/response";
if(typeof(T) == typeof(ProfilePatchCommand)) return "profile.schema.json#/$defs/patchCommand";
if(typeof(T) == typeof(ProjectionProfileKey)) return "projection.schema.json#/$defs/profileKey";
if(typeof(T) == typeof(ProjectionProgressionKey)) return "projection.schema.json#/$defs/progressionKey";
if(typeof(T) == typeof(ProjectionInventoryKey)) return "projection.schema.json#/$defs/inventoryKey";
if(typeof(T) == typeof(ProjectionWalletKey)) return "projection.schema.json#/$defs/walletKey";
if(typeof(T) == typeof(ProjectionEntitlementKey)) return "projection.schema.json#/$defs/entitlementKey";
if(typeof(T) == typeof(ProjectionProgressionData)) return "projection.schema.json#/$defs/progressionData";
if(typeof(T) == typeof(ProjectionInventoryData)) return "projection.schema.json#/$defs/inventoryData";
if(typeof(T) == typeof(ProjectionWalletData)) return "projection.schema.json#/$defs/walletData";
if(typeof(T) == typeof(ProjectionEntitlementData)) return "projection.schema.json#/$defs/entitlementData";
if(typeof(T) == typeof(ProjectionProfileSnapshot)) return "projection.schema.json#/$defs/profileSnapshot";
if(typeof(T) == typeof(ProjectionProgressionSnapshot)) return "projection.schema.json#/$defs/progressionSnapshot";
if(typeof(T) == typeof(ProjectionInventorySnapshot)) return "projection.schema.json#/$defs/inventorySnapshot";
if(typeof(T) == typeof(ProjectionWalletSnapshot)) return "projection.schema.json#/$defs/walletSnapshot";
if(typeof(T) == typeof(ProjectionEntitlementSnapshot)) return "projection.schema.json#/$defs/entitlementSnapshot";
if(typeof(T) == typeof(ProjectionProfileUpsert)) return "projection.schema.json#/$defs/profileUpsert";
if(typeof(T) == typeof(ProjectionProgressionUpsert)) return "projection.schema.json#/$defs/progressionUpsert";
if(typeof(T) == typeof(ProjectionInventoryUpsert)) return "projection.schema.json#/$defs/inventoryUpsert";
if(typeof(T) == typeof(ProjectionWalletUpsert)) return "projection.schema.json#/$defs/walletUpsert";
if(typeof(T) == typeof(ProjectionEntitlementUpsert)) return "projection.schema.json#/$defs/entitlementUpsert";
if(typeof(T) == typeof(ProjectionProfileRemovalValue)) return "projection.schema.json#/$defs/profileRemovalValue";
if(typeof(T) == typeof(ProjectionProgressionRemoval)) return "projection.schema.json#/$defs/progressionRemoval";
if(typeof(T) == typeof(ProjectionInventoryRemoval)) return "projection.schema.json#/$defs/inventoryRemoval";
if(typeof(T) == typeof(ProjectionWalletRemoval)) return "projection.schema.json#/$defs/walletRemoval";
if(typeof(T) == typeof(ProjectionEntitlementRemoval)) return "projection.schema.json#/$defs/entitlementRemoval";
if(typeof(T) == typeof(ProvisionRequest)) return "provision.schema.json#/$defs/request";
if(typeof(T) == typeof(ProvisionResponse)) return "provision.schema.json#/$defs/response";
if(typeof(T) == typeof(PullRequest)) return "pull.schema.json#/$defs/request";
if(typeof(T) == typeof(PullGroup)) return "pull.schema.json#/$defs/group";
if(typeof(T) == typeof(PullPage)) return "pull.schema.json#/$defs/page";
if(typeof(T) == typeof(PullReset)) return "pull.schema.json#/$defs/reset";
if(typeof(T) == typeof(PushOperation)) return "push.schema.json#/$defs/operation";
if(typeof(T) == typeof(PushRequest)) return "push.schema.json#/$defs/request";
if(typeof(T) == typeof(PushAccepted)) return "push.schema.json#/$defs/accepted";
if(typeof(T) == typeof(PushTerminalRejected)) return "push.schema.json#/$defs/terminalRejected";
if(typeof(T) == typeof(PushWaiting)) return "push.schema.json#/$defs/waiting";
if(typeof(T) == typeof(PushRetryable)) return "push.schema.json#/$defs/retryable";
if(typeof(T) == typeof(PushUpgradeRequired)) return "push.schema.json#/$defs/upgradeRequired";
if(typeof(T) == typeof(PushResponse)) return "push.schema.json#/$defs/response";
if(typeof(T) == typeof(RecoveryReceiptRequest)) return "recovery.schema.json#/$defs/receiptRequest";
if(typeof(T) == typeof(RecoveryReceiptResponse)) return "recovery.schema.json#/$defs/receiptResponse";
if(typeof(T) == typeof(RecoveryStreamRequest)) return "recovery.schema.json#/$defs/streamRequest";
if(typeof(T) == typeof(RecoveryStreamResponse)) return "recovery.schema.json#/$defs/streamResponse";
if(typeof(T) == typeof(RecoveryInspectRequest)) return "recovery.schema.json#/$defs/inspectRequest";
if(typeof(T) == typeof(RecoveryInspectResponse)) return "recovery.schema.json#/$defs/inspectResponse";
if(typeof(T) == typeof(PushProfileUpdatedResult)) return "push.schema.json#/$defs/accepted/properties/result/oneOf/0";
if(typeof(T) == typeof(PushGameplayCompletionRecordedResult)) return "push.schema.json#/$defs/accepted/properties/result/oneOf/1";
if(typeof(T) == typeof(GameplayValidation)) return "gameplay.schema.json#/$defs/completionCommand/properties/validation";
if(typeof(T) == typeof(IProjectionSnapshotEntity)) return "projection.schema.json#/$defs/snapshotEntity";
if(typeof(T) == typeof(IProjectionChange)) return "projection.schema.json#/$defs/change";
if(typeof(T) == typeof(IProjectionProfileChangeUnion)) return "projection.schema.json#/$defs/profileChangeUnion";
if(typeof(T) == typeof(IProjectionProgressionChange)) return "projection.schema.json#/$defs/progressionChange";
if(typeof(T) == typeof(IProjectionInventoryChange)) return "projection.schema.json#/$defs/inventoryChange";
if(typeof(T) == typeof(IProjectionWalletChange)) return "projection.schema.json#/$defs/walletChange";
if(typeof(T) == typeof(IProjectionEntitlementChange)) return "projection.schema.json#/$defs/entitlementChange";
if(typeof(T) == typeof(IPullResponse)) return "pull.schema.json#/$defs/response";
if(typeof(T) == typeof(IPushResult)) return "push.schema.json#/$defs/result";
throw new QualificationCodecException("Unsupported top-level DTO type."); }
public V NormalizeDto(string schema,V value) { new MappingBudget(4).Reserve(value); switch(schema) {
case "account.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<AccountResponse>(value));
case "bootstrap.schema.json#/$defs/collection": return ToDiagnostic(FromDiagnostic<BootstrapCollection>(value));
case "bootstrap.schema.json#/$defs/startResponse": return ToDiagnostic(FromDiagnostic<BootstrapStartResponse>(value));
case "bootstrap.schema.json#/$defs/pageRequest": return ToDiagnostic(FromDiagnostic<BootstrapPageRequest>(value));
case "bootstrap.schema.json#/$defs/pageResponse": return ToDiagnostic(FromDiagnostic<BootstrapPageResponse>(value));
case "bootstrap.schema.json#/$defs/startQuery": return ToDiagnostic(FromDiagnostic<BootstrapStartQuery>(value));
case "common.schema.json#/$defs/error": return ToDiagnostic(FromDiagnostic<CommonError>(value));
case "common.schema.json#/$defs/extension": return ToDiagnostic(FromDiagnostic<CommonExtension>(value));
case "common.schema.json#/$defs/actor": return ToDiagnostic(FromDiagnostic<CommonActor>(value));
case "common.schema.json#/$defs/account": return ToDiagnostic(FromDiagnostic<CommonAccount>(value));
case "common.schema.json#/$defs/membership": return ToDiagnostic(FromDiagnostic<CommonMembership>(value));
case "common.schema.json#/$defs/streamState": return ToDiagnostic(FromDiagnostic<CommonStreamState>(value));
case "error.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<ErrorResponse>(value));
case "gameplay.schema.json#/$defs/completionCommand": return ToDiagnostic(FromDiagnostic<GameplayCompletionCommand>(value));
case "profile.schema.json#/$defs/profile": return ToDiagnostic(FromDiagnostic<ProfileProfile>(value));
case "profile.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<ProfileResponse>(value));
case "profile.schema.json#/$defs/patchCommand": return ToDiagnostic(FromDiagnostic<ProfilePatchCommand>(value));
case "projection.schema.json#/$defs/profileKey": return ToDiagnostic(FromDiagnostic<ProjectionProfileKey>(value));
case "projection.schema.json#/$defs/progressionKey": return ToDiagnostic(FromDiagnostic<ProjectionProgressionKey>(value));
case "projection.schema.json#/$defs/inventoryKey": return ToDiagnostic(FromDiagnostic<ProjectionInventoryKey>(value));
case "projection.schema.json#/$defs/walletKey": return ToDiagnostic(FromDiagnostic<ProjectionWalletKey>(value));
case "projection.schema.json#/$defs/entitlementKey": return ToDiagnostic(FromDiagnostic<ProjectionEntitlementKey>(value));
case "projection.schema.json#/$defs/progressionData": return ToDiagnostic(FromDiagnostic<ProjectionProgressionData>(value));
case "projection.schema.json#/$defs/inventoryData": return ToDiagnostic(FromDiagnostic<ProjectionInventoryData>(value));
case "projection.schema.json#/$defs/walletData": return ToDiagnostic(FromDiagnostic<ProjectionWalletData>(value));
case "projection.schema.json#/$defs/entitlementData": return ToDiagnostic(FromDiagnostic<ProjectionEntitlementData>(value));
case "projection.schema.json#/$defs/profileSnapshot": return ToDiagnostic(FromDiagnostic<ProjectionProfileSnapshot>(value));
case "projection.schema.json#/$defs/progressionSnapshot": return ToDiagnostic(FromDiagnostic<ProjectionProgressionSnapshot>(value));
case "projection.schema.json#/$defs/inventorySnapshot": return ToDiagnostic(FromDiagnostic<ProjectionInventorySnapshot>(value));
case "projection.schema.json#/$defs/walletSnapshot": return ToDiagnostic(FromDiagnostic<ProjectionWalletSnapshot>(value));
case "projection.schema.json#/$defs/entitlementSnapshot": return ToDiagnostic(FromDiagnostic<ProjectionEntitlementSnapshot>(value));
case "projection.schema.json#/$defs/profileUpsert": return ToDiagnostic(FromDiagnostic<ProjectionProfileUpsert>(value));
case "projection.schema.json#/$defs/progressionUpsert": return ToDiagnostic(FromDiagnostic<ProjectionProgressionUpsert>(value));
case "projection.schema.json#/$defs/inventoryUpsert": return ToDiagnostic(FromDiagnostic<ProjectionInventoryUpsert>(value));
case "projection.schema.json#/$defs/walletUpsert": return ToDiagnostic(FromDiagnostic<ProjectionWalletUpsert>(value));
case "projection.schema.json#/$defs/entitlementUpsert": return ToDiagnostic(FromDiagnostic<ProjectionEntitlementUpsert>(value));
case "projection.schema.json#/$defs/profileRemovalValue": return ToDiagnostic(FromDiagnostic<ProjectionProfileRemovalValue>(value));
case "projection.schema.json#/$defs/progressionRemoval": return ToDiagnostic(FromDiagnostic<ProjectionProgressionRemoval>(value));
case "projection.schema.json#/$defs/inventoryRemoval": return ToDiagnostic(FromDiagnostic<ProjectionInventoryRemoval>(value));
case "projection.schema.json#/$defs/walletRemoval": return ToDiagnostic(FromDiagnostic<ProjectionWalletRemoval>(value));
case "projection.schema.json#/$defs/entitlementRemoval": return ToDiagnostic(FromDiagnostic<ProjectionEntitlementRemoval>(value));
case "provision.schema.json#/$defs/request": return ToDiagnostic(FromDiagnostic<ProvisionRequest>(value));
case "provision.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<ProvisionResponse>(value));
case "pull.schema.json#/$defs/request": return ToDiagnostic(FromDiagnostic<PullRequest>(value));
case "pull.schema.json#/$defs/group": return ToDiagnostic(FromDiagnostic<PullGroup>(value));
case "pull.schema.json#/$defs/page": return ToDiagnostic(FromDiagnostic<PullPage>(value));
case "pull.schema.json#/$defs/reset": return ToDiagnostic(FromDiagnostic<PullReset>(value));
case "push.schema.json#/$defs/operation": return ToDiagnostic(FromDiagnostic<PushOperation>(value));
case "push.schema.json#/$defs/request": return ToDiagnostic(FromDiagnostic<PushRequest>(value));
case "push.schema.json#/$defs/accepted": return ToDiagnostic(FromDiagnostic<PushAccepted>(value));
case "push.schema.json#/$defs/terminalRejected": return ToDiagnostic(FromDiagnostic<PushTerminalRejected>(value));
case "push.schema.json#/$defs/waiting": return ToDiagnostic(FromDiagnostic<PushWaiting>(value));
case "push.schema.json#/$defs/retryable": return ToDiagnostic(FromDiagnostic<PushRetryable>(value));
case "push.schema.json#/$defs/upgradeRequired": return ToDiagnostic(FromDiagnostic<PushUpgradeRequired>(value));
case "push.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<PushResponse>(value));
case "recovery.schema.json#/$defs/receiptRequest": return ToDiagnostic(FromDiagnostic<RecoveryReceiptRequest>(value));
case "recovery.schema.json#/$defs/receiptResponse": return ToDiagnostic(FromDiagnostic<RecoveryReceiptResponse>(value));
case "recovery.schema.json#/$defs/streamRequest": return ToDiagnostic(FromDiagnostic<RecoveryStreamRequest>(value));
case "recovery.schema.json#/$defs/streamResponse": return ToDiagnostic(FromDiagnostic<RecoveryStreamResponse>(value));
case "recovery.schema.json#/$defs/inspectRequest": return ToDiagnostic(FromDiagnostic<RecoveryInspectRequest>(value));
case "recovery.schema.json#/$defs/inspectResponse": return ToDiagnostic(FromDiagnostic<RecoveryInspectResponse>(value));
case "push.schema.json#/$defs/accepted/properties/result/oneOf/0": return ToDiagnostic(FromDiagnostic<PushProfileUpdatedResult>(value));
case "push.schema.json#/$defs/accepted/properties/result/oneOf/1": return ToDiagnostic(FromDiagnostic<PushGameplayCompletionRecordedResult>(value));
case "gameplay.schema.json#/$defs/completionCommand/properties/validation": return ToDiagnostic(FromDiagnostic<GameplayValidation>(value));
case "projection.schema.json#/$defs/snapshotEntity": return ToDiagnostic(FromDiagnostic<IProjectionSnapshotEntity>(value));
case "projection.schema.json#/$defs/change": return ToDiagnostic(FromDiagnostic<IProjectionChange>(value));
case "projection.schema.json#/$defs/profileChangeUnion": return ToDiagnostic(FromDiagnostic<IProjectionProfileChangeUnion>(value));
case "projection.schema.json#/$defs/progressionChange": return ToDiagnostic(FromDiagnostic<IProjectionProgressionChange>(value));
case "projection.schema.json#/$defs/inventoryChange": return ToDiagnostic(FromDiagnostic<IProjectionInventoryChange>(value));
case "projection.schema.json#/$defs/walletChange": return ToDiagnostic(FromDiagnostic<IProjectionWalletChange>(value));
case "projection.schema.json#/$defs/entitlementChange": return ToDiagnostic(FromDiagnostic<IProjectionEntitlementChange>(value));
case "pull.schema.json#/$defs/response": return ToDiagnostic(FromDiagnostic<IPullResponse>(value));
case "push.schema.json#/$defs/result": return ToDiagnostic(FromDiagnostic<IPushResult>(value));
case "bootstrap.schema.json#/$defs/entity": return ToDiagnostic(FromDiagnostic<IProjectionSnapshotEntity>(value));
case "projection.schema.json#/$defs/profileChange": return ToDiagnostic(FromDiagnostic<IProjectionProfileChangeUnion>(value));
case "projection.schema.json#/$defs/profileRemoval": return ToDiagnostic(FromDiagnostic<ProjectionProfileRemovalValue>(value));
case "pull.schema.json#/$defs/change": return ToDiagnostic(FromDiagnostic<IProjectionChange>(value));
default: throw new QualificationCodecException("Unsupported DTO schema."); } }
private static V WriteAccountResponse(AccountResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("account"), WriteCommonAccount(value.Account, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("memberships"), Array(value.Memberships, item0 => WriteCommonMembership(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static AccountResponse ReadAccountResponse(V value, MappingBudget b) { b.Node(); return new AccountResponse(
ReadCommonAccount(value.Properties["account"], b),
value.Properties["memberships"].Items.Select(item0 => ReadCommonMembership(item0, b)).ToArray(),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WriteBootstrapCollection(BootstrapCollection value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("name"), b.String(value.Name)));
fields.Add(new KeyValuePair<string,V>(b.Key("schemaVersion"), b.Integer(value.SchemaVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("required"), b.Boolean(value.Required)));
return V.Object(fields); }
private static BootstrapCollection ReadBootstrapCollection(V value, MappingBudget b) { b.Node(); return new BootstrapCollection(
value.Properties["name"].StringValue); }
private static V WriteBootstrapStartResponse(BootstrapStartResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("account"), WriteCommonAccount(value.Account, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("membership"), WriteCommonMembership(value.Membership, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("snapshotSession"), b.String(value.SnapshotSession)));
fields.Add(new KeyValuePair<string,V>(b.Key("committedThrough"), b.String(value.CommittedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("visibilityGeneration"), b.String(value.VisibilityGeneration.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("logEpoch"), b.String(value.LogEpoch.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("collections"), Array(value.Collections, item0 => WriteBootstrapCollection(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("firstPageToken"), b.String(value.FirstPageToken)));
fields.Add(new KeyValuePair<string,V>(b.Key("expiresAt"), b.Integer(value.ExpiresAt)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
fields.Add(new KeyValuePair<string,V>(b.Key("streamState"), WriteCommonStreamState(value.StreamState, b)));
return V.Object(fields); }
private static BootstrapStartResponse ReadBootstrapStartResponse(V value, MappingBudget b) { b.Node(); return new BootstrapStartResponse(
ReadCommonAccount(value.Properties["account"], b),
ReadCommonMembership(value.Properties["membership"], b),
value.Properties["snapshotSession"].StringValue,
long.Parse(value.Properties["committedThrough"].StringValue, CultureInfo.InvariantCulture),
long.Parse(value.Properties["visibilityGeneration"].StringValue, CultureInfo.InvariantCulture),
Guid.ParseExact(value.Properties["logEpoch"].StringValue, "D"),
value.Properties["collections"].Items.Select(item0 => ReadBootstrapCollection(item0, b)).ToArray(),
value.Properties["firstPageToken"].StringValue,
checked((long)value.Properties["expiresAt"].IntegerValue),
checked((long)value.Properties["serverTime"].IntegerValue),
ReadCommonStreamState(value.Properties["streamState"], b)); }
private static V WriteBootstrapPageRequest(BootstrapPageRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("snapshotSession"), b.String(value.SnapshotSession)));
fields.Add(new KeyValuePair<string,V>(b.Key("pageToken"), b.String(value.PageToken)));
fields.Add(new KeyValuePair<string,V>(b.Key("maxBytes"), b.Integer(value.MaxBytes)));
return V.Object(fields); }
private static BootstrapPageRequest ReadBootstrapPageRequest(V value, MappingBudget b) { b.Node(); return new BootstrapPageRequest(
value.Properties["snapshotSession"].StringValue,
value.Properties["pageToken"].StringValue,
checked((int)value.Properties["maxBytes"].IntegerValue)); }
private static V WriteBootstrapPageResponse(BootstrapPageResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("snapshotSession"), b.String(value.SnapshotSession)));
fields.Add(new KeyValuePair<string,V>(b.Key("committedThrough"), b.String(value.CommittedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("entities"), Array(value.Entities, item0 => WriteIProjectionSnapshotEntity(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("hasMore"), b.Boolean(value.HasMore)));
fields.Add(new KeyValuePair<string,V>(b.Key("nextPageToken"), (value.NextPageToken == null ? b.Null() : b.String(value.NextPageToken!))));
fields.Add(new KeyValuePair<string,V>(b.Key("initialPullCursor"), (value.InitialPullCursor == null ? b.Null() : b.String(value.InitialPullCursor!))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static BootstrapPageResponse ReadBootstrapPageResponse(V value, MappingBudget b) { b.Node(); return new BootstrapPageResponse(
value.Properties["snapshotSession"].StringValue,
long.Parse(value.Properties["committedThrough"].StringValue, CultureInfo.InvariantCulture),
value.Properties["entities"].Items.Select(item0 => ReadIProjectionSnapshotEntity(item0, b)).ToArray(),
value.Properties["hasMore"].BooleanValue,
(value.Properties["nextPageToken"].Kind == K.Null ? (string?)null : value.Properties["nextPageToken"].StringValue),
(value.Properties["initialPullCursor"].Kind == K.Null ? (string?)null : value.Properties["initialPullCursor"].StringValue),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WriteBootstrapStartQuery(BootstrapStartQuery value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
return V.Object(fields); }
private static BootstrapStartQuery ReadBootstrapStartQuery(V value, MappingBudget b) { b.Node(); return new BootstrapStartQuery(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D")); }
private static V WriteCommonError(CommonError value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("code"), b.String(value.Code)));
fields.Add(new KeyValuePair<string,V>(b.Key("category"), b.String(value.Category)));
fields.Add(new KeyValuePair<string,V>(b.Key("message"), b.String(value.Message)));
fields.Add(new KeyValuePair<string,V>(b.Key("retryable"), b.Boolean(value.Retryable)));
if(value.RetryAfterMilliseconds.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("retryAfterMilliseconds"), b.Integer(value.RetryAfterMilliseconds.Value)));
if(value.Details.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("details"), Map(value.Details.Value, pair0 => WriteDetail(pair0.Value, b), b)));
return V.Object(fields); }
private static CommonError ReadCommonError(V value, MappingBudget b) { b.Node(); return new CommonError(
value.Properties["code"].StringValue,
value.Properties["category"].StringValue,
value.Properties["message"].StringValue,
value.Properties["retryable"].BooleanValue,
value.Properties.ContainsKey("retryAfterMilliseconds") ? WireOptional<int>.Present(checked((int)value.Properties["retryAfterMilliseconds"].IntegerValue)) : default,
value.Properties.ContainsKey("details") ? WireOptional<IReadOnlyDictionary<string, ErrorDetailValue>>.Present(value.Properties["details"].Properties.ToDictionary(pair0 => pair0.Key, pair0 => ReadDetail(pair0.Value, b), StringComparer.Ordinal)) : default); }
private static V WriteCommonExtension(CommonExtension value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("schema"), b.String(value.Schema)));
fields.Add(new KeyValuePair<string,V>(b.Key("version"), b.Integer(value.Version)));
fields.Add(new KeyValuePair<string,V>(b.Key("value"), Map(value.Value, pair0 => WriteExtension(pair0.Value, b, 1), b)));
return V.Object(fields); }
private static CommonExtension ReadCommonExtension(V value, MappingBudget b) { b.Node(); return new CommonExtension(
value.Properties["schema"].StringValue,
checked((int)value.Properties["version"].IntegerValue),
value.Properties["value"].Properties.ToDictionary(pair0 => pair0.Key, pair0 => ReadExtension(pair0.Value, b, 1), StringComparer.Ordinal)); }
private static V WriteCommonActor(CommonActor value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("id"), b.String(value.Id)));
return V.Object(fields); }
private static CommonActor ReadCommonActor(V value, MappingBudget b) { b.Node(); return new CommonActor(
value.Properties["kind"].StringValue,
value.Properties["id"].StringValue); }
private static V WriteCommonAccount(CommonAccount value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("platformUserId"), b.String(value.PlatformUserId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("createdAt"), b.Integer(value.CreatedAt)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static CommonAccount ReadCommonAccount(V value, MappingBudget b) { b.Node(); return new CommonAccount(
Guid.ParseExact(value.Properties["platformUserId"].StringValue, "D"),
checked((long)value.Properties["createdAt"].IntegerValue),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteCommonMembership(CommonMembership value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("appId"), b.String(value.AppId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("createdAt"), b.Integer(value.CreatedAt)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static CommonMembership ReadCommonMembership(V value, MappingBudget b) { b.Node(); return new CommonMembership(
Guid.ParseExact(value.Properties["appId"].StringValue, "D"),
value.Properties["status"].StringValue,
checked((long)value.Properties["createdAt"].IntegerValue),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteCommonStreamState(CommonStreamState value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("finalizedThrough"), b.String(value.FinalizedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("nextSequence"), (value.NextSequence == null ? b.Null() : b.String(value.NextSequence.Value!.ToString(CultureInfo.InvariantCulture)))));
fields.Add(new KeyValuePair<string,V>(b.Key("state"), b.String(value.State)));
return V.Object(fields); }
private static CommonStreamState ReadCommonStreamState(V value, MappingBudget b) { b.Node(); return new CommonStreamState(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
Guid.ParseExact(value.Properties["installationId"].StringValue, "D"),
long.Parse(value.Properties["finalizedThrough"].StringValue, CultureInfo.InvariantCulture),
(value.Properties["nextSequence"].Kind == K.Null ? (long?)null : long.Parse(value.Properties["nextSequence"].StringValue, CultureInfo.InvariantCulture)),
value.Properties["state"].StringValue); }
private static V WriteErrorResponse(ErrorResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("correlationId"), b.String(value.CorrelationId)));
fields.Add(new KeyValuePair<string,V>(b.Key("error"), WriteCommonError(value.Error, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static ErrorResponse ReadErrorResponse(V value, MappingBudget b) { b.Node(); return new ErrorResponse(
value.Properties["correlationId"].StringValue,
ReadCommonError(value.Properties["error"], b),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WriteGameplayCompletionCommand(GameplayCompletionCommand value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("contentId"), b.String(value.ContentId)));
fields.Add(new KeyValuePair<string,V>(b.Key("contentVersion"), b.Integer(value.ContentVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("mode"), b.String(value.Mode)));
fields.Add(new KeyValuePair<string,V>(b.Key("difficulty"), b.String(value.Difficulty)));
fields.Add(new KeyValuePair<string,V>(b.Key("success"), b.Boolean(value.Success)));
fields.Add(new KeyValuePair<string,V>(b.Key("score"), b.String(value.Score.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("validation"), WriteGameplayValidation(value.Validation, b)));
if(value.Extension.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("extension"), WriteCommonExtension(value.Extension.Value, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("session"), b.String(value.Session)));
fields.Add(new KeyValuePair<string,V>(b.Key("durationTicks"), b.String(value.DurationTicks.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("ticksPerSecond"), b.Integer(value.TicksPerSecond)));
fields.Add(new KeyValuePair<string,V>(b.Key("metrics"), Map(value.Metrics, pair0 => b.String(pair0.Value.ToString(CultureInfo.InvariantCulture)), b)));
return V.Object(fields); }
private static GameplayCompletionCommand ReadGameplayCompletionCommand(V value, MappingBudget b) { b.Node(); return new GameplayCompletionCommand(
value.Properties["contentId"].StringValue,
checked((int)value.Properties["contentVersion"].IntegerValue),
value.Properties["mode"].StringValue,
value.Properties["difficulty"].StringValue,
value.Properties["success"].BooleanValue,
long.Parse(value.Properties["score"].StringValue, CultureInfo.InvariantCulture),
ReadGameplayValidation(value.Properties["validation"], b),
value.Properties.ContainsKey("extension") ? WireOptional<CommonExtension>.Present(ReadCommonExtension(value.Properties["extension"], b)) : default,
value.Properties["session"].StringValue,
long.Parse(value.Properties["durationTicks"].StringValue, CultureInfo.InvariantCulture),
checked((int)value.Properties["ticksPerSecond"].IntegerValue),
value.Properties["metrics"].Properties.ToDictionary(pair0 => pair0.Key, pair0 => long.Parse(pair0.Value.StringValue, CultureInfo.InvariantCulture), StringComparer.Ordinal)); }
private static V WriteProfileProfile(ProfileProfile value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("displayName"), b.String(value.DisplayName)));
if(value.AvatarKey.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("avatarKey"), (value.AvatarKey.Value == null ? b.Null() : b.String(value.AvatarKey.Value!))));
if(value.Locale.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("locale"), b.String(value.Locale.Value)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("updatedAt"), b.Integer(value.UpdatedAt)));
return V.Object(fields); }
private static ProfileProfile ReadProfileProfile(V value, MappingBudget b) { b.Node(); return new ProfileProfile(
value.Properties["displayName"].StringValue,
value.Properties.ContainsKey("avatarKey") ? WireOptional<string?>.Present((value.Properties["avatarKey"].Kind == K.Null ? (string?)null : value.Properties["avatarKey"].StringValue)) : default,
value.Properties.ContainsKey("locale") ? WireOptional<string>.Present(value.Properties["locale"].StringValue) : default,
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
checked((long)value.Properties["updatedAt"].IntegerValue)); }
private static V WriteProfileResponse(ProfileResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("profile"), WriteProfileProfile(value.Profile, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static ProfileResponse ReadProfileResponse(V value, MappingBudget b) { b.Node(); return new ProfileResponse(
ReadProfileProfile(value.Properties["profile"], b),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WriteProfilePatchCommand(ProfilePatchCommand value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("expectedRevision"), b.String(value.ExpectedRevision.ToString(CultureInfo.InvariantCulture))));
if(value.DisplayName.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("displayName"), b.String(value.DisplayName.Value)));
if(value.AvatarKey.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("avatarKey"), (value.AvatarKey.Value == null ? b.Null() : b.String(value.AvatarKey.Value!))));
if(value.Locale.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key("locale"), b.String(value.Locale.Value)));
return V.Object(fields); }
private static ProfilePatchCommand ReadProfilePatchCommand(V value, MappingBudget b) { b.Node(); return new ProfilePatchCommand(
long.Parse(value.Properties["expectedRevision"].StringValue, CultureInfo.InvariantCulture),
value.Properties.ContainsKey("displayName") ? WireOptional<string>.Present(value.Properties["displayName"].StringValue) : default,
value.Properties.ContainsKey("avatarKey") ? WireOptional<string?>.Present((value.Properties["avatarKey"].Kind == K.Null ? (string?)null : value.Properties["avatarKey"].StringValue)) : default,
value.Properties.ContainsKey("locale") ? WireOptional<string>.Present(value.Properties["locale"].StringValue) : default); }
private static V WriteProjectionProfileKey(ProjectionProfileKey value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("profile"), b.String(value.Profile)));
return V.Object(fields); }
private static ProjectionProfileKey ReadProjectionProfileKey(V value, MappingBudget b) { b.Node(); return new ProjectionProfileKey(
); }
private static V WriteProjectionProgressionKey(ProjectionProgressionKey value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("stateKey"), b.String(value.StateKey)));
return V.Object(fields); }
private static ProjectionProgressionKey ReadProjectionProgressionKey(V value, MappingBudget b) { b.Node(); return new ProjectionProgressionKey(
value.Properties["stateKey"].StringValue); }
private static V WriteProjectionInventoryKey(ProjectionInventoryKey value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("itemId"), b.String(value.ItemId)));
return V.Object(fields); }
private static ProjectionInventoryKey ReadProjectionInventoryKey(V value, MappingBudget b) { b.Node(); return new ProjectionInventoryKey(
value.Properties["itemId"].StringValue); }
private static V WriteProjectionWalletKey(ProjectionWalletKey value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("currencyId"), b.String(value.CurrencyId)));
return V.Object(fields); }
private static ProjectionWalletKey ReadProjectionWalletKey(V value, MappingBudget b) { b.Node(); return new ProjectionWalletKey(
value.Properties["currencyId"].StringValue); }
private static V WriteProjectionEntitlementKey(ProjectionEntitlementKey value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entitlementId"), b.String(value.EntitlementId)));
return V.Object(fields); }
private static ProjectionEntitlementKey ReadProjectionEntitlementKey(V value, MappingBudget b) { b.Node(); return new ProjectionEntitlementKey(
value.Properties["entitlementId"].StringValue); }
private static V WriteProjectionProgressionData(ProjectionProgressionData value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("value"), b.String(value.Value.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static ProjectionProgressionData ReadProjectionProgressionData(V value, MappingBudget b) { b.Node(); return new ProjectionProgressionData(
long.Parse(value.Properties["value"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteProjectionInventoryData(ProjectionInventoryData value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("quantity"), b.String(value.Quantity.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static ProjectionInventoryData ReadProjectionInventoryData(V value, MappingBudget b) { b.Node(); return new ProjectionInventoryData(
long.Parse(value.Properties["quantity"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteProjectionWalletData(ProjectionWalletData value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("balance"), b.String(value.Balance.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static ProjectionWalletData ReadProjectionWalletData(V value, MappingBudget b) { b.Node(); return new ProjectionWalletData(
long.Parse(value.Properties["balance"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteProjectionEntitlementData(ProjectionEntitlementData value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("active"), b.Boolean(value.Active)));
fields.Add(new KeyValuePair<string,V>(b.Key("expiresAt"), (value.ExpiresAt == null ? b.Null() : b.Integer(value.ExpiresAt.Value!))));
return V.Object(fields); }
private static ProjectionEntitlementData ReadProjectionEntitlementData(V value, MappingBudget b) { b.Node(); return new ProjectionEntitlementData(
value.Properties["active"].BooleanValue,
(value.Properties["expiresAt"].Kind == K.Null ? (long?)null : checked((long)value.Properties["expiresAt"].IntegerValue))); }
private static V WriteProjectionProfileSnapshot(ProjectionProfileSnapshot value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("collection"), b.String(value.Collection)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProfileKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProfileProfile(value.Data, b)));
return V.Object(fields); }
private static ProjectionProfileSnapshot ReadProjectionProfileSnapshot(V value, MappingBudget b) { b.Node(); return new ProjectionProfileSnapshot(
ReadProjectionProfileKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProfileProfile(value.Properties["data"], b)); }
private static V WriteProjectionProgressionSnapshot(ProjectionProgressionSnapshot value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("collection"), b.String(value.Collection)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProgressionKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionProgressionData(value.Data, b)));
return V.Object(fields); }
private static ProjectionProgressionSnapshot ReadProjectionProgressionSnapshot(V value, MappingBudget b) { b.Node(); return new ProjectionProgressionSnapshot(
ReadProjectionProgressionKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionProgressionData(value.Properties["data"], b)); }
private static V WriteProjectionInventorySnapshot(ProjectionInventorySnapshot value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("collection"), b.String(value.Collection)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionInventoryKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionInventoryData(value.Data, b)));
return V.Object(fields); }
private static ProjectionInventorySnapshot ReadProjectionInventorySnapshot(V value, MappingBudget b) { b.Node(); return new ProjectionInventorySnapshot(
ReadProjectionInventoryKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionInventoryData(value.Properties["data"], b)); }
private static V WriteProjectionWalletSnapshot(ProjectionWalletSnapshot value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("collection"), b.String(value.Collection)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionWalletKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionWalletData(value.Data, b)));
return V.Object(fields); }
private static ProjectionWalletSnapshot ReadProjectionWalletSnapshot(V value, MappingBudget b) { b.Node(); return new ProjectionWalletSnapshot(
ReadProjectionWalletKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionWalletData(value.Properties["data"], b)); }
private static V WriteProjectionEntitlementSnapshot(ProjectionEntitlementSnapshot value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("collection"), b.String(value.Collection)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionEntitlementKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionEntitlementData(value.Data, b)));
return V.Object(fields); }
private static ProjectionEntitlementSnapshot ReadProjectionEntitlementSnapshot(V value, MappingBudget b) { b.Node(); return new ProjectionEntitlementSnapshot(
ReadProjectionEntitlementKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionEntitlementData(value.Properties["data"], b)); }
private static V WriteProjectionProfileUpsert(ProjectionProfileUpsert value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProfileKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProfileProfile(value.Data, b)));
return V.Object(fields); }
private static ProjectionProfileUpsert ReadProjectionProfileUpsert(V value, MappingBudget b) { b.Node(); return new ProjectionProfileUpsert(
ReadProjectionProfileKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProfileProfile(value.Properties["data"], b)); }
private static V WriteProjectionProgressionUpsert(ProjectionProgressionUpsert value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProgressionKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionProgressionData(value.Data, b)));
return V.Object(fields); }
private static ProjectionProgressionUpsert ReadProjectionProgressionUpsert(V value, MappingBudget b) { b.Node(); return new ProjectionProgressionUpsert(
ReadProjectionProgressionKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionProgressionData(value.Properties["data"], b)); }
private static V WriteProjectionInventoryUpsert(ProjectionInventoryUpsert value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionInventoryKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionInventoryData(value.Data, b)));
return V.Object(fields); }
private static ProjectionInventoryUpsert ReadProjectionInventoryUpsert(V value, MappingBudget b) { b.Node(); return new ProjectionInventoryUpsert(
ReadProjectionInventoryKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionInventoryData(value.Properties["data"], b)); }
private static V WriteProjectionWalletUpsert(ProjectionWalletUpsert value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionWalletKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionWalletData(value.Data, b)));
return V.Object(fields); }
private static ProjectionWalletUpsert ReadProjectionWalletUpsert(V value, MappingBudget b) { b.Node(); return new ProjectionWalletUpsert(
ReadProjectionWalletKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionWalletData(value.Properties["data"], b)); }
private static V WriteProjectionEntitlementUpsert(ProjectionEntitlementUpsert value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionEntitlementKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("data"), WriteProjectionEntitlementData(value.Data, b)));
return V.Object(fields); }
private static ProjectionEntitlementUpsert ReadProjectionEntitlementUpsert(V value, MappingBudget b) { b.Node(); return new ProjectionEntitlementUpsert(
ReadProjectionEntitlementKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
ReadProjectionEntitlementData(value.Properties["data"], b)); }
private static V WriteProjectionProfileRemovalValue(ProjectionProfileRemovalValue value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProfileKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
return V.Object(fields); }
private static ProjectionProfileRemovalValue ReadProjectionProfileRemovalValue(V value, MappingBudget b) { b.Node(); return new ProjectionProfileRemovalValue(
ReadProjectionProfileKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["kind"].StringValue); }
private static V WriteProjectionProgressionRemoval(ProjectionProgressionRemoval value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionProgressionKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
return V.Object(fields); }
private static ProjectionProgressionRemoval ReadProjectionProgressionRemoval(V value, MappingBudget b) { b.Node(); return new ProjectionProgressionRemoval(
ReadProjectionProgressionKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["kind"].StringValue); }
private static V WriteProjectionInventoryRemoval(ProjectionInventoryRemoval value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionInventoryKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
return V.Object(fields); }
private static ProjectionInventoryRemoval ReadProjectionInventoryRemoval(V value, MappingBudget b) { b.Node(); return new ProjectionInventoryRemoval(
ReadProjectionInventoryKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["kind"].StringValue); }
private static V WriteProjectionWalletRemoval(ProjectionWalletRemoval value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionWalletKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
return V.Object(fields); }
private static ProjectionWalletRemoval ReadProjectionWalletRemoval(V value, MappingBudget b) { b.Node(); return new ProjectionWalletRemoval(
ReadProjectionWalletKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["kind"].StringValue); }
private static V WriteProjectionEntitlementRemoval(ProjectionEntitlementRemoval value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("entityType"), b.String(value.EntityType)));
fields.Add(new KeyValuePair<string,V>(b.Key("entityKey"), WriteProjectionEntitlementKey(value.EntityKey, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("revision"), b.String(value.Revision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
return V.Object(fields); }
private static ProjectionEntitlementRemoval ReadProjectionEntitlementRemoval(V value, MappingBudget b) { b.Node(); return new ProjectionEntitlementRemoval(
ReadProjectionEntitlementKey(value.Properties["entityKey"], b),
long.Parse(value.Properties["revision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["kind"].StringValue); }
private static V WriteProvisionRequest(ProvisionRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
return V.Object(fields); }
private static ProvisionRequest ReadProvisionRequest(V value, MappingBudget b) { b.Node(); return new ProvisionRequest(
Guid.ParseExact(value.Properties["installationId"].StringValue, "D"),
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D")); }
private static V WriteProvisionResponse(ProvisionResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("account"), WriteCommonAccount(value.Account, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("membership"), WriteCommonMembership(value.Membership, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("nextSequence"), b.String(value.NextSequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static ProvisionResponse ReadProvisionResponse(V value, MappingBudget b) { b.Node(); return new ProvisionResponse(
ReadCommonAccount(value.Properties["account"], b),
ReadCommonMembership(value.Properties["membership"], b),
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
long.Parse(value.Properties["nextSequence"].StringValue, CultureInfo.InvariantCulture),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WritePullRequest(PullRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("cursor"), b.String(value.Cursor)));
fields.Add(new KeyValuePair<string,V>(b.Key("maxBytes"), b.Integer(value.MaxBytes)));
return V.Object(fields); }
private static PullRequest ReadPullRequest(V value, MappingBudget b) { b.Node(); return new PullRequest(
value.Properties["cursor"].StringValue,
checked((int)value.Properties["maxBytes"].IntegerValue)); }
private static V WritePullGroup(PullGroup value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("feedRevision"), b.String(value.FeedRevision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("changes"), Array(value.Changes, item0 => WriteIProjectionChange(item0, b), b)));
return V.Object(fields); }
private static PullGroup ReadPullGroup(V value, MappingBudget b) { b.Node(); return new PullGroup(
long.Parse(value.Properties["feedRevision"].StringValue, CultureInfo.InvariantCulture),
value.Properties["changes"].Items.Select(item0 => ReadIProjectionChange(item0, b)).ToArray()); }
private static V WritePullPage(PullPage value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("changes"), Array(value.Changes, item0 => WritePullGroup(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("nextCursor"), b.String(value.NextCursor)));
fields.Add(new KeyValuePair<string,V>(b.Key("hasMore"), b.Boolean(value.HasMore)));
fields.Add(new KeyValuePair<string,V>(b.Key("resetRequired"), b.Boolean(value.ResetRequired)));
fields.Add(new KeyValuePair<string,V>(b.Key("committedThrough"), b.String(value.CommittedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static PullPage ReadPullPage(V value, MappingBudget b) { b.Node(); return new PullPage(
value.Properties["changes"].Items.Select(item0 => ReadPullGroup(item0, b)).ToArray(),
value.Properties["nextCursor"].StringValue,
value.Properties["hasMore"].BooleanValue,
long.Parse(value.Properties["committedThrough"].StringValue, CultureInfo.InvariantCulture),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WritePullReset(PullReset value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("changes"), Array(value.Changes, item0 => WriteIProjectionChange(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("nextCursor"), (value.NextCursor == null ? b.Null() : b.String(value.NextCursor!))));
fields.Add(new KeyValuePair<string,V>(b.Key("hasMore"), b.Boolean(value.HasMore)));
fields.Add(new KeyValuePair<string,V>(b.Key("resetRequired"), b.Boolean(value.ResetRequired)));
fields.Add(new KeyValuePair<string,V>(b.Key("reason"), b.String(value.Reason)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static PullReset ReadPullReset(V value, MappingBudget b) { b.Node(); return new PullReset(
value.Properties["changes"].Items.Select(item0 => ReadIProjectionChange(item0, b)).ToArray(),
(value.Properties["nextCursor"].Kind == K.Null ? (string?)null : value.Properties["nextCursor"].StringValue),
value.Properties["reason"].StringValue,
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WritePushOperation(PushOperation value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("type"), b.String(value.Type)));
fields.Add(new KeyValuePair<string,V>(b.Key("schemaVersion"), b.Integer(value.SchemaVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientCreatedAt"), b.Integer(value.ClientCreatedAt)));
fields.Add(new KeyValuePair<string,V>(b.Key("payload"), WriteIPushPayload(value.Payload, b)));
return V.Object(fields); }
private static PushOperation ReadPushOperation(V value, MappingBudget b) { b.Node(); return new PushOperation(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
Guid.ParseExact(value.Properties["installationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
value.Properties["type"].StringValue,
checked((long)value.Properties["clientCreatedAt"].IntegerValue),
ReadIPushPayload(value.Properties["payload"], b)); }
private static V WritePushRequest(PushRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("operations"), Array(value.Operations, item0 => WritePushOperation(item0, b), b)));
return V.Object(fields); }
private static PushRequest ReadPushRequest(V value, MappingBudget b) { b.Node(); return new PushRequest(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
value.Properties["operations"].Items.Select(item0 => ReadPushOperation(item0, b)).ToArray()); }
private static V WritePushAccepted(PushAccepted value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("feedRevision"), b.String(value.FeedRevision.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("result"), WriteIPushAcceptedResult(value.Result, b)));
return V.Object(fields); }
private static PushAccepted ReadPushAccepted(V value, MappingBudget b) { b.Node(); return new PushAccepted(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
long.Parse(value.Properties["feedRevision"].StringValue, CultureInfo.InvariantCulture),
ReadIPushAcceptedResult(value.Properties["result"], b)); }
private static V WritePushTerminalRejected(PushTerminalRejected value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("error"), WriteCommonError(value.Error, b)));
return V.Object(fields); }
private static PushTerminalRejected ReadPushTerminalRejected(V value, MappingBudget b) { b.Node(); return new PushTerminalRejected(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
ReadCommonError(value.Properties["error"], b)); }
private static V WritePushWaiting(PushWaiting value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("expectedSequence"), b.String(value.ExpectedSequence.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static PushWaiting ReadPushWaiting(V value, MappingBudget b) { b.Node(); return new PushWaiting(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
long.Parse(value.Properties["expectedSequence"].StringValue, CultureInfo.InvariantCulture)); }
private static V WritePushRetryable(PushRetryable value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("error"), WriteCommonError(value.Error, b)));
return V.Object(fields); }
private static PushRetryable ReadPushRetryable(V value, MappingBudget b) { b.Node(); return new PushRetryable(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
ReadCommonError(value.Properties["error"], b)); }
private static V WritePushUpgradeRequired(PushUpgradeRequired value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("status"), b.String(value.Status)));
fields.Add(new KeyValuePair<string,V>(b.Key("minimumProtocolVersion"), b.Integer(value.MinimumProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("error"), WriteCommonError(value.Error, b)));
return V.Object(fields); }
private static PushUpgradeRequired ReadPushUpgradeRequired(V value, MappingBudget b) { b.Node(); return new PushUpgradeRequired(
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
checked((int)value.Properties["minimumProtocolVersion"].IntegerValue),
ReadCommonError(value.Properties["error"], b)); }
private static V WritePushResponse(PushResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("operationResults"), Array(value.OperationResults, item0 => WriteIPushResult(item0, b), b)));
fields.Add(new KeyValuePair<string,V>(b.Key("finalizedThrough"), b.String(value.FinalizedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static PushResponse ReadPushResponse(V value, MappingBudget b) { b.Node(); return new PushResponse(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
value.Properties["operationResults"].Items.Select(item0 => ReadIPushResult(item0, b)).ToArray(),
long.Parse(value.Properties["finalizedThrough"].StringValue, CultureInfo.InvariantCulture),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WriteRecoveryReceiptRequest(RecoveryReceiptRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("sequence"), b.String(value.Sequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("fingerprint"), b.String(value.Fingerprint)));
return V.Object(fields); }
private static RecoveryReceiptRequest ReadRecoveryReceiptRequest(V value, MappingBudget b) { b.Node(); return new RecoveryReceiptRequest(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
Guid.ParseExact(value.Properties["installationId"].StringValue, "D"),
long.Parse(value.Properties["sequence"].StringValue, CultureInfo.InvariantCulture),
value.Properties["fingerprint"].StringValue); }
private static V WriteRecoveryReceiptResponse(RecoveryReceiptResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("found"), b.Boolean(value.Found)));
fields.Add(new KeyValuePair<string,V>(b.Key("result"), (value.Result == null ? b.Null() : WriteIPushFinalResult(value.Result!, b))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
fields.Add(new KeyValuePair<string,V>(b.Key("operationId"), b.String(value.OperationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("observedFinalizedThrough"), b.String(value.ObservedFinalizedThrough.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static RecoveryReceiptResponse ReadRecoveryReceiptResponse(V value, MappingBudget b) { b.Node(); return new RecoveryReceiptResponse(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
value.Properties["found"].BooleanValue,
(value.Properties["result"].Kind == K.Null ? (IPushFinalResult?)null : ReadIPushFinalResult(value.Properties["result"], b)),
checked((long)value.Properties["serverTime"].IntegerValue),
Guid.ParseExact(value.Properties["operationId"].StringValue, "D"),
long.Parse(value.Properties["observedFinalizedThrough"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteRecoveryStreamRequest(RecoveryStreamRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("priorClientStreamId"), b.String(value.PriorClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("newClientStreamId"), b.String(value.NewClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("reason"), b.String(value.Reason)));
fields.Add(new KeyValuePair<string,V>(b.Key("recoveryId"), b.String(value.RecoveryId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("inspectionToken"), b.String(value.InspectionToken)));
fields.Add(new KeyValuePair<string,V>(b.Key("expectedFinalizedThrough"), b.String(value.ExpectedFinalizedThrough.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static RecoveryStreamRequest ReadRecoveryStreamRequest(V value, MappingBudget b) { b.Node(); return new RecoveryStreamRequest(
Guid.ParseExact(value.Properties["installationId"].StringValue, "D"),
Guid.ParseExact(value.Properties["priorClientStreamId"].StringValue, "D"),
Guid.ParseExact(value.Properties["newClientStreamId"].StringValue, "D"),
value.Properties["reason"].StringValue,
Guid.ParseExact(value.Properties["recoveryId"].StringValue, "D"),
value.Properties["inspectionToken"].StringValue,
long.Parse(value.Properties["expectedFinalizedThrough"].StringValue, CultureInfo.InvariantCulture)); }
private static V WriteRecoveryStreamResponse(RecoveryStreamResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("priorClientStreamId"), b.String(value.PriorClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("newClientStreamId"), b.String(value.NewClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("nextSequence"), b.String(value.NextSequence.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("priorFinalizedThrough"), b.String(value.PriorFinalizedThrough.ToString(CultureInfo.InvariantCulture))));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
fields.Add(new KeyValuePair<string,V>(b.Key("recoveryId"), b.String(value.RecoveryId.ToString("D"))));
return V.Object(fields); }
private static RecoveryStreamResponse ReadRecoveryStreamResponse(V value, MappingBudget b) { b.Node(); return new RecoveryStreamResponse(
Guid.ParseExact(value.Properties["priorClientStreamId"].StringValue, "D"),
Guid.ParseExact(value.Properties["newClientStreamId"].StringValue, "D"),
long.Parse(value.Properties["nextSequence"].StringValue, CultureInfo.InvariantCulture),
long.Parse(value.Properties["priorFinalizedThrough"].StringValue, CultureInfo.InvariantCulture),
checked((long)value.Properties["serverTime"].IntegerValue),
Guid.ParseExact(value.Properties["recoveryId"].StringValue, "D")); }
private static V WriteRecoveryInspectRequest(RecoveryInspectRequest value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("clientStreamId"), b.String(value.ClientStreamId.ToString("D"))));
fields.Add(new KeyValuePair<string,V>(b.Key("installationId"), b.String(value.InstallationId.ToString("D"))));
return V.Object(fields); }
private static RecoveryInspectRequest ReadRecoveryInspectRequest(V value, MappingBudget b) { b.Node(); return new RecoveryInspectRequest(
Guid.ParseExact(value.Properties["clientStreamId"].StringValue, "D"),
Guid.ParseExact(value.Properties["installationId"].StringValue, "D")); }
private static V WriteRecoveryInspectResponse(RecoveryInspectResponse value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("protocolVersion"), b.Integer(value.ProtocolVersion)));
fields.Add(new KeyValuePair<string,V>(b.Key("streamState"), WriteCommonStreamState(value.StreamState, b)));
fields.Add(new KeyValuePair<string,V>(b.Key("inspectionToken"), b.String(value.InspectionToken)));
fields.Add(new KeyValuePair<string,V>(b.Key("expiresAt"), b.Integer(value.ExpiresAt)));
fields.Add(new KeyValuePair<string,V>(b.Key("serverTime"), b.Integer(value.ServerTime)));
return V.Object(fields); }
private static RecoveryInspectResponse ReadRecoveryInspectResponse(V value, MappingBudget b) { b.Node(); return new RecoveryInspectResponse(
ReadCommonStreamState(value.Properties["streamState"], b),
value.Properties["inspectionToken"].StringValue,
checked((long)value.Properties["expiresAt"].IntegerValue),
checked((long)value.Properties["serverTime"].IntegerValue)); }
private static V WritePushProfileUpdatedResult(PushProfileUpdatedResult value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("profileRevision"), b.String(value.ProfileRevision.ToString(CultureInfo.InvariantCulture))));
return V.Object(fields); }
private static PushProfileUpdatedResult ReadPushProfileUpdatedResult(V value, MappingBudget b) { b.Node(); return new PushProfileUpdatedResult(
long.Parse(value.Properties["profileRevision"].StringValue, CultureInfo.InvariantCulture)); }
private static V WritePushGameplayCompletionRecordedResult(PushGameplayCompletionRecordedResult value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("kind"), b.String(value.Kind)));
fields.Add(new KeyValuePair<string,V>(b.Key("completionRecorded"), b.Boolean(value.CompletionRecorded)));
return V.Object(fields); }
private static PushGameplayCompletionRecordedResult ReadPushGameplayCompletionRecordedResult(V value, MappingBudget b) { b.Node(); return new PushGameplayCompletionRecordedResult(
); }
private static V WriteGameplayValidation(GameplayValidation value, MappingBudget b) { b.Node(); var fields=new List<KeyValuePair<string,V>>();
fields.Add(new KeyValuePair<string,V>(b.Key("scheme"), b.String(value.Scheme)));
fields.Add(new KeyValuePair<string,V>(b.Key("reference"), b.String(value.Reference)));
return V.Object(fields); }
private static GameplayValidation ReadGameplayValidation(V value, MappingBudget b) { b.Node(); return new GameplayValidation(
value.Properties["scheme"].StringValue,
value.Properties["reference"].StringValue); }
private static V WriteIProjectionSnapshotEntity(IProjectionSnapshotEntity value, MappingBudget b) { switch(value) {
case ProjectionProfileSnapshot dto: return WriteProjectionProfileSnapshot(dto,b);
case ProjectionProgressionSnapshot dto: return WriteProjectionProgressionSnapshot(dto,b);
case ProjectionInventorySnapshot dto: return WriteProjectionInventorySnapshot(dto,b);
case ProjectionWalletSnapshot dto: return WriteProjectionWalletSnapshot(dto,b);
case ProjectionEntitlementSnapshot dto: return WriteProjectionEntitlementSnapshot(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionSnapshotEntity ReadIProjectionSnapshotEntity(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/profileSnapshot",value)) return ReadProjectionProfileSnapshot(value,b);
if(Matches("projection.schema.json#/$defs/progressionSnapshot",value)) return ReadProjectionProgressionSnapshot(value,b);
if(Matches("projection.schema.json#/$defs/inventorySnapshot",value)) return ReadProjectionInventorySnapshot(value,b);
if(Matches("projection.schema.json#/$defs/walletSnapshot",value)) return ReadProjectionWalletSnapshot(value,b);
if(Matches("projection.schema.json#/$defs/entitlementSnapshot",value)) return ReadProjectionEntitlementSnapshot(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionChange(IProjectionChange value, MappingBudget b) { switch(value) {
case ProjectionProfileUpsert dto: return WriteProjectionProfileUpsert(dto,b);
case ProjectionProfileRemovalValue dto: return WriteProjectionProfileRemovalValue(dto,b);
case ProjectionProgressionUpsert dto: return WriteProjectionProgressionUpsert(dto,b);
case ProjectionProgressionRemoval dto: return WriteProjectionProgressionRemoval(dto,b);
case ProjectionInventoryUpsert dto: return WriteProjectionInventoryUpsert(dto,b);
case ProjectionInventoryRemoval dto: return WriteProjectionInventoryRemoval(dto,b);
case ProjectionWalletUpsert dto: return WriteProjectionWalletUpsert(dto,b);
case ProjectionWalletRemoval dto: return WriteProjectionWalletRemoval(dto,b);
case ProjectionEntitlementUpsert dto: return WriteProjectionEntitlementUpsert(dto,b);
case ProjectionEntitlementRemoval dto: return WriteProjectionEntitlementRemoval(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionChange ReadIProjectionChange(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/profileUpsert",value)) return ReadProjectionProfileUpsert(value,b);
if(Matches("projection.schema.json#/$defs/profileRemovalValue",value)) return ReadProjectionProfileRemovalValue(value,b);
if(Matches("projection.schema.json#/$defs/progressionUpsert",value)) return ReadProjectionProgressionUpsert(value,b);
if(Matches("projection.schema.json#/$defs/progressionRemoval",value)) return ReadProjectionProgressionRemoval(value,b);
if(Matches("projection.schema.json#/$defs/inventoryUpsert",value)) return ReadProjectionInventoryUpsert(value,b);
if(Matches("projection.schema.json#/$defs/inventoryRemoval",value)) return ReadProjectionInventoryRemoval(value,b);
if(Matches("projection.schema.json#/$defs/walletUpsert",value)) return ReadProjectionWalletUpsert(value,b);
if(Matches("projection.schema.json#/$defs/walletRemoval",value)) return ReadProjectionWalletRemoval(value,b);
if(Matches("projection.schema.json#/$defs/entitlementUpsert",value)) return ReadProjectionEntitlementUpsert(value,b);
if(Matches("projection.schema.json#/$defs/entitlementRemoval",value)) return ReadProjectionEntitlementRemoval(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionProfileChangeUnion(IProjectionProfileChangeUnion value, MappingBudget b) { switch(value) {
case ProjectionProfileUpsert dto: return WriteProjectionProfileUpsert(dto,b);
case ProjectionProfileRemovalValue dto: return WriteProjectionProfileRemovalValue(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionProfileChangeUnion ReadIProjectionProfileChangeUnion(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/profileUpsert",value)) return ReadProjectionProfileUpsert(value,b);
if(Matches("projection.schema.json#/$defs/profileRemovalValue",value)) return ReadProjectionProfileRemovalValue(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionProgressionChange(IProjectionProgressionChange value, MappingBudget b) { switch(value) {
case ProjectionProgressionUpsert dto: return WriteProjectionProgressionUpsert(dto,b);
case ProjectionProgressionRemoval dto: return WriteProjectionProgressionRemoval(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionProgressionChange ReadIProjectionProgressionChange(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/progressionUpsert",value)) return ReadProjectionProgressionUpsert(value,b);
if(Matches("projection.schema.json#/$defs/progressionRemoval",value)) return ReadProjectionProgressionRemoval(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionInventoryChange(IProjectionInventoryChange value, MappingBudget b) { switch(value) {
case ProjectionInventoryUpsert dto: return WriteProjectionInventoryUpsert(dto,b);
case ProjectionInventoryRemoval dto: return WriteProjectionInventoryRemoval(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionInventoryChange ReadIProjectionInventoryChange(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/inventoryUpsert",value)) return ReadProjectionInventoryUpsert(value,b);
if(Matches("projection.schema.json#/$defs/inventoryRemoval",value)) return ReadProjectionInventoryRemoval(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionWalletChange(IProjectionWalletChange value, MappingBudget b) { switch(value) {
case ProjectionWalletUpsert dto: return WriteProjectionWalletUpsert(dto,b);
case ProjectionWalletRemoval dto: return WriteProjectionWalletRemoval(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionWalletChange ReadIProjectionWalletChange(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/walletUpsert",value)) return ReadProjectionWalletUpsert(value,b);
if(Matches("projection.schema.json#/$defs/walletRemoval",value)) return ReadProjectionWalletRemoval(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIProjectionEntitlementChange(IProjectionEntitlementChange value, MappingBudget b) { switch(value) {
case ProjectionEntitlementUpsert dto: return WriteProjectionEntitlementUpsert(dto,b);
case ProjectionEntitlementRemoval dto: return WriteProjectionEntitlementRemoval(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IProjectionEntitlementChange ReadIProjectionEntitlementChange(V value, MappingBudget b) {
if(Matches("projection.schema.json#/$defs/entitlementUpsert",value)) return ReadProjectionEntitlementUpsert(value,b);
if(Matches("projection.schema.json#/$defs/entitlementRemoval",value)) return ReadProjectionEntitlementRemoval(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIPullResponse(IPullResponse value, MappingBudget b) { switch(value) {
case PullPage dto: return WritePullPage(dto,b);
case PullReset dto: return WritePullReset(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IPullResponse ReadIPullResponse(V value, MappingBudget b) {
if(Matches("pull.schema.json#/$defs/page",value)) return ReadPullPage(value,b);
if(Matches("pull.schema.json#/$defs/reset",value)) return ReadPullReset(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIPushResult(IPushResult value, MappingBudget b) { switch(value) {
case PushAccepted dto: return WritePushAccepted(dto,b);
case PushTerminalRejected dto: return WritePushTerminalRejected(dto,b);
case PushWaiting dto: return WritePushWaiting(dto,b);
case PushRetryable dto: return WritePushRetryable(dto,b);
case PushUpgradeRequired dto: return WritePushUpgradeRequired(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IPushResult ReadIPushResult(V value, MappingBudget b) {
if(Matches("push.schema.json#/$defs/accepted",value)) return ReadPushAccepted(value,b);
if(Matches("push.schema.json#/$defs/terminalRejected",value)) return ReadPushTerminalRejected(value,b);
if(Matches("push.schema.json#/$defs/waiting",value)) return ReadPushWaiting(value,b);
if(Matches("push.schema.json#/$defs/retryable",value)) return ReadPushRetryable(value,b);
if(Matches("push.schema.json#/$defs/upgradeRequired",value)) return ReadPushUpgradeRequired(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIPushAcceptedResult(IPushAcceptedResult value, MappingBudget b) { switch(value) {
case PushProfileUpdatedResult dto: return WritePushProfileUpdatedResult(dto,b);
case PushGameplayCompletionRecordedResult dto: return WritePushGameplayCompletionRecordedResult(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IPushAcceptedResult ReadIPushAcceptedResult(V value, MappingBudget b) {
if(Matches("push.schema.json#/$defs/accepted/properties/result/oneOf/0",value)) return ReadPushProfileUpdatedResult(value,b);
if(Matches("push.schema.json#/$defs/accepted/properties/result/oneOf/1",value)) return ReadPushGameplayCompletionRecordedResult(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIPushPayload(IPushPayload value, MappingBudget b) { switch(value) {
case ProfilePatchCommand dto: return WriteProfilePatchCommand(dto,b);
case GameplayCompletionCommand dto: return WriteGameplayCompletionCommand(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IPushPayload ReadIPushPayload(V value, MappingBudget b) {
if(Matches("profile.schema.json#/$defs/patchCommand",value)) return ReadProfilePatchCommand(value,b);
if(Matches("gameplay.schema.json#/$defs/completionCommand",value)) return ReadGameplayCompletionCommand(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }
private static V WriteIPushFinalResult(IPushFinalResult value, MappingBudget b) { switch(value) {
case PushAccepted dto: return WritePushAccepted(dto,b);
case PushTerminalRejected dto: return WritePushTerminalRejected(dto,b);
default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }
private static IPushFinalResult ReadIPushFinalResult(V value, MappingBudget b) {
if(Matches("push.schema.json#/$defs/accepted",value)) return ReadPushAccepted(value,b);
if(Matches("push.schema.json#/$defs/terminalRejected",value)) return ReadPushTerminalRejected(value,b);
throw new QualificationCodecException("Unsupported DTO union value."); }

private static bool Matches(string schema,V value) { try { QualificationSchemaValidator.Validate(schema,value); return true; } catch(QualificationCodecException) { return false; } }
// Reserve three logical copies for source DTO/tree, mapped tree/DTO and constructor temporary containers.
private sealed class MappingBudget {
 private readonly int copies;
 internal MappingBudget(int copies=3) { this.copies=copies; }
 private long nodes,units;
 internal void Node() { nodes=checked(nodes+copies); Units(32); if(nodes>16384) throw new QualificationCodecException("DTO mapping node budget."); }
 internal void Units(long count) { units=checked(units+count*copies); if(units>2097152) throw new QualificationCodecException("DTO mapping allocation budget."); }
 internal string Key(string text) { String(text); Units(16); return text; }
 internal V String(string text) { Node(); if(V.Utf8.GetByteCount(text)>8192) throw new QualificationCodecException("DTO string budget."); Units(text.Length*2L); return V.String(text); }
 internal V Integer(long n) { Node(); return V.Integer(n); }
 internal V Boolean(bool n) { Node(); return V.Boolean(n); }
 internal V Null() { Node(); return V.Null; }
 internal void Reserve(V v,int depth=0) {
  if(depth>32) throw new QualificationCodecException("DTO mapping depth budget.");
  if(v.Kind==K.String) { String(v.StringValue); return; } Node();
  if(v.Kind==K.Array) { Units(v.Items.Count*8L); foreach(var item in v.Items) Reserve(item,depth+1); }
  if(v.Kind==K.Object) foreach(var pair in v.Properties) { Key(pair.Key); Reserve(pair.Value,depth+1); }
 }
}
private static V Array<T>(IReadOnlyList<T> source,Func<T,V> map,MappingBudget b) { if(source.Count>1024) throw new QualificationCodecException("DTO array budget."); b.Node(); b.Units(source.Count*8L); return V.Array(source.Select(map)); }
private static V Map<T>(IReadOnlyDictionary<string,T> source,Func<KeyValuePair<string,T>,V> map,MappingBudget b) { if(source.Count>256) throw new QualificationCodecException("DTO map budget."); b.Node(); return V.Object(source.Select(p=>new KeyValuePair<string,V>(b.Key(p.Key),map(p)))); }
private static V WriteExtension(ExtensionValue v,MappingBudget b,int depth) {
 if(depth>16) throw new QualificationCodecException("DTO extension depth.");
 switch(v.Kind) {
 case ExtensionValueKind.Null:return b.Null(); case ExtensionValueKind.Boolean:return b.Boolean(v.Boolean);
 case ExtensionValueKind.String:return b.String(v.Text!);
 case ExtensionValueKind.Array:return Array(v.Items!, x=>WriteExtension(x,b,depth+1),b);
 case ExtensionValueKind.Object:return Map(v.Properties!, p=>WriteExtension(p.Value,b,depth+1),b);
 default:throw new QualificationCodecException("DTO extension kind."); } }
private static ExtensionValue ReadExtension(V v,MappingBudget b,int depth) {
 b.Node(); if(depth>16) throw new QualificationCodecException("DTO extension depth.");
 switch(v.Kind) {
 case K.Null:return ExtensionValue.Null; case K.Boolean:return ExtensionValue.FromBoolean(v.BooleanValue);
 case K.String:return ExtensionValue.FromString(v.StringValue);
 case K.Array:return ExtensionValue.FromArray(v.Items.Select(x=>ReadExtension(x,b,depth+1)).ToArray());
 case K.Object:return ExtensionValue.FromObject(v.Properties.ToDictionary(p=>p.Key,p=>ReadExtension(p.Value,b,depth+1),StringComparer.Ordinal));
 default:throw new QualificationCodecException("DTO extension kind."); } }
private static V WriteDetail(ErrorDetailValue v,MappingBudget b) { switch(v.Kind) { case ErrorDetailKind.Null:return b.Null(); case ErrorDetailKind.Boolean:return b.Boolean(v.Boolean); case ErrorDetailKind.String:return b.String(v.Text!); case ErrorDetailKind.Integer:return b.Integer(v.Integer); default:throw new QualificationCodecException("DTO detail kind."); } }
private static ErrorDetailValue ReadDetail(V v,MappingBudget b) { switch(v.Kind) { case K.Null:return ErrorDetailValue.Null; case K.Boolean:return ErrorDetailValue.FromBoolean(v.BooleanValue); case K.String:return ErrorDetailValue.FromString(v.StringValue); case K.Integer:return ErrorDetailValue.FromInteger(checked((int)v.IntegerValue)); default:throw new QualificationCodecException("DTO detail kind."); } }
} }

