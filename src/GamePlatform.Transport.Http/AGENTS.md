# HTTP adapter instructions

Follow root and src AGENTS.md. Vendor types/exceptions do not leak inward. Preserve the bounded qualified providers; new endpoint/auth/runtime surface remains unavailable until its own tests pass. Retries preserve operation identity and never infer commit rollback from cancellation. No automatic JSON fallback after MessagePack decode errors.
