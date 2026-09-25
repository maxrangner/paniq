# Model scripts

One file per model, named after the thing it draws: the `PhysicsObjectKind`
for a prop (`VendingMachine.py`), `Person.py` for people. Each defines
`build()` and returns a `Model`; `tools\BuildModel.ps1 -Name VendingMachine`
does the rest. How to ask for a model, and the rules every model follows,
are in [docs/model-pipeline.md](../../../docs/model-pipeline.md).

Nothing here yet: the pipeline landed before its first model.
