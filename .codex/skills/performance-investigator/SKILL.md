---
name: performance-investigator
description: Analyze and optimize performance across ASP.NET Core backends, EF Core queries, databases, and React or Next.js frontends. Use when requests are slow, pages are heavy, or bottlenecks are unclear.
---

# Performance Investigator

Optimize with evidence. Measure the slow path, attribute the time across layers, and prioritize the fix with the highest real impact.

## Workflow

1. Define the performance problem in measurable terms:
   - endpoint latency
   - page load time
   - query duration
   - throughput or concurrency limits
2. Break down the time across:
   - network
   - API processing
   - database access
   - serialization
   - frontend fetch waterfall
   - render and hydration cost
3. Identify the dominant bottleneck before editing code.
4. Fix the highest-yield issue first, then re-measure.

## Backend And Data Rules

- Check for N+1 queries, excessive includes, unbounded result sets, redundant serialization, and chatty internal calls.
- Review indexes against actual filter, join, and sort patterns.
- Do not micro-optimize allocations before query shape and I/O are understood.

## Frontend Rules

- Look for redundant fetching, over-hydration, heavy client components, and wasteful rerenders.
- Prefer architecture fixes over cosmetic memoization.
- Distinguish server delay from client render delay.

## Deliverables

Produce:
- bottleneck analysis
- ranked optimization opportunities
- implemented fixes when appropriate
- measurement notes or verification steps
