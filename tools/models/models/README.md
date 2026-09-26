# Model scripts

One file per model, named after the thing it draws: the `PhysicsObjectKind`
for a prop (`VendingMachine.py`), `Person.py` for people. Each defines
`build()` and returns a `Model`; `tools\BuildModel.ps1 -Name VendingMachine`
does the rest. How to ask for a model, and the rules every model follows,
are in [docs/model-pipeline.md](../../../docs/model-pipeline.md).

A model may exist before its kind does: `WetFloorSign.py` was made on
request before the simulation had a wet-floor sign.
