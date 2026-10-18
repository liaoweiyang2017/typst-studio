# Examples

These examples do not use external Typst packages or require a specific system font.

| Entry | What it demonstrates |
| --- | --- |
| [hello.typ](hello.typ) | Headings, emphasis, lists and a quotation |
| [math.typ](math.typ) | Inline/display equations, integrals, matrices and cases |
| [table.typ](table.typ) | Tables, captions, cross-references and a styled note |
| [multi-file/main.typ](multi-file/main.typ) | Included chapters and cross-file source mapping |
| [showcase/main.typ](showcase/main.typ) | The richer document shown in the project screenshots |

Open a file in Typst Studio and choose **Compile**. A same-name PDF is written beside that entry. For a multi-file project, open its folder and compile `main.typ` first; browsing to an included file does not change the preview entry.

From the repository root, for example:

```sh
typst compile examples/hello.typ
typst compile examples/math.typ
typst compile examples/table.typ
typst compile examples/multi-file/main.typ
```

The examples are covered by the project's MIT license.
