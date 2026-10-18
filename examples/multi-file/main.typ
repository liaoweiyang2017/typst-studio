// Compile main.typ to include the chapter and keep one complete PDF.
#set page(paper: "a4", margin: 24mm)
#set text(size: 11pt)
#set heading(numbering: "1.")

= A multi-file document

The next chapter lives in a separate file. Open it to edit, but keep this
file as the compilation entry to preview the complete document.

#include "chapter.typ"
