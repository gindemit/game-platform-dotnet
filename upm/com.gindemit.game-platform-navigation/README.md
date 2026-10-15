# Optional navigation kernel

Generated from canonical SDK src at `bfca8279c6af813e072a8fbdad76d703737b3274`.

Install this Git subdirectory at a full commit SHA and explicitly reference
`GamePlatform.Navigation.Kernel` from a consuming asmdef. It includes no Unity,
account, HTTP, SQLite, shell artwork or native dependencies.

The existing mobile SDK pin may coexist only while it does not ship
`GamePlatform.Navigation.Kernel.dll`. A later full SDK update must select
one delivery of that assembly; the Unity import validator rejects duplicates.
This source package does not update the mobile SDK or activate any feature.
