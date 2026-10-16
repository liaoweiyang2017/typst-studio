#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build
swiftc Sources/TypstStudio/Compiler.swift Tests/CompilerSmoke.swift -o .build/compiler-smoke
.build/compiler-smoke
swiftc Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/SidebarTools.swift Tests/ProjectRegression.swift -o .build/project-regression
.build/project-regression

swiftc Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Tests/WatchCompilerTests.swift -o .build/watch-tests
.build/watch-tests
swiftc Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/Editor.swift Sources/TypstStudio/EditorPreviewSplit.swift Tests/EditorResponsiveness.swift -o .build/editor-responsiveness
python3 - <<'PYTEST'
import subprocess
result = subprocess.run(['.build/editor-responsiveness'], timeout=12, capture_output=True, text=True)
print(result.stdout)
if result.returncode:
    print(result.stderr[-2000:])
    raise SystemExit(result.returncode)
PYTEST
