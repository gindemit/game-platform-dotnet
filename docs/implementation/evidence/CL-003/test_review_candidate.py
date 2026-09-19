"""Regression checks for fail-closed G1 review tooling; no SDK runtime claims."""
import unittest
import json
from unittest.mock import patch

from review_candidate import SchemaValidator, field_rows, load_candidate, type_mapping


class ReviewTests(unittest.TestCase):
    def test_requires_immutable_commit(self):
        with self.assertRaises(ValueError):
            load_candidate("unused", "origin/main")

    def test_int64_overflow_is_rejected_even_when_regex_matches(self):
        schema = {"type": "string", "pattern": "^-?[0-9]+$", "x-maximumDecimal": "9223372036854775807", "x-minimumDecimal": "-9223372036854775808"}
        validator = SchemaValidator(schema)
        for value in ("9223372036854775808", "-9223372036854775809"):
            self.assertFalse(validator.is_valid(value))
        for value in ("9223372036854775807", "-9223372036854775808", "9007199254740993"):
            self.assertTrue(validator.is_valid(value))

    def test_numeric_extension_rejected_recursively(self):
        validator = SchemaValidator({"x-recursiveScalars": ["null", "boolean", "string"]})
        self.assertFalse(validator.is_valid({"nested": [{"value": 1}]}))
        self.assertTrue(validator.is_valid({"nested": [None, True, "1"]}))

    def test_uuid_does_not_map_to_semantic_string(self):
        self.assertEqual("RFC-order bin16", type_mapping({}, "", {"$ref": "common.schema.json#/$defs/uuid"})[2])
        self.assertEqual("UTF-8 string", type_mapping({}, "", {"type": "string"})[2])

    def test_full_width_values_do_not_map_to_ts_number(self):
        for name in ("signedInt64", "positiveInt64", "nonNegativeInt64"):
            self.assertEqual("bigint", type_mapping({}, "", {"$ref": "common.schema.json#/$defs/" + name})[1])

    def test_conditional_and_nested_properties_are_inventoried(self):
        doc = {"$defs": {"x": {"properties": {"outer": {"type": "object", "properties": {"child": {"type": "boolean"}}}}, "allOf": [{"then": {"properties": {"extra": {"type": "integer"}, "forbidden": False}}}]}}}
        rows = field_rows({"contracts/v1/schemas/test.schema.json": doc})
        self.assertEqual(["outer", "child", "extra"], [x["field"] for x in rows])
        self.assertEqual(3, len({x["pointer"] for x in rows}))

    def test_absence_not_collapsed_into_null(self):
        rows = field_rows({"contracts/v1/schemas/test.schema.json": {"properties": {"avatar": {"oneOf": [{"type": "string"}, {"type": "null"}]} }}})
        self.assertIn("explicit null", rows[0]["csharp"])
        self.assertIn("preserve absence independently", rows[0]["default"])

    def test_reference_resolves_target_properties(self):
        docs = {"contracts/v1/schemas/test.schema.json": {"$defs": {"name": {"type": "string", "maxLength": 5}}, "properties": {"name": {"$ref": "#/$defs/name"}}}}
        row = field_rows(docs)[0]
        self.assertEqual({"maxLength": 5}, row["bounds"])

    def test_candidate_digest_drift_fails(self):
        paths = b"contracts/proposals/core-v1.candidate.json\ncontracts/v1/schemas/test.schema.json\n"
        manifest = json.dumps({"files": [{"path": "contracts/v1/schemas/test.schema.json", "sha256": "0" * 64}]}).encode()
        with patch("review_candidate.git_bytes", side_effect=[paths, manifest, b"{}"]):
            with self.assertRaisesRegex(ValueError, "digest mismatch"):
                load_candidate("unused", "a" * 40)

    def test_unhashed_schema_fails(self):
        paths = b"contracts/proposals/core-v1.candidate.json\ncontracts/v1/schemas/test.schema.json\n"
        with patch("review_candidate.git_bytes", side_effect=[paths, b'{"files": []}', b"{}"]):
            with self.assertRaisesRegex(ValueError, "Unhashed"):
                load_candidate("unused", "a" * 40)


if __name__ == "__main__":
    unittest.main()
