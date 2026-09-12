// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// A <see cref="StandardLanguageGenerator"/> for the two targets that are the same language
/// underneath: what C and C++ share beyond what every generator shares.
/// </summary>
/// <remarks>
/// <see cref="StandardLanguageGenerator"/> owns what is common to every target, and most of it is
/// about the shape of the AST rather than about any language. This class owns what is common to
/// these two in particular, and all of it is about C: the preprocessor, the braced list, and the
/// declarator syntax that puts an array's brackets after the name rather than after the type.
/// <para>
/// Python and JavaScript are not C-family in any of those ways — neither has a preprocessor, an
/// array declarator or a designated initialiser — so this sits between them and their common base
/// rather than in it. <c>CSharpGenerator</c> is C-family in its syntax and is not here either,
/// because it does not derive from <see cref="StandardLanguageGenerator"/> at all.
/// </para>
/// <para>
/// Two things stay with each generator that might look shareable and are not. The type mappings are
/// one, because <c>str</c> is a <c>std::string</c> in one language and a <c>const char*</c> in the
/// other, and the whole of what a mapping is is the spelling. Documentation comments are the other:
/// both write <c>///</c>, which they inherit rather than agree on.
/// </para>
/// </remarks>
public abstract class CFamilyGenerator : StandardLanguageGenerator
{
	/// <inheritdoc/>
	/// <remarks>
	/// A function declared on its own belongs to no type, which is what the null says. A member is
	/// reached through <see cref="GenerateFunction"/> directly, by whichever emitter knows the name
	/// of the type it belongs to — the declaration does not.
	/// </remarks>
	protected sealed override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code) =>
		GenerateFunction(funcDecl, code, null);

	/// <summary>
	/// Emits a function, which may have been declared as a member of a type.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <remarks>
	/// The type's name is a parameter rather than something read off the declaration because the
	/// declaration does not carry it, and because both languages need it for a name they cannot
	/// otherwise spell — C++'s constructor and destructor are named after the type, and C's every
	/// member function is.
	/// </remarks>
	protected abstract void GenerateFunction(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType);

	/// <summary>
	/// Spells a type in the target language.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The source for it.</returns>
	/// <remarks>
	/// The one thing the two languages disagree about everywhere, and the hook everything here that
	/// needs a type name goes through.
	/// </remarks>
	protected abstract string SpellType(TypeReference type);

	/// <inheritdoc/>
	/// <remarks>
	/// <c>#pragma once</c> rather than an include guard, for the reason a guard cannot answer: the
	/// macro it needs must be unique across the whole program, which the file cannot know it has and
	/// which a generator picking one would eventually collide on. Every compiler either language
	/// targets supports the pragma.
	/// </remarks>
	protected override bool WriteFileDirectives(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);

		if (!file.IsHeader)
		{
			return false;
		}

		code.WriteLine("#pragma once");
		return true;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An import that already carries its own delimiters is written as it stands, because the choice
	/// between <c>&lt;&gt;</c> and <c>""</c> says where the compiler should look and only whoever
	/// wrote the file knows that. One that carries neither is quoted, which is right for a path
	/// within the project being generated.
	/// </remarks>
	protected override string? SpellImport(string import)
	{
		Ensure.NotNull(import);

		bool delimited = (import.StartsWith('<') && import.EndsWith('>'))
			|| (import.StartsWith('"') && import.EndsWith('"'));

		return delimited ? $"#include {import}" : $"#include \"{import}\"";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Two of a kind that say nothing about themselves stay together, which is what keeps a run of
	/// aliases, of defaulted declarations, or of assertions about one type reading as one block
	/// rather than as four paragraphs. A documented member needs air above it or its first comment
	/// line butts against the member before it and reads as belonging to that one.
	/// </remarks>
	protected override bool NeedsSeparation(AstNode previous, AstNode member)
	{
		Ensure.NotNull(previous);
		Ensure.NotNull(member);

		return previous.GetType() != member.GetType()
			|| IsDocumented(previous)
			|| IsDocumented(member);
	}

	/// <summary>
	/// Reports whether a member carries documentation.
	/// </summary>
	/// <param name="member">The member to test.</param>
	/// <returns>True when it does.</returns>
	protected static bool IsDocumented(AstNode member) =>
		member is IHasDocumentation documented && documented.Documentation.Count > 0;

	/// <summary>
	/// Spells a declaration of <paramref name="name"/> with that type.
	/// </summary>
	/// <param name="type">The declared type.</param>
	/// <param name="name">The name being declared.</param>
	/// <returns>The declaration, without an initialiser or a terminator.</returns>
	/// <remarks>
	/// Both languages put an array's brackets on the declarator rather than on the type —
	/// <c>T name[]</c>, never <c>T[] name</c> — so a declaration cannot be built by writing the type
	/// and the name in that order, which is what every other language here does. This is the one
	/// place that difference lives.
	/// </remarks>
	protected string SpellDeclarator(TypeReference type, string name)
	{
		Ensure.NotNull(type);

		TypeReference element = type.IsArray ? type.Clone() : type;
		if (type.IsArray)
		{
			element.IsArray = false;
		}

		return $"{SpellType(element)} {name}{(type.IsArray ? "[]" : string.Empty)}";
	}

	/// <summary>
	/// Writes a braced list, without whatever the language writes in front of it.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="emptyList">What to write when there are no arguments at all.</param>
	/// <remarks>
	/// The empty list is the caller's because it is the one part the two languages spell
	/// differently: <c>{}</c> is C++'s, and C only allows it from C23.
	/// <para>
	/// A list of values is a value and belongs on one line; a list whose elements are themselves
	/// lists is a table, and a table written on one line is a row of a diff nobody can read. The test
	/// is the shape of the data rather than a column count, because a generated file has no idea how
	/// wide anyone's editor is and a rule about that would have to be guessed.
	/// </para>
	/// </remarks>
	protected void WriteBracedList(ConstructionExpression construction, CodeBlocker code, string emptyList)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Arguments.Count == 0)
		{
			code.Write(emptyList);
			return;
		}

		if (SpansLines(construction))
		{
			WriteStackedList(construction, code);
			return;
		}

		code.Write("{ ");
		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			WriteListElement(construction.Arguments[index], code);
		}

		code.Write(" }");
	}

	/// <summary>
	/// Writes a braced list one element per line.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A trailing comma after the last element, which both languages allow in a braced list and which
	/// keeps adding a row to a generated table from touching the row above it in the diff.
	/// </remarks>
	private void WriteStackedList(ConstructionExpression construction, CodeBlocker code)
	{
		code.WriteLine("{");
		code.Indent();

		foreach (AstNode argument in construction.Arguments)
		{
			WriteListElement(argument, code);
			code.WriteLine(",");
		}

		code.Outdent();
		code.Write("}");
	}

	/// <summary>
	/// Writes one element of a braced list, which may name the member it is for.
	/// </summary>
	/// <param name="argument">The element to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A <see cref="MemberInitialiser"/> is a designated initialiser, spelled the same in both
	/// languages — C invented it and C++20 adopted it, with the one difference that C++ requires the
	/// designators to appear in declaration order. That is the caller's business: the generator
	/// writes the order it is given.
	/// </remarks>
	private void WriteListElement(AstNode argument, CodeBlocker code)
	{
		if (argument is MemberInitialiser designated)
		{
			code.Write($".{designated.Name} = ");
			WriteListValue(designated.Value ?? new VariableReference(string.Empty), code);
			return;
		}

		WriteListValue(argument, code);
	}

	/// <summary>
	/// Writes what one element of a braced list is.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Ordinary generation, unless a language has something to say about a value that stands inside
	/// a list rather than on its own — which C does, since only there may it leave the type out.
	/// </remarks>
	protected virtual void WriteListValue(AstNode value, CodeBlocker code) => GenerateInternal(value, code);

	/// <summary>
	/// Reports whether a braced list is worth breaking across lines.
	/// </summary>
	/// <param name="construction">The expression to judge.</param>
	/// <returns><see langword="true"/> when it should be written one element per line.</returns>
	private static bool SpansLines(ConstructionExpression construction) =>
		construction.Arguments.Any(argument =>
			argument is ConstructionExpression or MemberInitialiser { Value: ConstructionExpression });
}
