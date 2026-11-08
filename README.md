# O/T

A minimal framework for LLM agents to interact with a simulated world.

## Concept

World = fields + processing.

- **Obj** — a bag of Fields, identified by Id, Router, Routes
- **Field** — typed value with deferred commit (`Set` → `Cmt`)
- **Trait** — a processing unit (singleton, no state)
- **Router** — entry point for an Obj (Evt / Get / Cmt / Del)
- **Evt** — fire-and-forget propagation between objects
- **Get** — synchronous aggregation into a `GetHandle`

## Layout

- `Ot/` — the core, distributed as a Unity package (UPM)

## Install (Unity)

In `Packages/manifest.json`:

    "com.ok23902.ot": "https://github.com/ok23902/ot.git?path=Ot#v0.1.4"

## License

MIT