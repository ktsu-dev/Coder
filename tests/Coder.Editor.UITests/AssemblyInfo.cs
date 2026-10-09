// Copyright (c) 2023-2026 ktsu-dev contributors

// ImGui keeps its context in global state, so only one harness can exist per process and two
// tests running at once would corrupt each other. Sequential execution is a correctness
// requirement here, not a performance preference.
[assembly: DoNotParallelize]
