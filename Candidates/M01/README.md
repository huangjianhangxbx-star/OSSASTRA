# M01 core and Unity candidate
Canonical domain C# now lives at Assets/OSSASTRA/CharacterCore (one source of truth, no UnityEngine references). This netstandard2.1/C#8 project compiles those same files through Compile links. Tests use .NET9 and no external test packages.
Run: dotnet run --project Candidates/M01/Tests/Ossastra.CharacterCore.Tests.csproj -c Release
Unity scene: Assets/OSSASTRA/M01/SC_M01_CharacterTest.unity. See docs/ossastra/M01-scene-review.md for real wiring and limits. User architecture review remains pending. Candidate publication was explicitly authorized on 2026-10-19; see docs/ossastra/M01-HANDOFF.md.
