## v3.20.0 (minor)

Changes since v3.19.0:

- Keep the StaticType overloads adjacent (Sonar S4136) (Claude)
- Type a call by its declared callee so Python and JavaScript truncate twice(a) / 4 [patch] (Claude)
- Follow a Go local with `_ = name` so an unread one compiles [patch] (Claude)
- Cast a C# shift count to int so a long count compiles [patch] (Claude)
- Keep CSharpMemberModifierTests.cs without a byte order mark (Claude)
- Rename member references in C, Go, Rust and JavaScript enum values [patch] (Claude)
- Write C# operators public static, noting a dropped visibility or virtual [patch] (Claude)
- Write a Go method's type parameters down instead of on the method [patch] (Claude)
- Write nothing from Export when the generator refused the document [patch] (Claude)
- Write C and C++ minimum integers as a subtraction [patch] (Claude)
- Write the missing side of a C# comparison operator pair [patch] (Claude)
- Truncate Python's floating remainder with math.fmod [patch] (Claude)
- Skip empty constraints when parsing a type parameter [patch] (Claude)
- Give C# operators the operand the AST leaves implicit [patch] (Claude)
- Write down a Rust field's starting value when no constructor sets it [patch] (Claude)
- Make string literals owned wherever Rust receives a String [patch] (Claude)
- Lift C# member aliases out of their type and nest JS classes as statics [patch] (Claude)
- Merge remote-tracking branch 'origin/main' into fix/127-csharp-member-modifiers (Claude)
- Merge remote-tracking branch 'origin/main' into fix/167-c-static-fields-in-type (Claude)
- Move a C# constructor's initialisers out of GenerateFunction (Claude)
- Move a C# member's notes out of GenerateFunction (Claude)
- Hold a C# namespace's fields and functions in a static class [patch] (Claude)
- Write C# member modifiers only where C# allows them [patch] (Claude)
- Declare a C type's static fields beside the struct, not inside it [patch] (Claude)
- Keep float and long widths in Go, C and C++ inferred locals [patch] (Claude)
- Project member names with Select in NamesOf (matt-edmondson)
- Write readonly only on a C# struct, and make its instance members readonly [patch] (matt-edmondson)
- Name a C++ getter get_count() when count() would clash with its field [patch] (matt-edmondson)
- Keep the StaticType overloads adjacent and use Assert.Contains (Claude)
- Promote mixed int and float operands to the wider type [patch] (Claude)
- Apply declared field values in Rust and C constructors [patch] (Claude)
- Add ILanguageGeneratorResolver to look up a generator by id or file extension [minor] (Claude)
- Merge main into fix/174-floating-modulo (Claude)
- Let a Python subclass without a constructor take its base's arguments [patch] (Claude)
- Bind a Rust parameter or self mut when the body calls a mutating method on it [patch] (Claude)
- Truncate integer division in Python and JavaScript accessor and entry point bodies [patch] (Claude)
- Truncate integer /= and %= in Python and JavaScript the way the C family does [patch] (matt-edmondson)
- Write a JavaScript long literal past the safe-integer range as a BigInt [patch] (matt-edmondson)
- Merge main into fix/174-floating-modulo (matt-edmondson)
- Annotate an inferred Rust local whose initialiser is a long or a float [patch] (Claude)
- Bind args in a C or C++ main that accepts arguments [patch] (Claude)
- Take a floating-point remainder with fmod or math.Mod in C, C++ and Go [patch] (Claude)
- Write static properties that compile in C++ and are reachable from the class in Python [patch] (Claude)
- Value-initialise a constructor initialiser that has no value [patch] (Claude)
- Parenthesise a struct-literal call receiver in a Go if condition [patch] (Claude)
- Build the splice test's paths with Path.Join (Claude)
- Truncate Python integer division in integer arithmetic so long quotients stay exact [patch] (Claude)
- Spell NaN and infinite float literals in every target language [patch] (Claude)
- End a C/C++ comment line that would splice on a non-splice character [patch] (Claude)
- Make the editor gallery readable and the sample document honest (Claude)

## v3.19.2 (patch)

Changes since v3.19.1:

- Write static properties that compile in C++ and are reachable from the class in Python [patch] (Claude)
- Build the splice test's paths with Path.Join (Claude)
- Truncate Python integer division in integer arithmetic so long quotients stay exact [patch] (Claude)
- Spell NaN and infinite float literals in every target language [patch] (Claude)
- End a C/C++ comment line that would splice on a non-splice character [patch] (Claude)

