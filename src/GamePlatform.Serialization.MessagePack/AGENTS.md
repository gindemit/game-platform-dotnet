# MessagePack adapter instructions

Follow root and src AGENTS.md. Bound untrusted decoding. Preserve RFC-order UUID bin16 and exact signed int64 semantics; reject typeless metadata, duplicate keys and ambiguous numeric forms. Remain unavailable until real cross-language and AOT tests pass.
