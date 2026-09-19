"""Read immutable Git objects; produce review evidence, never install contracts.

Requires the already available jsonschema 4.17.3 for diagnostic schema checks.
This is G1 schema tooling, not a MessagePack codec or runtime acceptance test.
"""
import argparse
import hashlib
import json
import posixpath
import re
import subprocess
from pathlib import Path

from jsonschema import Draft202012Validator, RefResolver, ValidationError, validators


def git_bytes(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args])


def load_candidate(repo, commit):
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("An immutable full commit SHA is required")
    paths = git_bytes(repo, "ls-tree", "-r", "--name-only", commit, "contracts").decode().splitlines()
    raw = {p: git_bytes(repo, "show", f"{commit}:{p}") for p in paths if p.endswith(".json")}
    docs = {p: json.loads(b) for p, b in raw.items()}
    candidate = docs["contracts/proposals/core-v1.candidate.json"]
    declared = [a["path"] for a in candidate["files"]]
    if len(declared) != len(set(declared)):
        raise ValueError("Duplicate candidate artifact path")
    uncovered = {p for p in docs if "/schemas/" in p or "/fixtures/" in p} - set(declared)
    if uncovered:
        raise ValueError(f"Unhashed candidate schema/fixture: {sorted(uncovered)}")
    for artifact in candidate["files"]:
        path = artifact["path"]
        content = raw.get(path)
        if content is None:
            content = git_bytes(repo, "show", f"{commit}:{path}")
        if hashlib.sha256(content).hexdigest() != artifact["sha256"]:
            raise ValueError(f"Candidate digest mismatch: {path}")
    return docs, raw


def resolve(docs, path, node):
    seen = set()
    while isinstance(node, dict) and "$ref" in node:
        ref = node["$ref"]
        filename, fragment = ref.split("#", 1)
        path = posixpath.normpath(posixpath.join(posixpath.dirname(path), filename)) if filename else path
        key = (path, fragment)
        if key in seen:
            raise ValueError(f"Cyclic reference: {key}")
        seen.add(key)
        node = docs[path]
        for part in fragment.lstrip("/").split("/") if fragment else []:
            node = node[part.replace("~1", "/").replace("~0", "~")]
    return path, node


def type_mapping(docs, path, node):
    ref = node.get("$ref", "")
    name = ref.rsplit("/", 1)[-1]
    if name in ("signedInt64", "positiveInt64", "nonNegativeInt64"):
        return "long (checked)", "bigint", "signed integer", "decimal string"
    if name in ("uuid", "uuidV7"):
        return "Guid (role-specific wrapper at domain boundary)", "UUID role value", "RFC-order bin16", "lowercase UUID string"
    if name == "timestamp":
        return "long (bounded Unix milliseconds)", "number (bounded safe integer)", "integer", "integer"
    path, node = resolve(docs, path, node)
    if ref and (node.get("type") == "object" or "oneOf" in node):
        label = Path(path).name.split(".")[0].title() + name[0].upper() + name[1:] + "Dto"
        return label, label, "map or declared union", "object or declared union"
    if "oneOf" in node or "anyOf" in node:
        variants = [type_mapping(docs, path, x) for x in node.get("oneOf", node.get("anyOf"))]
        return tuple(" | ".join(dict.fromkeys(v[i] for v in variants)) for i in range(4))
    kind = node.get("type")
    if kind is None and "const" in node:
        value = node["const"]
        kind = "boolean" if isinstance(value, bool) else "integer" if isinstance(value, int) else "string"
    if kind is None and "enum" in node:
        kind = "string" if all(isinstance(x, str) for x in node["enum"]) else None
    if kind == "array":
        if node.get("maxItems") == 0:
            return "empty collection", "readonly []", "empty array", "empty array"
        inner = type_mapping(docs, path, node.get("items", {}))
        return f"IReadOnlyList<{inner[0]}>", f"ReadonlyArray<{inner[1]}>", "array", "array"
    if kind == "object":
        if isinstance(node.get("additionalProperties"), dict):
            inner = type_mapping(docs, path, node["additionalProperties"])
            return f"IReadOnlyDictionary<string, {inner[0]}>", f"ReadonlyMap<string, {inner[1]}>", "map", "object"
        label = "typed record/union (see child rows and schema conditions)" if node.get("properties") else "bounded schema-selected value (review required)"
        return label, label, "map", "object"
    return {
        "string": ("string (ordinal, bounded)", "string", "UTF-8 string", "string"),
        "boolean": ("bool", "boolean", "boolean", "boolean"),
        "integer": ("long (range-check; narrow to int only within Int32)", "number (safe-integer check)", "integer", "integer"),
        "null": ("explicit null", "null", "nil", "null"),
    }.get(kind, ("REVIEW_REQUIRED",) * 4)


