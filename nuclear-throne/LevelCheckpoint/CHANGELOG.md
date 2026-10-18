# Level Checkpoint — Changelog

## v1.3.2 — Production (2026-10-18)

- Promoted **v1.3.1 Beta** after user confirmation that its Retry character restoration works correctly.
- **No gameplay-code changes**: mod source differs from v1.3.1 only by the version/comment header.
- Preserved Retry-only resume, first autosave at 1-2, new-run checkpoint replacement, and two-file schema-1 saves.
- Preserved a clean production interface: concise checkpoint notices and actionable errors, without development diagnostics.
- Updated release metadata and static tests for version 1.3.2.

## v1.3.1 Beta — Retry character initialization

- After the portal transition, initialize the actual Player actor as the saved race before restoring saved HP, equipment, mutations and other state.
- Apply saved skin before mutation and equipment restoration.
- Validate the final actor race before reporting resume success.

## v1.3.0 — Retry-only cleanup

- Removed experimental Random-menu resume and synthetic Retry paths; Retry is the only automatic resume trigger.
- Removed developer status commands and verbose diagnostics, leaving concise user-facing notices and errors.
- Retained first autosave at 1-2 and rotating checkpoint backup files.
