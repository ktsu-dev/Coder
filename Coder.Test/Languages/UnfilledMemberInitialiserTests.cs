// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a braced-list member initialiser with no value yet is left out rather than thrown on.
/// </summary>
/// <remarks>
/// <c>new MemberInitialiser("x")</c> is legal, and is what the graph editor holds between adding a
/// member initialiser and filling it in, so the preview generates it on every edit in between.
/// </remarks>
[TestClass]
public class UnfilledMemberInitialiserTests
{
	/// <summary>
	/// Tests that every generator writes a list holding an unfilled member, and writes its filled
	/// sibling.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	[TestMethod]
	[DataRow("csharp")]
	[DataRow("c")]
	[DataRow("cpp")]
	[DataRow("go")]
	[DataRow("rust")]
	[DataRow("python")]
	[DataRow("javascript")]
	public void UnfilledMember_IsLeftOut(string language)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		ConstructionExpression point = new("Point");
		point.Arguments.Add(new MemberInitialiser("unfilled"));
		point.Arguments.Add(new MemberInitialiser("y", Literal.Number(2)));

		string code = generator.Generate(new VariableDeclaration { Name = "p", Type = "Point", InitialValue = point });

		Assert.DoesNotContain("unfilled", code, StringComparison.Ordinal, $"{language} wrote {code}");
		Assert.Contains("y", code, StringComparison.Ordinal, $"{language} wrote {code}");
		Assert.Contains("2", code, StringComparison.Ordinal, $"{language} wrote {code}");
	}

	/// <summary>
	/// Tests that every generator writes a typeless list whose only element is unfilled.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	[TestMethod]
	[DataRow("csharp")]
	[DataRow("c")]
	[DataRow("cpp")]
	[DataRow("go")]
	[DataRow("rust")]
	[DataRow("python")]
	[DataRow("javascript")]
	public void OnlyElementUnfilled_IsAnEmptyList(string language)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		ConstructionExpression list = new();
		list.Arguments.Add(new MemberInitialiser("unfilled"));

		string code = generator.Generate(new VariableDeclaration { Name = "p", Type = "Point", InitialValue = list });

		Assert.DoesNotContain("unfilled", code, StringComparison.Ordinal, $"{language} wrote {code}");
	}

	/// <summary>
	/// Tests that a constructor initialiser with no value value-initialises its member, as C++'s
	/// <c>x()</c> does, in every target that writes it (issue #178).
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	/// <param name="expected">How the target spells the initialiser.</param>
	/// <remarks>
	/// C and Go start every member at its zero value and leave the initialiser out, which they
	/// already did; the compile test below covers them.
	/// </remarks>
	[TestMethod]
	[DataRow("csharp", "this.x = default;")]
	[DataRow("cpp", ": x()")]
	[DataRow("rust", "Self { x: Default::default() }")]
	[DataRow("python", "self.x = None")]
	[DataRow("javascript", "this.x = undefined;")]
	public void UnfilledConstructorInitialiser_ValueInitialisesTheMember(string language, string expected)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		string code = generator.Generate(PointWithUnfilledConstructorInitialiser());

		Assert.Contains(expected, code, StringComparison.Ordinal, $"{language} wrote {code}");
	}

	/// <summary>
	/// Tests that the type whose constructor holds an unfilled initialiser compiles, or parses where
	/// the target has no compiler, in every target the tests have a toolchain for.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	/// <remarks>
	/// Generated C# is not compiled anywhere in the tests yet (#162), so it is checked by spelling
	/// alone, above.
	/// </remarks>
	[TestMethod]
	[DataRow("c")]
	[DataRow("cpp")]
	[DataRow("go")]
	[DataRow("rust")]
	[DataRow("python")]
	[DataRow("javascript")]
	public void UnfilledConstructorInitialiser_Compiles(string language)
	{
		(string file, string command, string arguments, string version) = language switch
		{
			"c" => ("point.c", "gcc", "-std=c11 -Wall -Werror -fsyntax-only point.c", "--version"),
			"cpp" => ("point.cpp", "g++", "-std=c++20 -Wall -Werror -fsyntax-only point.cpp", "--version"),
			"go" => ("point.go", "go", "build ./...", "version"),
			"rust" => ("point.rs", "rustc", "--crate-type lib --edition 2021 -A dead_code -o point.rlib point.rs", "--version"),
			"python" => ("point.py", "python3", "-m py_compile point.py", "--version"),
			_ => ("point.js", "node", "--check point.js", "--version"),
		};

		if (ToolchainHarness.FindOnPath(version, command) is null)
		{
			Assert.Inconclusive($"No {command} on the path, so nothing was checked.");
			return;
		}

		string code = Generators().Single(g => g.LanguageId == language).Generate(PointWithUnfilledConstructorInitialiser());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, file), code);
			if (language == "go")
			{
				File.WriteAllText(Path.Combine(directory, "go.mod"), $"module point{Environment.NewLine}{Environment.NewLine}go 1.21{Environment.NewLine}");
			}

			(int exitCode, string output) = ToolchainHarness.Run(command, arguments, directory);
			Assert.AreEqual(0, exitCode, $"{command} rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{code}");
		});
	}

	/// <summary>
	/// Builds the issue's type: one field, and a constructor whose only initialiser names it with no
	/// value.
	/// </summary>
	/// <returns>The file holding the type.</returns>
	private static SourceFile PointWithUnfilledConstructorInitialiser()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new VariableDeclaration("x", "int"));
		FunctionDeclaration constructor = new("Point") { Kind = FunctionKind.Constructor };
		constructor.Initialisers.Add(new MemberInitialiser("x"));
		point.Members.Add(constructor);

		SourceFile file = new("point");
		file.Members.Add(point);
		return file;
	}

	private static ILanguageGenerator[] Generators() =>
	[
		new CSharpGenerator(),
		new CGenerator(),
		new CppGenerator(),
		new GoGenerator(),
		new RustGenerator(),
		new PythonGenerator(),
		new JavaScriptGenerator(),
	];
}