def field_rows(docs):
    rows = []
    def walk(path, node, pointer):
        if not isinstance(node, dict):
            return
        for name, child in node.get("properties", {}).items():
            if not isinstance(child, dict):
                continue  # false is a conditional prohibition, not a new field
            target = pointer + "/properties/" + name.replace("~", "~0").replace("/", "~1")
            cs, ts, wire, diagnostic = type_mapping(docs, path, child)
            if name == "error" and "properties" in child and "type" not in child:
                cs, ts, wire, diagnostic = "CommonErrorDto (constraint overlay)", "CommonErrorDto (constraint overlay)", "map", "object"
            if name == "payload" and child == {"type": "object"}:
                cs = ts = "ProfilePatchCommandDto | GameplayCompletionCommandDto (selected by type/schemaVersion)"
            if name == "value" and "x-recursiveScalars" in child:
                cs, ts = "BoundedExtensionValue (null/bool/string/list/map union)", "BoundedExtensionValue (null/boolean/string/array/map union)"
            if name == "nextSequence" and child == {"const": "1"}:
                cs, ts, wire, diagnostic = "long (constant 1)", "bigint (constant 1n)", "signed integer", "decimal string"
            _, resolved = resolve(docs, path, child)
            bounds = {k: v for k, v in resolved.items() if k in (
                "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems",
                "minProperties", "maxProperties", "pattern", "const", "enum",
                "x-minimumDecimal", "x-maximumDecimal", "x-maximumDepth", "x-recursiveScalars")}
            rows.append({"schema": path, "pointer": target, "field": name,
                         "presence": "required in this branch" if name in node.get("required", []) else "optional in this branch; inspect parent conditions",
                         "csharp": cs, "typescript": ts, "messagepack": wire,
                         "diagnostic_json": diagnostic, "bounds": bounds,
                         "schema_ref": child.get("$ref"),
                         "null": "allowed by explicit union/type only; otherwise reject",
                         "default": "none; preserve absence independently from null"})
        for key, value in node.items():
            if isinstance(value, dict):
                walk(path, value, pointer + "/" + key)
            elif isinstance(value, list):
                for i, item in enumerate(value):
                    walk(path, item, pointer + f"/{key}/{i}")
    for path, doc in sorted(docs.items()):
        if "/schemas/" in path:
            walk(path, doc, "#")
    return rows


def decimal_bound(keyword, compare):
    def check(validator, bound, instance, schema):
        if isinstance(instance, str) and re.fullmatch(r"-?[0-9]+", instance):
            if compare(int(instance), int(bound)):
                yield ValidationError(f"{keyword}: decimal integer outside range")
    return check


def recursive_scalars(validator, allowed, instance, schema):
    if isinstance(instance, dict):
        children = instance.values()
    elif isinstance(instance, list):
        children = instance
    else:
        kind = "null" if instance is None else "boolean" if isinstance(instance, bool) else "string" if isinstance(instance, str) else "number"
        if kind not in allowed:
            yield ValidationError("Forbidden recursive scalar type")
        return
    for value in children:
        yield from recursive_scalars(validator, allowed, value, schema)


SchemaValidator = validators.extend(Draft202012Validator, {
    "x-maximumDecimal": decimal_bound("maximum", lambda a, b: a > b),
    "x-minimumDecimal": decimal_bound("minimum", lambda a, b: a < b),
    "x-recursiveScalars": recursive_scalars,
})


