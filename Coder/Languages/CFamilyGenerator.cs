// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

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

	/// <inheritdoc/>
	/// <remarks>
	/// A designated initialiser, which C invented and C++20 adopted — with the one difference that
	/// C++ requires the designators to appear in declaration order. That is the caller's business:
	/// the generator writes the order it is given.
	/// </remarks>
	protected override void WriteDesignator(string name, CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.Write($".{name} = ");
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
	/// </remarks>
	protected void WriteBracedList(ConstructionExpression construction, CodeBlocker code, string emptyList) =>
		WriteElementList(construction, code, "{", "}", emptyList);
}
