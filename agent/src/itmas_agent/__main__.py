"""Thin entry point — supports `python -m itmas_agent` and is the script
PyInstaller bundles into the installed binary. All real logic lives in
cli.py; this file only exists to have something concrete to invoke/bundle.
"""

from __future__ import annotations

import sys

from itmas_agent.cli import main

if __name__ == "__main__":
    sys.exit(main())
