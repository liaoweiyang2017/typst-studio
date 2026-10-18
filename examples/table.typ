// A small table and reusable styling, without any package imports.
#set page(paper: "a4", margin: 24mm)
#set text(size: 11pt)
#let accent = rgb("197c91")

= A simple experiment

#figure(
  table(
    columns: (1fr, 1fr, 1fr),
    align: (left, right, right),
    inset: 8pt,
    stroke: 0.5pt + rgb("d5e3e6"),
    fill: (x, y) => if y == 0 { rgb("eaf4f5") },
    [*Sample*], [*Time (s)*], [*Value*],
    [A], [0.5], [12.4],
    [B], [1.0], [15.8],
    [C], [1.5], [19.1],
  ),
  caption: [Measurements from three samples.],
) <measurements>

The values in @measurements increase with time.

#block(fill: rgb("eaf4f5"), inset: 12pt, radius: 4pt)[
  #text(fill: accent, weight: "bold")[Note]
  Replace the values above with your own measurements.
]
