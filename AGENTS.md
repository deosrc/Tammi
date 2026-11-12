# Tammi

Task-first software development workspace manager, built with .NET MAUI Blazor Hybrid (Windows) and Photino.Blazor (Linux) sharing one Blazor UI.

Read `docs/CONTEXT.md` before starting work. It defines the product, the glossary and the open questions.
Use its glossary terms in code and UI. When a decision resolves an open question, update that document.

## Code style

Put each top-level type in its own file, named after the type (e.g. `FolderPickerOptions.cs`). Nested types can stay
in their containing type's file.

## Token scopes and permissions

Wherever the UI names the scopes or permissions an integration's token needs (e.g. the token help in Settings →
Integrations), show them as a bulleted list in alphabetical order, one to a line, with scope names in `<code>`. Keep the
lists in messages and docs in the same order. Don't run them together in a sentence. Separate lists for fine-grained
and classic tokens, or for optional extras, are fine, each in alphabetical order.

## Third-party software

Every third-party library that ships with the app must be listed on the About page's third-party software list.
When you add one (including a new transitive dependency that ships, or a dependency of only one host), add an entry
to `src/Deosrc.Tammi.UI/About/ThirdPartySoftware.cs` in alphabetical order, and add its license text to
`src/Deosrc.Tammi.UI/Licenses/`, copied from the package or its source repository. If the license is only published online,
link to it instead. When you remove a library, remove its entry and any license text no other entry uses.
