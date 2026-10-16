#!/usr/bin/env python3
"""Run an explicit PowerShell command in the local development VM.
EncodedCommand preserves Unicode and quotes through prlctl; execution policy is not changed.
"""
import base64
import subprocess
import sys
script = "$ProgressPreference = 'SilentlyContinue';\n" + '[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new();\n' + sys.stdin.read()
command = ['prlctl', 'exec', 'Windows on arm']
if '--user' in sys.argv:
    command += ['--current-user']
command += ['powershell.exe', '-NoProfile', '-EncodedCommand', base64.b64encode(script.encode('utf-16-le')).decode('ascii')]
raise SystemExit(subprocess.run(command).returncode)
