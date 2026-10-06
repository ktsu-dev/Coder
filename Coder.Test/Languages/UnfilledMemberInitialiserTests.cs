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
