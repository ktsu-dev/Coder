// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Linq;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a string literal's control characters and Unicode line terminators are written as escapes.
/// </summary>
/// <remarks>
/// Written raw, a NUL is a compile error in Go and Python, and U+0085, U+2028 and U+2029 end the line
/// inside a C# string. The other controls compile but leave invisible bytes in the generated source.
/// </remarks>
[TestClass]
public class StringLiteralControlCharacterTests
{
	/// <summary>
	/// NUL, ESC, DEL, a newline, NEL, and the two Unicode line terminators, with a hex digit after ESC
	/// so a greedy <c>\x</c> escape would swallow it.
	/// </summary>
	private const string Value = "\0\u001bb\u007f\n\u0085\u2028\u2029";

	/// <summary>
	/// Tests that each generator writes the string's controls as its own escapes, with no raw control
	/// character left in the output.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	/// <param name="expected">The literal the generator should write.</param>
	[TestMethod]
	[DataRow("csharp", @"""\u0000\u001Bb\u007F\n\u0085\u2028\u2029""")]
	[DataRow("javascript", @"""\u0000\u001Bb\u007F\n\u0085\u2028\u2029""")]
	[DataRow("python", @"""\u0000\u001Bb\u007F\n\u0085\u2028\u2029""")]
	[DataRow("go", @"""\u0000\u001Bb\u007F\n\u0085\u2028\u2029""")]
	[DataRow("rust", @"""\u{0000}\u{001B}b\u{007F}\n\u{0085}\u{2028}\u{2029}""")]
	[DataRow("c", @"""\000\033b\177\n\302\205\342\200\250\342\200\251""")]
	[DataRow("cpp", @"""\000\033b\177\n\302\205\342\200\250\342\200\251""")]
	public void ControlCharactersAreEscaped(string language, string expected)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		string code = generator.Generate(Literal.Text(Value));

		Assert.Contains(expected, code, StringComparison.Ordinal, $"{language} wrote {code}");
		Assert.IsFalse(code.Any(IsRawControl), $"{language} left a raw control character in {code}");
	}

	/// <summary>
	/// Tests that a string with nothing to escape numerically is written as before.
	/// </summary>
	[TestMethod]
	public void PrintableTextIsWrittenAsIs()
	{
		foreach (ILanguageGenerator generator in Generators())
		{
			string code = generator.Generate(Literal.Text("caf\u00e9 \"x\"\t"));

			Assert.Contains(@"""café \""x\""\t""", code, StringComparison.Ordinal, $"{generator.LanguageId} wrote {code}");
		}
	}

	/// <summary>
	/// Reports whether a character is one the generators must not leave raw inside a literal. A line
	/// break between statements is expected, so only one inside a literal would be wrong, and the
	/// expected literal already pins that.
	/// </summary>
	private static bool IsRawControl(char c) =>
		c is (< '\u0020' and not '\n' and not '\r') or '\u007f' or '\u0085' or '\u2028' or '\u2029';

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
