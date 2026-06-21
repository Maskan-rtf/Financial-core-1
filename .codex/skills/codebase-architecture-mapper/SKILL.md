---
name: codebase-architecture-mapper
description: Build a practical architecture map of an ASP.NET Core, EF Core, React, or Next.js codebase. Use when onboarding, preparing a large change, or needing fast understanding of modules, flows, conventions, and hotspots.
---

# Codebase Architecture Mapper

Create a working mental model of the system quickly enough to support real engineering decisions. Focus on entry points, ownership boundaries, request flow, and change risk.

## Workflow

1. Discover the top-level shape:
   - projects and apps
   - startup and composition roots
   - frontend routes and feature folders
   - shared libraries
2. Trace a few representative flows end to end:
   - request to controller or handler
   - service or domain logic
   - persistence path
   - response shaping
   - frontend consumption and state handling
3. Identify conventions and deviations:
   - folder structure
   - mapping style
   - validation boundaries
   - auth patterns
   - testing approach
4. Summarize hotspots:
   - modules with high coupling
   - duplicated logic
   - risky legacy seams
   - low-test areas

## Rules

- Prefer concrete path tracing over vague architectural labels.
- Cite the code locations that establish a conclusion.
- Distinguish current structure from intended architecture if they diverge.

## Deliverables

Produce:
- concise architecture map
- key flows and boundaries
- important conventions
- risk and opportunity notes for the requested area
