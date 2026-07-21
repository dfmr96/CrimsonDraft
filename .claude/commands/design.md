# Design Pipeline Orchestrator

You are orchestrating the complete design-to-implementation pipeline for **Crimson Draft**. This pipeline has 4 phases with a user approval gate between each one. NEVER skip a phase or proceed without explicit user approval.

## Pipeline

```
Phase 1: BRAINSTORM → Phase 2: GDD DOC → Phase 3: IMPL PLAN → Phase 4: EXECUTE
  (ideas/clarity)     (design doc)       (technical plan)      (code)
```

## Task: $ARGUMENTS

### Phase 1: Brainstorm

Invoke the `superpowers:brainstorming` skill with the user's request. This explores the idea, asks clarifying questions, and produces a design document at `docs/plans/YYYY-MM-DD-<topic>-design.md`.

**Gate:** Wait for user approval of the brainstorm output before proceeding.

### Phase 2: GDD Document

Using the approved brainstorm output as input, write a **game design document** (or section of an existing one) using the `/obsidian-docs` skill conventions. This document:

- Lives in the Obsidian vault root as a `.md` file (and synced to `Assets/_Design/`)
- Describes **HOW the system works** from a pure design perspective — no code, no implementation details
- Eliminates all ambiguity: every variable, every state, every edge case is defined
- Uses tables for data, pseudocode for logic flows, and blockquotes for design intent
- Follows all conventions from the `obsidian-docs` skill (frontmatter, tildes, Diseno/Intencion sections, wikilinks, footer)
- Links to/from `[[Crimson Draft]]` index and related system docs
- If the feature belongs as a section of an existing doc rather than a new doc, update the existing doc instead

The GDD doc is the **source of truth**. The implementation plan in Phase 3 must implement exactly what this doc specifies — nothing more, nothing less.

Save the design doc to both vault locations. Update `Crimson Draft.md` index if it's a new doc. Commit with `docs(gdd): <description>`.

**Gate:** Wait for user approval of the GDD doc before proceeding.

### Phase 3: Implementation Plan

Invoke the `superpowers:writing-plans` skill. The plan must:

- Reference the GDD doc as its spec: "Implements [[Doc Name]]"
- Include a link to the GDD doc at the top of the plan
- Implement exactly what the GDD doc specifies — no creative interpretation, no extra features
- Follow the writing-plans conventions (exact files, exact code, TDD steps, commands with expected output)

Save to `docs/plans/YYYY-MM-DD-<topic>-impl.md`. Commit with `docs(plan): <description>`.

**Gate:** Wait for user approval of the implementation plan before proceeding.

### Phase 4: Execute

Invoke the `superpowers:executing-plans` skill to implement the plan task by task with review checkpoints.

## Rules

1. **One phase at a time.** Complete each phase fully, present the output, and wait for explicit approval before moving to the next.
2. **Read existing docs first.** Before Phase 2, read all related GDD docs to maintain consistency with established systems and terminology.
3. **No code in Phase 2.** The GDD doc is pure design. Variables and states can use pseudocode notation but no C#, no Unity APIs, no implementation specifics.
4. **No design decisions in Phase 3.** The implementation plan translates the GDD doc to code. If something is ambiguous in the GDD doc, go back and fix the doc first — don't resolve ambiguity in the plan.
5. **Traceability.** Each phase's output must reference the previous phase's artifact.
