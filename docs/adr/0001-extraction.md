# ADR 0001: bootstrap before extraction

Status: accepted for M0, 2026-09-17.

Create an independent game-neutral SDK scaffold while retaining `MrSquare.Platform` as the sole live authority. Do not modify Unity or install this SDK. Record compatibility mappings first; extract in a later coordinated consumer/backend change. This avoids parallel incompatible ID/result/provider hierarchies and keeps M0 evidence honest.
