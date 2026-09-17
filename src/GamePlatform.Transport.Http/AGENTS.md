# HTTP adapter instructions

Follow root and src AGENTS.md. Vendor types/exceptions do not leak inward. Remain unavailable until endpoint, auth and runtime tests pass. Retries preserve operation identity and never infer commit rollback from cancellation. No automatic JSON fallback after MessagePack decode errors.
