# GamePlatform.Transport.Abstractions

Owns bounded neutral HTTP request/response, delivery-certainty and codec ports.
Models defensively copy bodies and headers and contain no Unity or serializer
types. Platform executors remain injected. Gates: A02, A11, A12.
