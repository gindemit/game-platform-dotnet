# GamePlatform.Sync

Owns portable ordering, retry and reconciliation policy over backend/storage ports. Excludes timers tied to Unity Update and codec/storage implementations. The bounded CL-009 sender leases only the next contiguous durable command, preserves immutable identity under retry/uncertainty, and persists terminal acceptance or rejection before advancing.

The bounded CL-010 coordinator consumes an injected private-sync remote, resumes durable bootstrap pages, installs only a complete final-page snapshot, pulls one fixed-watermark page at a time, and routes reset responses back through snapshot replacement. It does not claim a live HTTP provider or backend success. The M0 aggregate coordinator remains unavailable. Gates: A03–A05, A07.
