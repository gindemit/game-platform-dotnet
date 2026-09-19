# GamePlatform.Sync

Owns portable ordering, retry and reconciliation policy over backend/storage ports. Excludes timers tied to Unity Update and codec/storage implementations. The bounded CL-009 sender leases only the next contiguous durable command, preserves immutable identity under retry/uncertainty, and persists terminal acceptance or rejection before advancing. Pull/bootstrap remains unavailable and the M0 coordinator still returns typed NotImplemented. Gates: A03–A05, A07.
