# AI usage

This document describes how AI tooling is used in this project.
The project voluntarily follows the [Dalamud plugin repository's AI policy](https://dalamud.dev/plugin-publishing/ai-policy/).

## Declared level

**Copilot** — AI implements while the human plans, reviews, tests, and owns the result. The AI does most of the writing.

Dalamud's six levels, from least to most AI involvement: None · Hint · Assist · Pair · **Copilot** · Auto.

## How the work actually happens

The tool is Claude Code. The workflow in short: I write the spec, describe what addons to read and what behavior I want, the AI writes the implementation, I review every diff, and I test in a live event instance.

What stays with me:

- Concept and feature design — I wanted hands-free Keybound Brawler runs for easy MGP
- Reverse engineering — `KTGEWord` slot layout, `KTGInput` buffer node path, `KTGEnemyCastBar` hierarchy for Obstruction detection
- Priority and safety design — defense-first targeting, obstruction halt, escape throttling, stun detection
- All in-game testing across multiple waves and edge cases

What AI wrote:

- C# implementation from my specs — typing simulator, state reader, automation loop, addon interop
- FF7 Remake-themed UI and custom ImGui components
- Memory cache for obscured words and Lumina word database loader
- AFK loop state machine (NPC → register → commence → play → results → repeat)

## What this does not change

Code is held to the same bar as any hand-written change. It has to work in a live event instance, handle edge cases, and not interfere with the game client. The AI gets Dalamud and FFXIVClientStructs APIs wrong often enough that every generated file is reviewed.

