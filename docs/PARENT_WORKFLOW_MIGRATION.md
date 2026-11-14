# Parent Repository Release Workflow Migration Guide

## Overview

The enclosing repository (`Infinix-GT-Book-Custom-Control-Center`) and the nested application repository (`GTBookEssentials`) are two independent Git repositories.

During the repository audit of 2026-11-13, a development issue was identified regarding the parent repository's release workflow:
- **Location:** `ControlCenter\.github\workflows\release.yml:18`
- **Issue:** The workflow sets `working-directory: Infinix_Toolkit`, but `Infinix_Toolkit` is absent from the current reorganized working tree.

Per Phase 2 implementation rules, the parent repository's working tree has been left untouched to preserve repository boundaries and avoid unreviewed history mutations. This document describes the situation and provides clear, actionable instructions for updating or retiring that workflow.

---

## Current Architecture

1. **`GTBookEssentials` (Application Repository):**
   - Active repository containing the full C# Windows Forms application, tests, installer scripts, and CI workflows.
   - Workflow: `GTBookEssentials\.github\workflows\build.yml`
   - Handles building, headless testing, packaging (Setup.exe + portable ZIP + SHA-256), and GitHub releases with proper SemVer and prerelease (`--prerelease`) tagging.

2. **`ControlCenter` (Enclosing Repository):**
   - Historically hosted the Python-based `Infinix_Toolkit`.
   - The workflow `ControlCenter\.github\workflows\release.yml` attempted to build and package the Python toolkit from `Infinix_Toolkit/`.
   - Because `Infinix_Toolkit` was replaced by the modern C# `GTBookEssentials` application, any run of the parent `release.yml` will fail at step `working-directory: Infinix_Toolkit`.

---

## Recommended Migration Options

When the maintainer is ready to update the enclosing repository, choose one of the following approaches:

### Option A: Retire Parent Release Workflow (Recommended)
If releases are distributed directly from `GTBookEssentials`:
1. Remove or disable `.github/workflows/release.yml` in the enclosing repository.
2. Direct all release tags (`v*`) to the `GTBookEssentials` repository where CI is fully configured and tested.

### Option B: Point Parent Workflow to `GTBookEssentials`
If the parent repository is intended to build and publish the C# application:
1. Update `working-directory` in `ControlCenter\.github\workflows\release.yml` from `Infinix_Toolkit` to `GTBookEssentials`.
2. Replace Python packaging steps with PowerShell calls to `GTBookEssentials\Build-Release.ps1`.
3. Use the updated asset packaging from `GTBookEssentials\out\`.

---

## Verification
- `GTBookEssentials` standalone build verified with `.\Build-Release.ps1` and `.\build.ps1`.
- Production compilation and testing operate completely independently from parent directory paths.
