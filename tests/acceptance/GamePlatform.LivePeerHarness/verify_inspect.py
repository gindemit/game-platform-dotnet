#!/usr/bin/env python3
"""Assert a C# live probe against the backend's read-only --inspect JSON."""
import json
import sys


def require(value, message):
    if not value:
        raise SystemExit("LIVE_PEER_INSPECTION_FAILED: " + message)


def main():
    require(len(sys.argv) in (3, 4), "usage: verify_inspect.py probe.json inspect.json [proxy-faults.json]")
    with open(sys.argv[1], encoding="utf-8") as handle:
        probe = json.load(handle)
    with open(sys.argv[2], encoding="utf-8") as handle:
        inspected = json.load(handle)
    require(probe.get("status") == "passed" and probe.get("mode") == "probe", "probe_not_passed")
    for key in ("sdkRevision", "backendRevision", "unityRevision"):
        require(len(probe.get(key, "")) == 40, "revision_missing_" + key)
    require(probe.get("ready") == 1 and probe.get("confirmed", 0) > 0, "local_projection_not_ready")
    require(probe.get("ownerMismatchDenied") is True, "owner_mismatch_not_checked")
    require(probe.get("repeatedOperationAccepted") is True, "operation_replay_not_checked")
    require(probe.get("duplicateSourceAccepted") is True, "business_source_not_checked")
    matches = [account for account in inspected.get("accounts", [])
               if any(operation.get("operationId") == probe.get("operation")
                      for operation in account.get("operations", []))]
    require(len(matches) == 1, "operation_not_in_one_account")
    account = matches[0]
    require(len(account.get("operations", [])) == 2, "expected_two_distinct_operations")
    for key in ("completions", "rewardGrants", "rewardGrantClaims", "feedGroups", "testCoinLedger"):
        require(len(account.get(key, [])) == 1, "duplicate_or_missing_" + key)
    require(account.get("testCoinBalance", {}).get("balance") == 1, "balance_not_one")
    require(all(operation.get("clientStreamId") == probe.get("stream")
                for operation in account["operations"]), "foreign_stream_operation")
    if len(sys.argv) == 4:
        require(probe.get("lostResponseRecovered") is True, "lost_response_not_checked")
        with open(sys.argv[3], encoding="utf-8") as handle:
            faults = json.load(handle)
        paths = list(faults.get("paths", {}).values())
        require(len(paths) == 1 and paths[0].get("dropped") == 1, "one_response_not_dropped")
        first = paths[0].get("droppedBodySha256", [])
        later = paths[0].get("laterBodySha256", [])
        require(len(first) == 1 and first[0] in later, "replay_body_not_identical")
    print(json.dumps({"status": "passed", "sdkRevision": probe["sdkRevision"],
                      "backendRevision": probe["backendRevision"], "unityRevision": probe["unityRevision"],
                      "operations": 2, "completions": 1, "rewardGrants": 1, "ledgerEntries": 1,
                      "testCoinBalance": 1, "lostResponse": len(sys.argv) == 4}, sort_keys=True))


if __name__ == "__main__":
    main()