## v3.19.1 (patch)

Changes since v3.19.0:

- Make the editor gallery readable and the sample document honest (Claude)

## v3.19.0 (minor)

Changes since v3.18.0:

- Photograph the editor into a gallery on every build of main (Claude)
- fix: report an empty document as empty rather than unparseable [patch] (matt-edmondson)
- fix: clone a variable reference that has no name yet [patch] (matt-edmondson)
- fix: register each generator once when AddCoder is called twice [patch] (matt-edmondson)
- fix: give a C struct with no data a placeholder member [patch] (Claude)
- fix: write a Go constant holding NaN or an infinity as a var [patch] (Claude)
- fix: generate an empty enum that compiles in C and Rust [patch] (Claude)
- fix: double the braces in a Rust compile-time assertion message [patch] (Claude)
- test: use the MSTest sequence and emptiness assertions in the undo tests (Claude)
- fix: keep list siblings when undoing a move, and keep the node a drop displaces [patch] (Claude)
- fix: truncate integer division and remainder in Python and JavaScript [patch] (Claude)
- Filter the constructor's default initialisers with Where (Claude)
- Keep Go constant-field values and apply instance-field initialisers (Claude)
- fix: leave out a braced-list member initialiser that has no value [patch] (Claude)
- fix: write a C# const local only for a compile-time value [patch] (Claude)
- fix: build a write-only Python property from its setter alone [patch] (Claude)
- fix: parenthesise a struct literal operand in a Go condition [patch] (Claude)
- fix: escape trigraphs in C and C++ string literals [patch] (Claude)
- Merge remote-tracking branch 'origin/main' into fix/104-javascript-static-fields (matt-edmondson)
- [patch] Defer Python annotations so a class can name itself or a later class (Claude)
- [patch] Move JavaScript initialiser assignments out of GenerateMethod (matt-edmondson)
- [patch] Honour IsStatic and IsConstant on JavaScript fields (matt-edmondson)
- [patch] Call super() first in a JavaScript derived class's constructor (matt-edmondson)
- [patch] Bind a Rust parameter or operator self mut when the body assigns its field (Claude)
- [patch] Spell NaN and infinity in each target language, with the import it needs (Claude)
- [patch] Escape control characters and Unicode line terminators in string literals (Claude)
- [patch] Write a C++ constant of an allocating type as const, not constexpr (Claude)
- [patch] Parenthesise a number literal used as a call receiver (Claude)
- Spell bitwise not as ! and drop unary plus in Rust output [patch] (Claude)
- Move CI onto the shared ci-shared.yml pipeline (Claude)
- Merge remote-tracking branch 'origin/main' into fix/84-rust-owned-string-literals (Claude)
- [patch] Make a string literal an owned String where Rust receives one (Claude)
- [patch] Keep declaration metadata and keyed children through a YAML round trip (Claude)
- [patch] Leave the program path out of Go and Rust entry-point arguments (Claude)
- [patch] Keep string, float and long literals through a YAML round trip (Claude)
- Merge remote-tracking branch 'origin/main' into fix/rust-associated-constants (Claude)
- Read .inf, -.inf and .nan back as double literals [patch] (Claude)
- Write a Rust type's constant fields as associated constants (Claude)

## v3.18.16 (patch)

Changes since v3.18.15:

- fix: report an empty document as empty rather than unparseable [patch] (matt-edmondson)
- fix: clone a variable reference that has no name yet [patch] (matt-edmondson)
- fix: register each generator once when AddCoder is called twice [patch] (matt-edmondson)
- fix: give a C struct with no data a placeholder member [patch] (Claude)
- fix: write a Go constant holding NaN or an infinity as a var [patch] (Claude)
- fix: generate an empty enum that compiles in C and Rust [patch] (Claude)

## v3.18.15 (patch)

Changes since v3.18.14:

- fix: double the braces in a Rust compile-time assertion message [patch] (Claude)

## v3.18.14 (patch)

Changes since v3.18.13:

- test: use the MSTest sequence and emptiness assertions in the undo tests (Claude)
- fix: keep list siblings when undoing a move, and keep the node a drop displaces [patch] (Claude)
- fix: truncate integer division and remainder in Python and JavaScript [patch] (Claude)
- Filter the constructor's default initialisers with Where (Claude)
- Keep Go constant-field values and apply instance-field initialisers (Claude)

## v3.18.13 (patch)