def schema_errors(docs, path, fragment, instance):
    root = docs[path]
    store = {d["$id"]: d for p, d in docs.items() if "/schemas/" in p}
    def no_network(uri):
        raise ValueError(f"Unresolved local schema reference: {uri}")
    resolver = RefResolver.from_schema(root, store=store, handlers={"https": no_network, "http": no_network})
    return list(SchemaValidator({"$ref": root["$id"] + fragment}, resolver=resolver).iter_errors(instance))


def fixture_results(docs, raw):
    results = []
    for case in docs["contracts/proposals/core-v1.candidate.json"]["validations"]:
        digest = hashlib.sha256(raw[case["instancePath"]]).hexdigest()
        if digest != case["instanceSha256"]:
            raise ValueError(f"Fixture hash mismatch: {case['id']}")
        errors = schema_errors(docs, case["schemaPath"], case["schemaFragment"], docs[case["instancePath"]])
        actual = not errors
        results.append({"id": case["id"], "expected_valid": case["expectedValid"],
                        "actual_valid": actual, "passed": actual == case["expectedValid"]})
    return results


def consumer_schema_results(docs):
    """Independent boundary cases, not candidate-provided expected outcomes."""
    cases = []
    def add(name, schema, definition, value, valid):
        cases.append((name, f"contracts/v1/schemas/{schema}.schema.json", f"#/$defs/{definition}", value, valid))
    for value, valid in [("-9223372036854775808", True), ("9223372036854775807", True),
                         ("9007199254740993", True), ("9223372036854775808", False),
                         ("-9223372036854775809", False), (9007199254740993, False),
                         ("-0", False), ("1e3", False)]:
        add(f"signed64-{type(value).__name__}-{value}", "common", "signedInt64", value, valid)
    for value, valid in [(0, True), (253402300799999, True), (-1, False), (253402300800000, False)]:
        add(f"timestamp-{value}", "common", "timestamp", value, valid)
    for name, extra, valid in [("absent-avatar", {}, True), ("null-avatar", {"avatarKey": None}, True),
                               ("null-display-name", {"displayName": None}, False),
                               ("body-actor", {"actor": "forged"}, False)]:
        add(name, "profile", "patchCommand", {"expectedRevision": "0", "displayName": "Synthetic", **extra}, valid)
    return [{"id": name, "expected_valid": valid,
             "actual_valid": not schema_errors(docs, path, fragment, value),
             "passed": (not schema_errors(docs, path, fragment, value)) == valid}
            for name, path, fragment, value, valid in cases]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--backend", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    docs, raw = load_candidate(args.backend, args.commit)
    rows = field_rows(docs)
    results = fixture_results(docs, raw)
    consumer_results = consumer_schema_results(docs)
    report = {"backend_commit": args.commit, "candidate_version": docs["contracts/v1/operations.json"]["contractVersion"],
              "scope": "G1 diagnostic schema validation and proposed field mapping only; not gate approval or runtime wire validation",
              "artifact_sha256": {p: hashlib.sha256(b).hexdigest() for p, b in sorted(raw.items())},
              "field_count": len(rows), "fields": rows,
              "schema_cases": results, "schema_cases_passed": sum(x["passed"] for x in results),
              "client_boundary_cases": consumer_results,
              "client_boundary_cases_passed": sum(x["passed"] for x in consumer_results),
              "limitations": ["Custom recursive/byte/aggregate semantics require separate consumer review",
                              "MessagePack duplicate keys, wire bytes, allocation, network races and codecs unrun",
                              "Mapping rows retain schema pointers: conditional branches must be implemented together",
                              "No DTO, canonical schema or reviewed mirror written"]}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{len(rows)} field rows; {report['schema_cases_passed']}/{len(results)} candidate schema cases; {report['client_boundary_cases_passed']}/{len(consumer_results)} client boundary cases passed")
    return 0 if results and all(x["passed"] for x in results + consumer_results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
