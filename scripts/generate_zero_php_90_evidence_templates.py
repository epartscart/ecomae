#!/usr/bin/env python3
"""Generate the same conservative evidence summary for the 90% gate.

The project is not at 90%; this wrapper exists so automation can fail closed
against the same evidence template source used by the 100% gate.
"""
from generate_zero_php_100_evidence_templates import main

if __name__ == "__main__":
    raise SystemExit(main())
