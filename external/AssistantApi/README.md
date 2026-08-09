# UOSagas.AssistantApi

This folder contains a **synced copy** of the UOSagas client's plugin API
(`UOSagas.AssistantApi`). It defines the versioned ABI between the UOSagas
game client and external assistant plugins such as UOSagas Razor: the
`IAssistantPlugin` entry point, the native function tables, packet viewer /
sender delegates, capability flags, and the data service surface.

The canonical source lives in the (private) UOSagas client repository and is
mirrored here so this repository builds standalone. Do not edit these files
directly in a pull request — API changes must happen client-side first and are
synced over together with a client release.

License: GPL-3.0, same as the rest of this repository.
