#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Tests/CompilerSmoke.swift -o .build/compiler-smoke
.build/compiler-smoke
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/SidebarTools.swift Tests/ProjectRegression.swift -o .build/project-regression
.build/project-regression

swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Tests/WatchCompilerTests.swift -o .build/watch-tests
.build/watch-tests
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/Editor.swift Sources/TypstStudio/EditorPreviewSplit.swift Tests/EditorResponsiveness.swift -o .build/editor-responsiveness
python3 - <<'PYTEST'
import subprocess
result = subprocess.run(['.build/editor-responsiveness'], timeout=12, capture_output=True, text=True)
print(result.stdout)
if result.returncode:
    print(result.stderr[-2000:])
    raise SystemExit(result.returncode)
PYTEST

swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Sources/TypstStudio/SidebarTools.swift Tests/DocumentSwitch.swift -o .build/document-switch
.build/document-switch

swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/SidebarModePicker.swift Tests/SidebarInteraction.swift -o .build/sidebar-interaction
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Tests/PreviewPositionRegression.swift -o .build/preview-position
python3 - <<'PYINTERACTION'
import subprocess
for test in ['.build/sidebar-interaction', '.build/preview-position']:
    result = subprocess.run([test], timeout=18, capture_output=True, text=True)
    print(result.stdout)
    if result.returncode:
        print(result.stderr[-2500:])
        raise SystemExit(result.returncode)
PYINTERACTION
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Tests/PDFColorTests.swift -o .build/pdf-color-tests
.build/pdf-color-tests

swiftc Sources/TypstStudio/StudioLanguage.swift Tests/LegacyUpdateChecker.swift Tests/UpdateCheckerTests.swift -o .build/update-tests
.build/update-tests
swiftc Sources/TypstStudio/StudioLanguage.swift Tests/LanguageTests.swift -o .build/language-tests
.build/language-tests
swiftc Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/SourceCoordinates.swift Sources/TypstStudio/TinymistConnection.swift Sources/TypstStudio/TinymistCompiler.swift Sources/TypstStudio/Compiler.swift Sources/TypstStudio/WatchCompiler.swift Sources/TypstStudio/ProjectFiles.swift Sources/TypstStudio/StudioModel.swift Sources/TypstStudio/Preview.swift Tests/SourceMappingTests.swift -o .build/navigation-tests
.build/navigation-tests
if [[ ! -d .build/artifacts/sparkle/Sparkle/Sparkle.xcframework ]]; then swift package resolve; fi
SPARKLE_DIR=.build/artifacts/sparkle/Sparkle/Sparkle.xcframework/macos-arm64_x86_64
swiftc -F "$SPARKLE_DIR" -framework Sparkle -Xlinker -rpath -Xlinker "$PWD/$SPARKLE_DIR" Sources/TypstStudio/StudioLanguage.swift Sources/TypstStudio/AutomaticUpdater.swift Tests/AutomaticUpdaterTests.swift -o .build/automatic-update-tests
.build/automatic-update-tests
