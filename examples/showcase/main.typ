// Typst Studio · Native editing, live typesetting
#set page(paper: "a4", margin: 22mm)
#set text(font: "New Computer Modern", size: 11pt)
#set heading(numbering: "1.")
#set par(leading: 0.7em)
#let accent = rgb("197c91")
#show heading: set text(fill: accent)

#text(size: 10pt, fill: accent)[TYPST STUDIO / FIELD NOTES]
#v(12mm)
#text(size: 32pt, weight: "bold")[Ideas, beautifully set.]
#v(4mm)
A native workspace for writing, mathematics, and discovery.

= Write with clarity
Keep your source and PDF side by side. Explore the outline,
insert a symbol, and follow an equation back to its source.

= Let the mathematics speak
The Gaussian integral brings a familiar constant into view:

$ integral_(-infinity)^infinity e^(-x^2) dif x = sqrt(pi) $

A short series can tell a much longer story:

$ e^x = sum_(n=0)^infinity x^n / n! $

= A simple workflow
#table(
  columns: (1fr, 2fr), inset: 8pt,
  stroke: 0.5pt + rgb("d8e5e8"),
  fill: (x, y) => if y == 0 { rgb("eaf4f5") },
  [*Step*], [*In the studio*],
  [Write], [Native editing and syntax highlighting],
  [Compile], [A PDF saved beside your source],
  [Explore], [Precise source-to-preview navigation],
)

#v(8mm)
#text(size: 9pt, fill: gray)[
  macOS + Windows · One document, two native experiences.
]

#include "notes.typ"