Changes since v3.18.12:

- fix: leave out a braced-list member initialiser that has no value [patch] (Claude)
- fix: write a C# const local only for a compile-time value [patch] (Claude)
- fix: build a write-only Python property from its setter alone [patch] (Claude)

## v3.18.12 (patch)

Changes since v3.18.11:

- fix: parenthesise a struct literal operand in a Go condition [patch] (Claude)
- fix: escape trigraphs in C and C++ string literals [patch] (Claude)

## v3.18.11 (patch)

Changes since v3.18.10:

- Merge remote-tracking branch 'origin/main' into fix/104-javascript-static-fields (matt-edmondson)
- [patch] Honour IsStatic and IsConstant on JavaScript fields (matt-edmondson)

## v3.18.10 (patch)

Changes since v3.18.9:

- [patch] Defer Python annotations so a class can name itself or a later class (Claude)
- [patch] Move JavaScript initialiser assignments out of GenerateMethod (matt-edmondson)
- [patch] Call super() first in a JavaScript derived class's constructor (matt-edmondson)

## v3.18.9 (patch)

Changes since v3.18.8:

- [patch] Bind a Rust parameter or operator self mut when the body assigns its field (Claude)
- [patch] Spell NaN and infinity in each target language, with the import it needs (Claude)

## v3.18.9-pre.1 (prerelease)

Changes since v3.18.8:

- Add cluster regression and toolchain tests ([@Copilot](https://github.com/Copilot))
- Add Python construction and Go type inference ([@Copilot](https://github.com/Copilot))
- Continue generator bug cluster ([@Copilot](https://github.com/Copilot))
- Harden YAML round trips and load errors ([@Copilot](https://github.com/Copilot))
- Initial plan ([@Copilot](https://github.com/Copilot))

## v3.18.8 (patch)

Changes since v3.18.7:

- [patch] Escape control characters and Unicode line terminators in string literals (Claude)
- [patch] Parenthesise a number literal used as a call receiver (Claude)

## v3.18.7 (patch)

Changes since v3.18.6:

- [patch] Write a C++ constant of an allocating type as const, not constexpr (Claude)

## v3.18.6 (patch)

Changes since v3.18.5:

- Spell bitwise not as ! and drop unary plus in Rust output [patch] (Claude)

## v3.18.5 (patch)

Changes since v3.18.4:

- Move CI onto the shared ci-shared.yml pipeline (Claude)

## v3.18.4 (patch)

Changes since v3.18.3:

- Merge remote-tracking branch 'origin/main' into fix/84-rust-owned-string-literals (Claude)
- [patch] Make a string literal an owned String where Rust receives one (Claude)

## v3.18.3 (patch)

Changes since v3.18.2:

- [patch] Keep declaration metadata and keyed children through a YAML round trip (Claude)
- [patch] Leave the program path out of Go and Rust entry-point arguments (Claude)

## v3.18.2 (patch)

Changes since v3.18.1:

- [patch] Keep string, float and long literals through a YAML round trip (Claude)

## v3.18.1 (patch)

Changes since v3.18.0:

- Merge remote-tracking branch 'origin/main' into fix/rust-associated-constants (Claude)
- Read .inf, -.inf and .nan back as double literals [patch] (Claude)
- Write a Rust type's constant fields as associated constants (Claude)

## v3.18.0 (minor)

Changes since v3.17.0:

- Declare a Rust parameter mut when the body assigns to it (Claude)
- Parenthesise a negative literal under a unary minus (Claude)
- Write whole-number doubles with a fraction so they stay floating-point [patch] (Claude)
- Number Python and JavaScript enum members on from the previous value [patch] (Claude)
- Spell a mutable static C++ field inline static inside a type [patch] (Claude)

## v3.17.1 (patch)

Changes since v3.17.0:

- Write whole-number doubles with a fraction so they stay floating-point [patch] (Claude)
- Number Python and JavaScript enum members on from the previous value [patch] (Claude)
- Spell a mutable static C++ field inline static inside a type [patch] (Claude)

## v3.17.0 (minor)

Changes since v3.16.0:

- Ask the three-way menu choice as part of the click's own condition [patch] (Claude)
- Merge main, settling the right-click between the pin and node menus (Claude)
- style(test): assert the absence with DoesNotContain [patch] (Claude)
- style(test): take the assertion Sonar names for the new test [patch] (Claude)
- Offer a pin's own menu on right-click (closes #35) [minor] (Claude)

## v3.16.0 (minor)

Changes since v3.15.0:

- style(test): take the assertion Sonar names for the new test [patch] (Claude)
- Offer a node's own menu on right-click, to add a pin (closes #36) [minor] (Claude)

## v3.15.0 (minor)

Changes since v3.14.0:

- Split the link-drop menu's draw, and modernise the new assertions (Claude)
- Make the link-drop menu reachable in a test, and cover it (Claude)
- Offer to create a node when a link is dropped on empty canvas (Claude)
- Gate Dependabot auto-merge on CI actually being green (Claude)
- style: use Assert.Contains over StringAssert.Contains [patch] (Claude)
- fix: parse YAML numeric literals invariantly [patch] (Claude)
- Write a Go type down over the whole declaration, not its first line (closes #63) [patch] (Claude)
- style(test): use Assert.Contains over StringAssert.Contains on the changed lines (Claude)
- fix: stop JavaScript renaming a private member its references still name [patch] (Claude)
- Write a positional construction as a Go composite literal (closes #68) [patch] (Claude)
- Take the assertion Sonar names for the tests this PR adds [patch] (Claude)
- Quote a Python base that names the class being declared (closes #64) [patch] (Claude)
- ci: adopt the consolidated .NET workflow [patch] (Claude)

## v3.14.7 (patch)

Changes since v3.14.6:

- Gate Dependabot auto-merge on CI actually being green (Claude)

## v3.14.6 (patch)

Changes since v3.14.5:

- style: use Assert.Contains over StringAssert.Contains [patch] (Claude)
- fix: parse YAML numeric literals invariantly [patch] (Claude)

## v3.14.5 (patch)

Changes since v3.14.4:

- Write a Go type down over the whole declaration, not its first line (closes #63) [patch] (Claude)

## v3.14.4 (patch)

Changes since v3.14.3:

- style(test): use Assert.Contains over StringAssert.Contains on the changed lines (Claude)
- fix: stop JavaScript renaming a private member its references still name [patch] (Claude)

## v3.14.3 (patch)

Changes since v3.14.2:

- Write a positional construction as a Go composite literal (closes #68) [patch] (Claude)

## v3.14.2 (patch)

Changes since v3.14.1:

- Take the assertion Sonar names for the tests this PR adds [patch] (Claude)
- Quote a Python base that names the class being declared (closes #64) [patch] (Claude)

## v3.14.1 (patch)

Changes since v3.14.0:

- ci: adopt the consolidated .NET workflow [patch] (Claude)

## v3.14.0 (minor)

Changes since v3.13.0:

- Preserve fraction literal precision in inspector edits ([@Copilot](https://github.com/Copilot))
- Initial plan ([@Copilot](https://github.com/Copilot))

## v3.13.0 (minor)

Changes since v3.12.0:

- Let a construction hold a designated initialiser in the graph (closes #52) (Claude)

## v3.12.0 (minor)

Changes since v3.11.0:

- Put an array's brackets where C++ puts them, everywhere (closes #51) (Claude)

## v3.11.0 (minor)

Changes since v3.10.0:

- Fix the two Sonar findings this PR introduced (Claude)
- Address the review comment and the analyzer findings from this PR (Claude)
- Teach the AST what a property is, and where one lands (Claude)
- Teach a declaration to carry metadata (Claude)
- Teach a declaration the types it is written over (Claude)
- Teach a type declaration what it implements and what it promises (Claude)
- Make a node type that misses AstSchema or AstFields fail a test (Claude)

## v3.10.0 (minor)

Changes since v3.9.0:

- refactor: address the SonarCloud findings this PR introduced [patch] (Claude)
- Merge main, and teach Go the three nodes it gained (Claude)
- refactor: map the declarations rather than the loop variable [patch] (Claude)
- feat: add a Go language generator [minor] (Claude)

## v3.9.0 (minor)

Changes since v3.8.0:

- Address the SonarCloud findings from #57 (Claude)
- Merge main, and teach the new nodes the Rust generator (Claude)
- [minor] Add CallExpression, ExpressionStatement and ConditionalExpression (Claude)

## v3.8.0 (minor)

Changes since v3.7.0:

- refactor: name the two notes the comparison operators share (Claude)
- feat: add a Rust language generator [minor] (Claude)

## v3.7.0 (minor)

Changes since v3.6.0:

- refactor: filter the operator vocabulary explicitly (Claude)
- refactor: single-source what the C and C++ generators share (Claude)
- feat: add a C language generator [minor] (Claude)
- Filter the specialisation arguments before the loop reads them (matt-edmondson)
- Let a class declaration be for a type rather than of one [minor] (matt-edmondson)

## v3.6.0 (minor)

Changes since v3.5.0:

- Say what a constant table is, so a reflection table can be one (Claude)
- Follow the exemplar's rename (Claude)

## v3.5.1 (patch)

Changes since v3.5.0:

- Follow the exemplar's rename (Claude)

## v3.5.0 (minor)

Changes since v3.4.0:

- What a generated type promises that the type itself cannot say (Claude)

## v3.4.0 (minor)

Changes since v3.3.0:

- Say the repeated things once and split what got long (Claude)
- Cover the vocabulary a shim is built from (Claude)
- Cover what the graph and the clones actually do (Claude)
- A type can be a distinct name for something already representable (Claude)
- A type can declare behaviour for someone else to implement (Claude)
- An AST can describe a header, not only a snippet (Claude)

## v3.3.0 (minor)

Changes since v3.2.0:

- Read a type mapper without holding two branches in your head (Claude)
- A function can say it takes no receiver and changes nothing (Claude)
- A type is a type, not a string that happens to spell one (Claude)
- Bump the ktsu ImGui suite to 3.32.0 (Claude)

## v3.2.1 (patch)

Changes since v3.2.0:

- Bump the ktsu ImGui suite to 3.32.0 (Claude)

## v3.2.0 (minor)

Changes since v3.1.0:

- Merge main, and take the whole ImGui suite to 3.31.0 (Claude)
- Count the parameters with the collection assert (Claude)
- [minor] Draw the inspector's properties as a property grid (Claude)
- Bump the ktsu ImGui suite to 3.30.0 (Claude)
- [patch] Take the ktsu ImGui suite at 3.28.0 (Claude)
- Bump the ktsu ImGui suite to 3.27.0 (Claude)

## v3.1.3 (patch)

Changes since v3.1.2:

- Bump the ktsu ImGui suite to 3.30.0 (Claude)

## v3.1.2 (patch)

Changes since v3.1.1:

- [patch] Take the ktsu ImGui suite at 3.28.0 (Claude)

## v3.1.1 (patch)

Changes since v3.1.0:

- Bump the ktsu ImGui suite to 3.27.0 (Claude)

## v3.1.0 (minor)

Changes since v3.0.0:

- Tune the layout from a pane beside the graph (Claude)

## v3.0.0 (major)

Changes since v2.0.0:

- [major] Model visibility, constants and entry points in the AST (Claude)
- Take the ImGui suite at 3.25.0 (Claude)
- Take the ImGui suite at 3.24.0 (Claude)
- Take the ImGui suite at 3.23.0 (Claude)
- Take the ImGui suite at 3.22.0 (Claude)
- Merge remote-tracking branch 'origin/main' into claude/default-document-class (Claude)
- Rename the round-trip test's local root so it stops shadowing the fixture (Claude)
- Open on a class that does something, and fix loading an assignment (Claude)

## v2.1.3 (patch)

Changes since v2.1.2:

- Take the ImGui suite at 3.25.0 (Claude)

## v2.1.2 (patch)

Changes since v2.1.1:

- Take the ImGui suite at 3.24.0 (Claude)

## v2.1.1 (patch)

Changes since v2.1.0:

- Take the ImGui suite at 3.23.0 (Claude)

## v2.1.0 (minor)

Changes since v2.0.0:

- Take the ImGui suite at 3.22.0 (Claude)
- Merge remote-tracking branch 'origin/main' into claude/default-document-class (Claude)
- Rename the round-trip test's local root so it stops shadowing the fixture (Claude)
- Open on a class that does something, and fix loading an assignment (Claude)

## v2.0.0 (major)

Changes since v1.0.0:

- fix: keep the Coder.Cli sample out of the SonarCloud analysis [patch] (Claude)
- chore: remove the Coder.App sample [patch] (Claude)
- refactor!: publish the library as ktsu.Coder [major] (Claude)
- Take the ImGui suite at 3.21.0 (Claude)
- refactor: take the zoom from the node editor rather than doing it here [patch] (Claude)
- Update ktsu.ImGui.App and related packages to version 3.20.0 (matt-edmondson)
- Merge main into the ktsu.ImGui.NodeEditor rename (Claude)
- Move to the renamed ktsu.ImGui.NodeEditor package (Claude)
- fix: reference the package WindowState actually lives in [patch] (Claude)
- feat: resizable panes, zoom, and a window that opens where it was left [minor] (Claude)
- feat: highlight the generated source in the preview pane [minor] (Claude)
- feat: separate overlapping nodes, inspect on the right, adopt the root [minor] (Claude)
- feat: make the origin the middle of the canvas [minor] (Claude)
- fix: assert the layout's bounds one edge at a time [patch] (Claude)
- fix: declare the physics package the graph now uses directly [patch] (Claude)
- fix: give the inspector room, and keep the layout inside the view [patch] (Claude)
- feat: convert nodes in place, export generated source, and document it [minor] (Claude)
- feat: animate the layout, edit node properties, and declare classes [minor] (Claude)
- refactor: use ktsu.UndoRedo for the graph editor's history [minor] (Claude)
- feat: add the Coder editor application [minor] (Claude)
- test: run the graph editor's ImGui surface headlessly [patch] (Claude)
- feat: add a visual AST graph editor library [minor] (Claude)
- refactor: emit generated code through ktsu.CodeBlocker [minor] (Claude)
- feat: add unary operator support to the AST and every generator [minor] (Claude)
- fix: make the new code's string comparisons explicit [patch] (Claude)
- fix: clear the Sonar findings on the new generator code [patch] (Claude)
- refactor: finish de-duplicating the generators [patch] (Claude)
- refactor: single-source the generator logic the four languages share [patch] (Claude)
- feat: implement the JavaScript and C++ language generators [minor] (Claude)
- ci: make the SonarQube quality gate opt in [patch] (matt-edmondson)
- ci: adopt the unified dotnet workflow [patch] (matt-edmondson)
- chore: store icon.png in LFS as .gitattributes declares (matt-edmondson)
- docs: scope build badge to the default branch (matt-edmondson)
- docs: correct README, DESCRIPTION and TAGS metadata (matt-edmondson)
- Stop Update SDKs failing when there is nothing to update (matt-edmondson)
- Fix ktsu.Sdk 2.27 analyzer errors: Polyfill PrivateAssets, InternalsVisibleTo [patch] (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Sync .editorconfig (KtsuTools)
- Sync global.json (KtsuTools)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- chore: update ktsu.Sdk to 2.21.1 [patch] (matt-edmondson)
- fix: standardize null checks and exact exception assertions (matt-edmondson)
- chore: modernize project SDKs and target frameworks (matt-edmondson)
- Remove serena/cursor files (matt-edmondson)
- Regenerate TAGS.md with NuGet package tags (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Remove legacy build scripts (matt-edmondson)
- Enhance test assertions with descriptive messages in ExpressionTests and ExpressionIntegrationTests (matt-edmondson)
- Remove .github\workflows\project.yml (matt-edmondson)
- Refactor codebase to replace all instances of 'ktsu.Coder.Core' with 'ktsu.Coder' across files, enhancing consistency. Updated .runsettings for coverage configuration and modified .gitattributes for clarity. Removed obsolete test-expression-demo.cs file. Added new scripts for updating winget manifests and improved PSBuild.psm1 for better package publishing support. (matt-edmondson)
- Update configuration files and scripts for improved build and test processes (matt-edmondson)
- Enhance expression system integration and serialization support (matt-edmondson)
- Enhance YAML serialization and deserialization for new node types (matt-edmondson)
- Enhance YAML serialization and deserialization handling (matt-edmondson)
- Refactor project structure and enhance expression handling (matt-edmondson)
- Enhance expression handling in ExpressionDemo (matt-edmondson)
- Refactor expression handling and improve code consistency (matt-edmondson)
- Refactor AST expression handling and enhance visual editor support (matt-edmondson)
- Implement dependency injection and enhance AST expression handling (matt-edmondson)
- Update implementation and design documents to reflect current project status (matt-edmondson)

## v1.8.3 (patch)

Changes since v1.8.2:

- Take the ImGui suite at 3.21.0 (Claude)
- refactor: take the zoom from the node editor rather than doing it here [patch] (Claude)

## v1.8.2 (patch)

Changes since v1.8.1:

- Update ktsu.ImGui.App and related packages to version 3.20.0 (matt-edmondson)

## v1.8.1 (patch)

Changes since v1.8.0:

- Merge main into the ktsu.ImGui.NodeEditor rename (Claude)
- Move to the renamed ktsu.ImGui.NodeEditor package (Claude)

## v1.8.0 (minor)

Changes since v1.7.0:

- fix: reference the package WindowState actually lives in [patch] (Claude)
- feat: resizable panes, zoom, and a window that opens where it was left [minor] (Claude)

## v1.7.0 (minor)

Changes since v1.6.0:

- feat: highlight the generated source in the preview pane [minor] (Claude)

## v1.6.0 (minor)

Changes since v1.5.0:

- feat: separate overlapping nodes, inspect on the right, adopt the root [minor] (Claude)
- feat: make the origin the middle of the canvas [minor] (Claude)

## v1.5.0 (minor)

Changes since v1.4.0:

- fix: assert the layout's bounds one edge at a time [patch] (Claude)
- fix: declare the physics package the graph now uses directly [patch] (Claude)
- fix: give the inspector room, and keep the layout inside the view [patch] (Claude)
- feat: convert nodes in place, export generated source, and document it [minor] (Claude)
- feat: animate the layout, edit node properties, and declare classes [minor] (Claude)

## v1.4.0 (minor)

Changes since v1.3.0:

- refactor: use ktsu.UndoRedo for the graph editor's history [minor] (Claude)
- feat: add the Coder editor application [minor] (Claude)

## v1.3.0 (minor)

Changes since v1.2.0:

- test: run the graph editor's ImGui surface headlessly [patch] (Claude)
- feat: add a visual AST graph editor library [minor] (Claude)

## v1.2.0 (minor)

Changes since v1.1.0:

- refactor: emit generated code through ktsu.CodeBlocker [minor] (Claude)
- feat: add unary operator support to the AST and every generator [minor] (Claude)

## v1.1.0 (minor)

Changes since v1.0.0:

- fix: make the new code's string comparisons explicit [patch] (Claude)
- fix: clear the Sonar findings on the new generator code [patch] (Claude)
- refactor: finish de-duplicating the generators [patch] (Claude)
- refactor: single-source the generator logic the four languages share [patch] (Claude)
- feat: implement the JavaScript and C++ language generators [minor] (Claude)
- ci: make the SonarQube quality gate opt in [patch] (matt-edmondson)
- ci: adopt the unified dotnet workflow [patch] (matt-edmondson)
- chore: store icon.png in LFS as .gitattributes declares (matt-edmondson)
- docs: scope build badge to the default branch (matt-edmondson)
- docs: correct README, DESCRIPTION and TAGS metadata (matt-edmondson)
- Stop Update SDKs failing when there is nothing to update (matt-edmondson)
- Fix ktsu.Sdk 2.27 analyzer errors: Polyfill PrivateAssets, InternalsVisibleTo [patch] (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Sync .editorconfig (KtsuTools)
- Sync global.json (KtsuTools)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- chore: update ktsu.Sdk to 2.21.1 [patch] (matt-edmondson)
- fix: standardize null checks and exact exception assertions (matt-edmondson)
- chore: modernize project SDKs and target frameworks (matt-edmondson)
- Remove serena/cursor files (matt-edmondson)
- Regenerate TAGS.md with NuGet package tags (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Remove legacy build scripts (matt-edmondson)
- Enhance test assertions with descriptive messages in ExpressionTests and ExpressionIntegrationTests (matt-edmondson)
- Remove .github\workflows\project.yml (matt-edmondson)
- Refactor codebase to replace all instances of 'ktsu.Coder.Core' with 'ktsu.Coder' across files, enhancing consistency. Updated .runsettings for coverage configuration and modified .gitattributes for clarity. Removed obsolete test-expression-demo.cs file. Added new scripts for updating winget manifests and improved PSBuild.psm1 for better package publishing support. (matt-edmondson)
- Update configuration files and scripts for improved build and test processes (matt-edmondson)
- Enhance expression system integration and serialization support (matt-edmondson)
- Enhance YAML serialization and deserialization for new node types (matt-edmondson)
- Enhance YAML serialization and deserialization handling (matt-edmondson)
- Refactor project structure and enhance expression handling (matt-edmondson)
- Enhance expression handling in ExpressionDemo (matt-edmondson)
- Refactor expression handling and improve code consistency (matt-edmondson)
- Refactor AST expression handling and enhance visual editor support (matt-edmondson)
- Implement dependency injection and enhance AST expression handling (matt-edmondson)
- Update implementation and design documents to reflect current project status (matt-edmondson)

## v1.0.10 (patch)

Changes since v1.0.9:

- ci: make the SonarQube quality gate opt in [patch] (matt-edmondson)
- ci: adopt the unified dotnet workflow [patch] (matt-edmondson)

## v1.0.9 (patch)

Changes since v1.0.8:

- chore: store icon.png in LFS as .gitattributes declares (matt-edmondson)
- docs: scope build badge to the default branch (matt-edmondson)
- docs: correct README, DESCRIPTION and TAGS metadata (matt-edmondson)

## v1.0.8 (patch)

Changes since v1.0.7:

- Stop Update SDKs failing when there is nothing to update (matt-edmondson)

## v1.0.7 (patch)

Changes since v1.0.6:

- Fix ktsu.Sdk 2.27 analyzer errors: Polyfill PrivateAssets, InternalsVisibleTo [patch] (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Sync .editorconfig (KtsuTools)
- Sync global.json (KtsuTools)
- Sync .github\workflows\dotnet.yml (KtsuTools)

## v1.0.6 (patch)

Changes since v1.0.5:

- chore: update ktsu.Sdk to 2.21.1 [patch] (matt-edmondson)

## v1.0.5 (patch)

Changes since v1.0.4:

- Merge remote-tracking branch 'refs/remotes/origin/main' (ktsu[bot])
- Sync .github\workflows\dotnet.yml (ktsu[bot])
- Sync global.json (ktsu[bot])

## v1.0.4 (patch)

Changes since v1.0.3:

- fix: standardize null checks and exact exception assertions (matt-edmondson)
- chore: modernize project SDKs and target frameworks (matt-edmondson)
- Remove serena/cursor files (matt-edmondson)
- Regenerate TAGS.md with NuGet package tags (matt-edmondson)
- Sync .github\workflows\dotnet.yml (KtsuTools)
- Remove legacy build scripts (matt-edmondson)
- Enhance test assertions with descriptive messages in ExpressionTests and ExpressionIntegrationTests (matt-edmondson)
- Remove .github\workflows\project.yml (matt-edmondson)
- Refactor codebase to replace all instances of 'ktsu.Coder.Core' with 'ktsu.Coder' across files, enhancing consistency. Updated .runsettings for coverage configuration and modified .gitattributes for clarity. Removed obsolete test-expression-demo.cs file. Added new scripts for updating winget manifests and improved PSBuild.psm1 for better package publishing support. (matt-edmondson)
- Update configuration files and scripts for improved build and test processes (matt-edmondson)

## v1.0.4-pre.1 (prerelease)

Changes since v1.0.3:

- Bump the microsoft group with 2 updates (dependabot[bot])

## v1.0.3 (patch)

Changes since v1.0.2:

- Enhance expression system integration and serialization support (matt-edmondson)
- Enhance YAML serialization and deserialization for new node types (matt-edmondson)

## v1.0.2 (patch)

Changes since v1.0.1:

- Enhance YAML serialization and deserialization handling (matt-edmondson)
- Refactor project structure and enhance expression handling (matt-edmondson)
- Enhance expression handling in ExpressionDemo (matt-edmondson)
- Refactor expression handling and improve code consistency (matt-edmondson)

## v1.0.1 (patch)

Changes since v1.0.0:

- Refactor AST expression handling and enhance visual editor support (matt-edmondson)
- Implement dependency injection and enhance AST expression handling (matt-edmondson)
- Update implementation and design documents to reflect current project status (matt-edmondson)

## v1.0.0 (major)

- Enhance Coder.App with type consistency and testing improvements (matt-edmondson)
- Update project configuration and enhance build scripts (matt-edmondson)
- Enhance Coder.App with XML documentation and null handling improvements (matt-edmondson)
- Transform Coder.App into a TUI using Spectre.Console (matt-edmondson)
- Refactor Coder library to address build errors and improve serialization logic (matt-edmondson)
- Enhance Coder library with comprehensive CLI and App examples (matt-edmondson)
- Add initial project files and implementation plan for Coder library (matt-edmondson)
- Refactor AST node classes to improve deep cloning and metadata handling (matt-edmondson)
- Update package references in Coder.Core.csproj (matt-edmondson)
- Refactor AST implementation and improve serialization (matt-edmondson)
- Remove Sample class and associated unit tests from the project (matt-edmondson)
- Refactor AST node classes and improve YAML serialization (matt-edmondson)
- Add AST node classes and YAML serialization support (matt-edmondson)
- Design and implementation plan documentation (matt-edmondson)
- Initial commit for Coder (matt-edmondson)

