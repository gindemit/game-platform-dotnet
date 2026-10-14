# GamePlatform.Backend.Contracts

Owns semantic remote ports and results, depending only on Core. Excludes wire
DTOs, HTTP/cloud SDK types and endpoint details. CL-005 adds a typed provisioning
snapshot and explicit transport failure outcomes. CL-009 adds a narrow retained
terminal-receipt lookup result without exposing wire or HTTP types; a missing
receipt remains an observation, not evidence that a command did not commit.
Other unsupported remote capabilities
remain unavailable. Gates: A02, A04–A07, A12.
