# GamePlatform.Backend.Contracts

Owns semantic remote ports and results, depending only on Core. Excludes wire
DTOs, HTTP/cloud SDK types and endpoint details. CL-005 adds a typed provisioning
snapshot and explicit transport failure outcomes; unsupported remote capabilities
remain unavailable. Gates: A02, A04–A07, A12.
