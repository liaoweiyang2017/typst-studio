#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build
swiftc Sources/TypstStudio/Compiler.swift Tests/CompilerSmoke.swift -o .build/compiler-smoke
.build/compiler-smoke
swiftc Sources/TypstStudio/Compiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/SidebarTools.swift Tests/ProjectRegression.swift -o .build/project-regression
.build/project-regression
