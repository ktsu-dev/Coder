// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates Go code from AST nodes.
/// </summary>
/// <remarks>
/// Go is the target that answers most of the AST with something it already had rather than with
/// something built for the occasion. A type is a <c>struct</c> and its behaviour is methods declared
/// beside it; an interface is an <c>interface</c>, satisfied by whatever has the methods rather than
/// by saying so; a base type is an <b>embedded field</b>, whose members are promoted, which is as
/// near as Go comes to inheritance and nearer than anything the other targets without it manage.
/// A member that promises not to modify what it is called on takes a value receiver and one that
/// does takes a pointer receiver — the same promise C++ writes as a trailing <c>const</c>, made by
/// the shape of the declaration rather than by a modifier on it.
/// <para>
/// What Go does not have is more interesting than what it does, because in each case it left the
/// feature out rather than not reaching it. There is no operator overloading, so an operator is a
/// method named for what it does — <c>Add</c>, <c>LessThan</c> — taken from the AST's own word for
/// it. There are no constructors, so one is the <c>New<i>Type</i></c> function every Go package
/// writes instead, and it needs no fallback when there is nothing to build from, because every Go
/// type has a zero value and that is what the declaration described. There are no destructors: the
/// convention is a <c>Close</c> the caller defers, which unlike a destructor has to be called.
/// And there is no <c>const</c> beyond numbers, strings and booleans, so a constant table is a
/// <c>var</c> — which is what the language means by constant, rather than a gap in it.
/// </para>
/// <para>
/// Visibility is the one thing Go says in a way no generator can write: a name is exported when its
/// first letter is a capital, and there is no keyword. Renaming a declaration to match what it asked
/// for would not rename the references to it, which is the rule every generator here keeps, so a
/// name that disagrees with its declared visibility gets a note saying so. Unexported is Go's only
/// other answer and it means package-private, so <see cref="Visibility.Internal"/> is exactly right
/// and <see cref="Visibility.Private"/> and <see cref="Visibility.Protected"/> are as near as there
/// is.
/// </para>
/// <para>
/// The output is what <c>gofmt</c> would write, which is why this generator indents with a tab and
/// lines up the columns of a struct's fields and a constant block. That is not a style anybody here
/// chose: Go has one formatter, everybody runs it, and a generated file it disagrees with is a diff
/// the first time anyone opens it.
/// </para>
/// </remarks>
public class GoGenerator : StandardLanguageGenerator
{
	/// <summary>
	/// What a declaration that never said what type it is gets.
	/// </summary>
	/// <remarks>
	/// A type is optional on every node that carries one, because a half-built AST is a thing the
	/// editor has to be able to hold. Go's most general type is <c>any</c>, which keeps the output
	/// compiling while making it obvious which declaration was never finished.
	/// </remarks>
	private const string UnknownTypeName = "object";

	/// <summary>
	/// The name a method's receiver is bound to.
	/// </summary>
	/// <remarks>
	/// Go style asks for a one- or two-letter abbreviation of the type, and this is not one, for the
	/// reason C names its receiver the same thing: a body is text the generator cannot rewrite, so
	/// whoever wrote the statements had to know what the instance would be called. A name that is
	/// the same whatever the type is, is the only kind they could have known.
	/// </remarks>
	private const string ReceiverName = "self";

	/// <summary>
	/// The package a file with an entry point is in.
	/// </summary>
	/// <remarks>
	/// Not a default: Go runs <c>main</c> in a package called <c>main</c> and nowhere else, so a file
	/// holding an entry point is in that package whatever its namespace says.
	/// </remarks>
	private const string MainPackage = "main";

	/// <summary>
	/// The name a destructor is written under.
	/// </summary>
	/// <remarks>
	/// Go has no destructor. <c>Close</c> is the convention for releasing what a value holds — it is
	/// what <c>io.Closer</c> asks for and what <c>defer</c> exists to pair with — and the one thing it
	/// does not share with a destructor is that somebody has to call it.
	/// </remarks>
	private const string CloseName = "Close";

	/// <summary>
	/// What a type declaration with no name is written under.
	/// </summary>
	private const string UnnamedType = "UnnamedType";

	/// <summary>
	/// What a member with no name is written under.
	/// </summary>
	private const string UnnamedMember = "unnamed";

	/// <summary>
	/// The package an entry point reaches for its arguments and its exit code.
	/// </summary>
	private const string RuntimePackage = "\"os\"";

	/// <summary>
	/// How Go spells a string.
	/// </summary>
	/// <remarks>
	/// Named because the generator asks three different questions of it: which Go type a name maps
	/// to, whether a type is already a view of what it holds, and whether a conversion is the one the
	/// standard library prints a value with.
	/// </remarks>
	private const string StringTypeName = "string";

	private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "str", StringTypeName },
		{ "string", StringTypeName },
		{ "int", "int" },
		{ "long", "int64" },
		{ "float", "float32" },
		{ "double", "float64" },
		{ "bool", "bool" },
		{ "void", "" },
		{ "object", "any" },
	};

	/// <summary>
	/// The name of each operator, for a language that cannot overload one and so must call it
	/// something.
	/// </summary>
	/// <remarks>
	/// The AST's own word for the operator, unchanged: <c>+</c> is <c>Add</c> and <c>&lt;</c> is
	/// <c>LessThan</c>, which is both what the enumeration calls them and how Go names a method. C
	/// reaches the same vocabulary through the same builder and lowercases it, which is the whole of
	/// the difference between naming a thing in the two languages.
	/// </remarks>
	private static readonly Dictionary<string, string> OperatorNames = BuildOperatorNames(word => word);

	/// <summary>
	/// The name of each unary operator, which is not always the name of the binary one sharing its
	/// symbol.
	/// </summary>
	/// <remarks>
	/// Kept apart because <c>-</c> is in both, and a type declaring subtraction and negation would
	/// otherwise declare <c>Subtract</c> twice — which Go refuses, as it should. Which one a
	/// declaration means is decided by whether it takes an operand beside the instance, the same
	/// question Rust asks of the same symbol.
	/// </remarks>
	private static readonly Dictionary<string, string> UnaryOperatorNames = BuildUnaryOperatorNames(word => word);

	/// <summary>
	/// Whether what is being written is a member of an interface.
	/// </summary>
	/// <remarks>
	/// An interface holds signatures, so a member there is written without <c>func</c>, without a
	/// receiver and without a body — and a member with no body is a requirement rather than something
	/// missing.
	/// </remarks>
	private bool insideInterface;

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "go";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "Go";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "go";

	/// <inheritdoc/>
	/// <remarks>
	/// A tab, because <c>gofmt</c> writes a tab. This is the one target where the indentation is not
	/// the generator's to pick.
	/// </remarks>
	protected override string IndentString => "\t";

	/// <inheritdoc/>
	/// <remarks>
	/// Go's documentation is an ordinary comment in the right place: a run of <c>//</c> lines
	/// directly above a declaration, with no blank line between, is that declaration's doc comment
	/// and is what <c>go doc</c> prints. There is no second marker, and writing one would make the
	/// comment stop being documentation.
	/// </remarks>
	protected override string DocumentationPrefix => "//";

	/// <inheritdoc/>
	/// <remarks>
	/// Nothing between a literal's braces and its elements: <c>Point{X: 1}</c>, which is what
	/// <c>gofmt</c> writes.
	/// </remarks>
	protected override string ListPadding => string.Empty;

	/// <inheritdoc/>
	/// <remarks>
	/// The line break alone. Go's grammar wants a semicolon and its lexer inserts one at the end of
	/// the line, so a generator writing one would be writing the character <c>gofmt</c> deletes.
	/// </remarks>
	protected override void EndStatement(CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.WriteLine();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>^</c>. Go spells bitwise complement with the same character as exclusive-or and has no
	/// <c>~</c> outside a type constraint, which is the one operator in the AST's vocabulary it does
	/// not share with the C family.
	/// </remarks>
	protected override string GetUnaryOperatorSpelling(UnaryOperator op) =>
		op == UnaryOperator.BitwiseNot ? "^" : GetUnaryOperator(op);

	/// <inheritdoc/>
	/// <remarks>
	/// Nothing: a Go file's imports are written with its package clause, by
	/// <see cref="WriteFileDirectives"/>, because the two are one header and have to appear in that
	/// order before anything else.
	/// </remarks>
	protected override string? SpellImport(string import) => null;

	/// <inheritdoc/>
	/// <remarks>
	/// The package clause and the import block, which is the whole of a Go file's header and is the
	/// one part of it whose order the language fixes.
	/// <para>
	/// Writing the imports here rather than one at a time is what lets the block hold one the file
	/// did not ask for. An entry point that reads its arguments or answers an exit code reaches
	/// <c>os</c>, and Go has no way to name a package without importing it — nor to leave an import
	/// unused, which is an error rather than a warning, so it is added exactly when it is about to be
	/// used. Nothing similar can be done for an import a <em>body</em> needs: a statement is text,
	/// and what it depends on is the file's to declare.
	/// </para>
	/// <para>
	/// A group is sorted and the groups are kept apart, which is what <c>gofmt</c> does to an import
	/// block: it sorts within each run of lines and leaves a blank line where it finds one. The AST
	/// already spells a group boundary as an empty import, so the two agree without being made to.
	/// </para>
	/// </remarks>
	protected override bool WriteFileDirectives(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);

		code.WriteLine($"package {PackageOf(file)}");

		List<List<string>> groups = ImportGroups(file);
		if (groups.Count == 0)
		{
			return true;
		}

		code.NewLine();

		// One import is written on the line, which is what a Go file with one import looks like.
		if (groups is [[string only]])
		{
			code.WriteLine($"import {only}");
			return true;
		}

		code.Write("import ");

		using ParenScope block = new(code);

		bool first = true;
		foreach (List<string> group in groups)
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;

			foreach (string import in group)
			{
				code.WriteLine(import);
			}
		}

		return true;
	}

	/// <summary>
	/// Gives the package a file declares.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <returns>The package name.</returns>
	/// <remarks>
	/// A file holding an entry point is <c>main</c>, whatever else it says: that is where Go runs
	/// one. Otherwise the namespace names it, and failing that the file does.
	/// </remarks>
	private static string PackageOf(SourceFile file)
	{
		if (EntryPoints(file.Members).Any())
		{
			return MainPackage;
		}

		foreach (NamespaceDeclaration declared in file.Members.OfType<NamespaceDeclaration>())
		{
			if (NamespaceDeclaration.Split(declared.Name) is [.., string leaf])
			{
				return PackageName(leaf);
			}
		}

		return PackageName(file.Name ?? string.Empty);
	}

	/// <summary>
	/// Folds a name into something Go will accept as a package name.
	/// </summary>
	/// <param name="name">The name to fold.</param>
	/// <returns>The package name.</returns>
	/// <remarks>
	/// Lower case and letters only, which is what Go asks a package to be named — and the one place
	/// here a name is recased, because a package name is not referred to by the declarations in it.
	/// </remarks>
	private static string PackageName(string name)
	{
		string folded = string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
		return folded.Length == 0 || char.IsDigit(folded[0]) ? MainPackage : folded;
	}

	/// <summary>
	/// Gives the import block's groups, in the order they are written.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <returns>Each group's import lines, sorted by path, with the empty groups dropped.</returns>
	private static List<List<string>> ImportGroups(SourceFile file)
	{
		List<List<string>> groups = [[]];

		foreach (string import in file.Imports)
		{
			// An empty import is a group separator rather than an import of nothing.
			if (import.Length == 0)
			{
				groups.Add([]);
				continue;
			}

			groups[^1].Add(Quoted(import));
		}

		if (EntryPoints(file.Members).Any(entry => entry.AcceptsArguments || entry.ReturnsExitCode)
			&& !groups.Any(group => group.Contains(RuntimePackage)))
		{
			groups[0].Add(RuntimePackage);
		}

		foreach (List<string> group in groups)
		{
			group.Sort((left, right) => string.CompareOrdinal(ImportPath(left), ImportPath(right)));
		}

		return [.. groups.Where(group => group.Count > 0)];
	}

	/// <summary>
	/// Quotes an import path, unless whoever wrote it already spelled the whole item.
	/// </summary>
	/// <param name="import">The import as the file carries it.</param>
	/// <returns>The import line, without the keyword.</returns>
	/// <remarks>
	/// A path is quoted in Go, so text that already carries a quote is the whole item — a renaming
	/// import, <c>f "fmt"</c>, or a blank one — and is written as it stands.
	/// </remarks>
	private static string Quoted(string import) =>
		import.Contains('"', StringComparison.Ordinal) ? import : $"\"{import}\"";

	/// <summary>
	/// Gives the path an import line imports, which is what it sorts by.
	/// </summary>
	/// <param name="import">The import line.</param>
	/// <returns>The path, without its quotes.</returns>
	private static string ImportPath(string import)
	{
		int open = import.IndexOf('"', StringComparison.Ordinal);
		int close = import.LastIndexOf('"');
		return open >= 0 && close > open ? import[(open + 1)..close] : import;
	}

	/// <summary>
	/// Finds every entry point a file declares, wherever its namespaces put them.
	/// </summary>
	/// <param name="members">The declarations to search.</param>
	/// <returns>The entry points.</returns>
	private static IEnumerable<EntryPoint> EntryPoints(IEnumerable<AstNode> members)
	{
		foreach (AstNode member in members)
		{
			if (member is EntryPoint entryPoint)
			{
				yield return entryPoint;
			}
			else if (member is NamespaceDeclaration declared)
			{
				foreach (EntryPoint nested in EntryPoints(declared.Members))
				{
					yield return nested;
				}
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Two of a kind that say nothing about themselves stay together, which keeps a run of type
	/// aliases or of assertions reading as one block rather than as a paragraph each — but only where
	/// each of them is one line. A type or a function is a paragraph in Go whatever precedes it, and
	/// a declaration with a body butted against a one-line one reads as part of it.
	/// </remarks>
	protected override bool NeedsSeparation(AstNode previous, AstNode member) =>
		!GroupsWith(previous, member) || member is ClassDeclaration or EnumDeclaration or FunctionDeclaration or EntryPoint;

	/// <inheritdoc/>
	/// <remarks>
	/// A namespace is a package, and the package clause is part of the file's header — so what is
	/// left here is the members, written where they stand.
	/// <para>
	/// A dotted name is not nested packages. A Go package is named for the one directory holding its
	/// files, so <c>geo.shapes</c> is a file in <c>geo/shapes</c> called <c>shapes</c>, and the rest
	/// of the path is where the file goes rather than anything written in it. That is worth a note,
	/// because it is the one part of the name a generated file cannot carry.
	/// </para>
	/// </remarks>
	protected override void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);

		IReadOnlyList<string> path = NamespaceDeclaration.Split(namespaceDecl.Name);
		if (path.Count > 1)
		{
			WriteInexpressible(
				code,
				$"{string.Join("/", path)}: a Go package is named for one directory, so the rest of the path is where this file goes");

			// A comment against a declaration is that declaration's documentation in Go, so this one
			// needs air under it or it becomes what the first member says about itself.
			code.NewLine();
		}

		WriteMembers(namespaceDecl.Members, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A struct for the data, and the behaviour beside it rather than inside it — which is the split
	/// Rust insists on and Go simply does, since a method is declared at package scope with a
	/// receiver and nothing gathers them.
	/// <para>
	/// A static member has no receiver and so nothing to scope its name, which is the one place a
	/// declaration is renamed: the type's name becomes part of it, the way C writes
	/// <c>Point_zero</c>, spelled as Go spells a name. That applies to a static field too, which
	/// becomes a package-level <c>var</c>, because Go has no static data member and a note in place
	/// of the table would lose it.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		string name = classDecl.Name ?? UnnamedType;

		// A type declared inside another is written beside it: Go nests nothing but a function.
		foreach (AstNode nested in classDecl.Members.Where(IsTypeDeclaration))
		{
			GenerateInternal(nested, code);
			code.NewLine();
		}

		if (classDecl.IsSpecialisation)
		{
			GenerateDocumentation(classDecl, code);
			WriteInexpressible(
				code,
				$"{name} for {string.Join(", ", classDecl.SpecialisationArguments.Select(SpellType))}: "
					+ "Go attaches a method only in the package declaring its type, so there is nowhere to put this");
			return;
		}

		if (classDecl.Kind == TypeDeclarationKind.Interface)
		{
			GenerateInterface(classDecl, name, code);
			return;
		}

		GenerateStruct(classDecl, name, code);
		WriteInterfaceAssertions(classDecl, name, code);

		foreach (FieldDeclaration field in classDecl.Members.OfType<FieldDeclaration>().Where(field => field.IsStatic))
		{
			code.NewLine();
			WriteStorage(Join(name, field.Name ?? UnnamedMember), field, code);
		}

		foreach (FunctionDeclaration function in classDecl.Members.OfType<FunctionDeclaration>())
		{
			code.NewLine();
			GenerateFunction(function, code, name);
		}
	}

	/// <summary>
	/// Asserts, at compile time, that a type implements what it said it implements.
	/// </summary>
	/// <param name="classDecl">The declaration to emit the assertions for.</param>
	/// <param name="name">The name the type is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Go satisfies an interface structurally: a type implements one by having its methods, and
	/// never says so. That leaves a declaration that meant to implement something with nothing in
	/// the file to show for it, and nothing to fail when a method is renamed out from under it.
	/// <para>
	/// <c>var _ Contract = (*Type)(nil)</c> is the language's own answer, and it is a check rather
	/// than a comment: the file stops compiling when the type stops implementing the interface,
	/// which is the same trade the C++ projection of a relationship makes. The pointer form is the
	/// one that always holds -- a method declared on the pointer receiver is not in the value's
	/// method set, and one declared on the value is in both.
	/// </para>
	/// </remarks>
	private static void WriteInterfaceAssertions(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		if (classDecl.Interfaces.Count == 0)
		{
			return;
		}

		code.NewLine();

		foreach (TypeReference contract in classDecl.Interfaces)
		{
			code.WriteLine($"var _ {SpellType(contract)} = (*{name})(nil)");
		}
	}

	/// <summary>
	/// Reports whether a member declares a type rather than data or behaviour.
	/// </summary>
	/// <param name="member">The member to test.</param>
	/// <returns>True when it declares a type.</returns>
	private static bool IsTypeDeclaration(AstNode member) =>
		member is ClassDeclaration or EnumDeclaration or UsingAlias;

	/// <summary>
	/// Writes the data half of a type declaration.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A base type is an embedded field: a field with a type and no name of its own, whose members
	/// are reached through the outer value as though they were its. That is the nearest thing to
	/// inheritance in the language, and it is near enough to be worth saying which it is — what it
	/// does not give is a derived value standing in for a base one, which interfaces do instead.
	/// <para>
	/// A field's initial value is not written, and that is not a gap: a Go value that names none of
	/// its fields starts every one of them at that type's zero, so what a field starts at is the
	/// language's answer rather than the declaration's.
	/// </para>
	/// </remarks>
	private void GenerateStruct(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		GenerateDocumentation(classDecl, code);
		WriteTypePromises(classDecl, code);
		WriteExportNote(name, classDecl.Visibility, code);

		List<AlignedLine> fields = [.. StructFields(classDecl)];

		if (fields.Count == 0)
		{
			code.WriteLine($"type {name} struct{{}}");
			return;
		}

		code.Write($"type {name} struct ");

		using Scope body = new(code);
		WriteAligned(fields, code);
	}

	/// <summary>
	/// Gives the fields a struct declares, in the order they are written.
	/// </summary>
	/// <param name="classDecl">The declaration being emitted.</param>
	/// <returns>One line per field.</returns>
	private IEnumerable<AlignedLine> StructFields(ClassDeclaration classDecl)
	{
		if (classDecl.BaseType is TypeReference baseType)
		{
			yield return new AlignedLine(
				[$"{CommentPrefix} the base, embedded: Go promotes an embedded type's members rather than deriving from it"],
				SpellType(baseType),
				string.Empty);
		}

		foreach (AstNode member in classDecl.Members)
		{
			switch (member)
			{
				case VariableDeclaration field:
					yield return Field(field.Name, field.Type, field.Visibility, []);
					break;

				case FieldDeclaration field when !field.IsStatic:
					yield return Field(field.Name, field.Type, field.Visibility, field.Documentation);
					break;

				default:
					break;
			}
		}
	}

	/// <summary>
	/// Builds the line one field of a struct is written on.
	/// </summary>
	/// <param name="name">The field's name.</param>
	/// <param name="type">The field's type.</param>
	/// <param name="visibility">What the declaration said about who may see it.</param>
	/// <param name="documentation">What the declaration says about itself.</param>
	/// <returns>The line.</returns>
	private AlignedLine Field(string? name, TypeReference? type, Visibility visibility, IEnumerable<string> documentation)
	{
		List<string> notes = [.. documentation.Select(DocumentationLine)];

		if (ExportNote(name, visibility) is string note)
		{
			notes.Add($"{CommentPrefix} {note}");
		}

		return new AlignedLine(notes, name ?? UnnamedMember, SpellType(type ?? new TypeReference(UnknownTypeName)));
	}

	/// <summary>
	/// Writes an interface as the interface it is.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The one mapping here that needs no explaining, and the one thing Go does that no other target
	/// does at all: an interface is satisfied by any type with the methods, which never has to say so
	/// and need not have been written when the interface was. A base type is an embedded interface,
	/// which requires everything it requires — the same thing Rust spells as a supertrait.
	/// </remarks>
	private void GenerateInterface(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		GenerateDocumentation(classDecl, code);
		WriteTypePromises(classDecl, code);
		WriteExportNote(name, classDecl.Visibility, code);

		List<AstNode> members = [.. classDecl.Members.Where(member => member is FunctionDeclaration or FieldDeclaration)];

		// An interface embedded in another is written as its bare name among the members, and means
		// every method of it. A base and an interface are the same thing at this end -- it is the
		// struct below where they part, Go having no inheritance for one and structural
		// satisfaction for the other.
		string[] embedded =
		[
			.. classDecl.BaseType is TypeReference baseType ? (string[])[SpellType(baseType)] : [],
			.. classDecl.Interfaces.Select(SpellType),
		];

		if (embedded.Length == 0 && members.Count == 0)
		{
			code.WriteLine($"type {name} interface{{}}");
			return;
		}

		code.Write($"type {name} interface ");

		using Scope body = new(code);

		foreach (string contract in embedded)
		{
			code.WriteLine(contract);
		}

		insideInterface = true;

		foreach (AstNode member in members)
		{
			if (member is FunctionDeclaration function)
			{
				GenerateFunction(function, code, name);
				continue;
			}

			GenerateInternal(member, code);
		}

		insideInterface = false;
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code) =>
		GenerateFunction(funcDecl, code, null);

	/// <summary>
	/// Emits a function, which may have been declared as a member of a type.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <remarks>
	/// A member takes a receiver, and which one it takes is what
	/// <see cref="FunctionDeclaration.IsReadOnly"/> decides: a member that promises not to modify
	/// what it is called on takes the value and one that does takes a pointer to it. Go makes that
	/// promise by the shape of the declaration rather than with a keyword, and it is a real one —
	/// a value receiver is a copy, so a method that took one cannot change the caller's value even
	/// by mistake.
	/// <para>
	/// Nothing is written for <see cref="FunctionDeclaration.IsPure"/>,
	/// <see cref="FunctionDeclaration.MustUseResult"/>,
	/// <see cref="FunctionDeclaration.IsVirtual"/>,
	/// <see cref="FunctionDeclaration.IsCompileTimeEvaluable"/>,
	/// <see cref="FunctionDeclaration.IsNoThrow"/>, <see cref="FunctionDeclaration.IsExplicit"/> or
	/// <see cref="FunctionDeclaration.IsFriend"/>. Go has no attribute for a call worth looking at,
	/// no virtual dispatch outside an interface, no compile-time evaluation, no exceptions, no
	/// converting constructors and no friends — which is a list of things left out rather than of
	/// things missed.
	/// </para>
	/// </remarks>
	private void GenerateFunction(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);

		string name = SpellFunctionName(funcDecl, enclosingType);

		// A deleted declaration exists to make a call illegal, and Go has no way to say that of one
		// member. Writing the signature would do the opposite of what it asks for.
		if (funcDecl.Definition == FunctionDefinition.Deleted)
		{
			WriteInexpressible(code, $"{name} is deleted: Go cannot refuse a call");
			return;
		}

		// A defaulted declaration is one the language supplies. Go supplies it to every type at once,
		// as the zero value, rather than to a type that asks.
		if (funcDecl.Definition == FunctionDefinition.Defaulted)
		{
			WriteInexpressible(code, $"{name} is defaulted: Go gives every type a zero value instead");
			return;
		}

		if (insideInterface)
		{
			WriteSignature(funcDecl, name, code, enclosingType);
			code.WriteLine();
			return;
		}

		// A declaration with no definition is a requirement, which only an interface can hold: there
		// is nowhere on a struct for one to be implemented.
		if (funcDecl.IsAbstract)
		{
			WriteInexpressible(code, $"{name} has no body: only an interface may require one");
			return;
		}

		WriteExportNote(name, funcDecl.Visibility, code);

		code.Write("func ");
		WriteReceiver(funcDecl, enclosingType, code);
		WriteSignature(funcDecl, name, code, enclosingType);

		// The line is left open, so the scope's brace lands on it: Go braces hang, and gofmt will not
		// have them anywhere else.
		code.Write(" ");

		using Scope body = new(code);

		if (funcDecl.Kind == FunctionKind.Constructor)
		{
			WriteConstructorBody(funcDecl, enclosingType, code);
			return;
		}

		WriteBody(funcDecl.Body, code);
	}

	/// <summary>
	/// Writes a function's name, its parameters and what it answers with.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	private void WriteSignature(FunctionDeclaration funcDecl, string name, CodeBlocker code, string? enclosingType)
	{
		code.Write($"{name}(");
		GenerateParameterList(funcDecl.Parameters, code);
		code.Write(")");

		if (SpellResult(funcDecl, enclosingType) is string result && result.Length > 0)
		{
			code.Write($" {result}");
		}
	}

	/// <summary>
	/// Spells what a function answers with, or nothing when it answers nothing.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The result type, or an empty string.</returns>
	/// <remarks>
	/// A constructor answers the type it builds, which the declaration does not carry — it is named
	/// after the type rather than typed by it, the same as everywhere else in the AST.
	/// </remarks>
	private static string SpellResult(FunctionDeclaration funcDecl, string? enclosingType) =>
		funcDecl.Kind == FunctionKind.Constructor && enclosingType is not null
			? enclosingType
			: SpellType(funcDecl.ReturnType ?? new TypeReference("void"));

	/// <summary>
	/// Writes the instance a method is called on, when it is called on one.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <param name="code">The writer to emit into.</param>
	private static void WriteReceiver(FunctionDeclaration funcDecl, string? enclosingType, CodeBlocker code)
	{
		if (!TakesReceiver(funcDecl, enclosingType))
		{
			return;
		}

		code.Write($"({ReceiverName} {(funcDecl.IsReadOnly ? string.Empty : "*")}{enclosingType}) ");
	}

	/// <summary>
	/// Reports whether a declaration is called on an instance.
	/// </summary>
	/// <param name="funcDecl">The declaration to test.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>True when it takes a receiver.</returns>
	private static bool TakesReceiver(FunctionDeclaration funcDecl, string? enclosingType) =>
		enclosingType is not null && !funcDecl.IsStatic && funcDecl.Kind != FunctionKind.Constructor;

	/// <summary>
	/// Spells the name a declaration is written under.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The name as Go writes it.</returns>
	/// <remarks>
	/// A constructor is <c>New<i>Type</i></c>, which is a convention rather than a keyword — Go has
	/// no constructors, and a function answering the type is what every package writes instead.
	/// <para>
	/// A member with no receiver has nothing to scope its name, so the type's name becomes part of
	/// it. That is a rename, which this generator otherwise refuses to do, and it is forced: there is
	/// no <c>Point.zero</c> in Go for a reference to have named in the first place.
	/// </para>
	/// </remarks>
	private static string SpellFunctionName(FunctionDeclaration funcDecl, string? enclosingType)
	{
		string bare = funcDecl.Kind switch
		{
			FunctionKind.Constructor => $"New{enclosingType ?? UnnamedType}",
			FunctionKind.Destructor => CloseName,
			FunctionKind.Operator => OperatorName(funcDecl),
			FunctionKind.ConversionOperator => ConversionName(funcDecl.ReturnType),
			_ => funcDecl.Name ?? UnnamedMember,
		};

		return funcDecl.Kind != FunctionKind.Constructor
			&& enclosingType is not null
			&& !TakesReceiver(funcDecl, enclosingType)
			? Join(enclosingType, bare)
			: bare;
	}

	/// <summary>
	/// Names an operator, which Go cannot overload and so must call something.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <returns>The method name.</returns>
	/// <remarks>
	/// A declaration with no operand beside the instance is the unary reading of its symbol, which is
	/// the only thing that tells <c>-a</c> from <c>a - b</c>.
	/// </remarks>
	private static string OperatorName(FunctionDeclaration funcDecl)
	{
		string symbol = funcDecl.Name ?? string.Empty;

		if (funcDecl.Parameters.Count == 0 && UnaryOperatorNames.TryGetValue(symbol, out string? unary))
		{
			return unary;
		}

		return OperatorNames.TryGetValue(symbol, out string? word) ? word : $"Operator{Identifier(symbol)}";
	}

	/// <summary>
	/// Names a conversion, which Go cannot declare and so writes as a method.
	/// </summary>
	/// <param name="target">The type being converted to.</param>
	/// <returns>The method name.</returns>
	/// <remarks>
	/// A conversion to a string is <c>String</c>, which is not a naming convention but the method
	/// <c>fmt.Stringer</c> asks for: a type that has it is printed with it by everything in the
	/// standard library that prints anything. Every other conversion is <c>To<i>Type</i></c>, which
	/// is only a name.
	/// </remarks>
	private static string ConversionName(TypeReference? target)
	{
		string spelled = SpellType(target ?? new TypeReference(UnknownTypeName));
		return string.Equals(spelled, StringTypeName, StringComparison.Ordinal)
			? "String"
			: $"To{Identifier(spelled)}";
	}

	/// <summary>
	/// Joins a type's name to a member's, as Go joins the words of a name.
	/// </summary>
	/// <param name="type">The type's name.</param>
	/// <param name="name">The member's name.</param>
	/// <returns>The joined name.</returns>
	private static string Join(string type, string name) =>
		name.Length == 0 ? type : $"{type}{char.ToUpperInvariant(name[0])}{name[1..]}";

	/// <summary>
	/// Keeps what Go will accept in an identifier, and capitalises what is left.
	/// </summary>
	/// <param name="text">The text to fold.</param>
	/// <returns>The text as a word of a name.</returns>
	private static string Identifier(string text)
	{
		string kept = string.Concat(text.Where(character => char.IsLetterOrDigit(character) || character == '_'));
		return kept.Length == 0 ? string.Empty : $"{char.ToUpperInvariant(kept[0])}{kept[1..]}";
	}

	/// <summary>
	/// Writes what a constructor builds.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The type being built.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A Go value is built by a composite literal naming the fields it does not want the zero of, so
	/// the initialiser list is the whole of the constructor rather than a preamble to it. An
	/// initialiser list with nothing in it needs no fallback: the literal with no fields is the zero
	/// value, which every Go type has and which is exactly what a constructor with nothing to build
	/// from was asking for.
	/// </remarks>
	private void WriteConstructorBody(FunctionDeclaration funcDecl, string? enclosingType, CodeBlocker code)
	{
		WriteBody(funcDecl.Body, code);

		ConstructionExpression value = new(new TypeReference(enclosingType ?? UnnamedType));
		foreach (MemberInitialiser initialiser in funcDecl.Initialisers)
		{
			value.Arguments.Add(initialiser);
		}

		code.Write("return ");
		GenerateConstructionExpression(value, code);
		EndStatement(code);
	}

	/// <summary>
	/// Writes a function's statements.
	/// </summary>
	/// <param name="statements">The statements to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteBody(IEnumerable<AstNode> statements, CodeBlocker code)
	{
		foreach (AstNode statement in statements)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A parameter's default value is written beside it as a comment. Go has no default arguments,
	/// so the caller has to pass one — and the value the declaration chose is exactly what they need
	/// in order to pass the same thing.
	/// </remarks>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write($"{parameter.Name ?? $"param{position}"} ");
		code.Write(SpellType(parameter.Type ?? new TypeReference(UnknownTypeName)));

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" /* = {parameter.DefaultValue} */");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>:=</c> where the declaration says its type is to be inferred and gives a value to infer it
	/// from, and <c>var</c> otherwise. A <c>var</c> with no value is not an omission: Go starts it at
	/// its type's zero, which is the whole of what the declaration said.
	/// <para>
	/// <see cref="VariableDeclaration.IsConstant"/> is a <c>const</c> only where Go can hold one.
	/// A Go constant is a number, a string or a boolean the compiler worked out — never a struct, a
	/// slice or anything built while running — so a declaration that starts at one of those is a
	/// <c>var</c> with a note, rather than a <c>const</c> the compiler refuses.
	/// </para>
	/// </remarks>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		bool inferred = varDecl.IsTypeInferred || varDecl.Type is null;

		// A declaration that says its type is the third statement a conditional can be lowered into,
		// and the only one that knows what the branches are: `var x T` first, then the choice
		// assigning to it. Without this the expression form would have to name a type nobody gave it.
		if (!inferred && varDecl.InitialValue is ConditionalExpression chosen)
		{
			code.Write($"var {varDecl.Name} {SpellType(varDecl.Type!)}");
			EndStatement(code);

			WriteChoice(chosen, code, bothArms: true, branch =>
			{
				code.Write($"{varDecl.Name} = ");
				GenerateInternal(branch, code);
				EndStatement(code);
			});

			return;
		}

		// The short form declares a variable, so it is not open to a constant — which is the one
		// reason the decision has to be made before the keyword is written rather than with it.
		if (inferred && varDecl.InitialValue is not null && !IsConstant(varDecl.IsConstant, varDecl.InitialValue))
		{
			code.Write($"{varDecl.Name} := ");
			GenerateInternal(varDecl.InitialValue, code);
			EndStatement(code);
			return;
		}

		WriteStorageKeyword(varDecl.Name, varDecl.IsConstant, varDecl.InitialValue, code);
		code.Write(varDecl.Name);

		TypeReference type = varDecl.Type ?? new TypeReference(UnknownTypeName);

		if (!inferred || varDecl.InitialValue is null)
		{
			code.Write($" {SpellType(type)}");
		}

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			WriteInitialValue(type, varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A package-level <c>var</c> or <c>const</c>. A field of a struct never reaches here: the struct
	/// writes its own, so that their columns line up.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		if (insideInterface)
		{
			GenerateDocumentation(field, code);
			WriteInexpressible(code, $"{field.Name}: a Go interface holds methods, so a constant belongs to what implements it");
			return;
		}

		WriteStorage(field.Name ?? UnnamedMember, field, code);
	}

	/// <summary>
	/// Writes a field as the package-level declaration Go holds one in.
	/// </summary>
	/// <param name="name">The name it is written under.</param>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteStorage(string name, FieldDeclaration field, CodeBlocker code)
	{
		GenerateDocumentation(field, code);
		WriteExportNote(name, field.Visibility, code);

		TypeReference type = field.Type ?? new TypeReference(UnknownTypeName);

		WriteStorageKeyword(name, field.IsConstant, field.InitialValue, code);
		code.Write($"{name} {SpellType(type)}");

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			WriteInitialValue(type, field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Writes <c>const</c> or <c>var</c>, saying so first where the declaration asked for the one Go
	/// cannot give.
	/// </summary>
	/// <param name="name">The name being declared.</param>
	/// <param name="wanted">Whether the declaration says its value never changes.</param>
	/// <param name="value">What it starts at.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteStorageKeyword(string? name, bool wanted, AstNode? value, CodeBlocker code)
	{
		bool constant = IsConstant(wanted, value);

		if (wanted && !constant)
		{
			WriteInexpressible(code, $"{name} is constant: a Go const is a number, a string or a bool, so this is a var");
		}

		code.Write(constant ? "const " : "var ");
	}

	/// <summary>
	/// Reports whether a declaration is written as a <c>const</c>.
	/// </summary>
	/// <param name="wanted">Whether the declaration says its value never changes.</param>
	/// <param name="value">What it starts at.</param>
	/// <returns>True when Go will hold it as a constant.</returns>
	private static bool IsConstant(bool wanted, AstNode? value) => wanted && IsCompileTimeValue(value);

	/// <summary>
	/// Reports whether a value is one Go will let a <c>const</c> hold.
	/// </summary>
	/// <param name="value">The value to test.</param>
	/// <returns>True when it is.</returns>
	/// <remarks>
	/// A literal, and nothing else. Go's constants are the untyped ones the compiler evaluates, which
	/// rules out every value with a field or an element in it however fixed its contents are.
	/// </remarks>
	private static bool IsCompileTimeValue(AstNode? value) =>
		value is LiteralExpression<string>
			or LiteralExpression<int>
			or LiteralExpression<bool>
			or LiteralExpression<double>
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>;

	/// <summary>
	/// Writes what a declaration starts at, giving a bare list the declaration's own type.
	/// </summary>
	/// <param name="declared">The declared type.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// This is the one place Go asks for the opposite of what C does. A composite literal carries its
	/// type, and the only place an untyped one is allowed is inside another — so a list standing as
	/// an initialiser is given the type the declaration already said, where C had to take one away.
	/// </remarks>
	private void WriteInitialValue(TypeReference declared, AstNode value, CodeBlocker code)
	{
		if (value is ConstructionExpression { Type: null })
		{
			code.Write(SpellType(declared));
		}

		GenerateInternal(value, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A named type and a block of constants, which is what Go has in place of an enumeration. The
	/// constants are at package scope, so each is prefixed with the type's name the way C's are and
	/// for the same reason: two enumerations with a <c>None</c> each would otherwise be one
	/// redeclaration.
	/// <para>
	/// A member with no value of its own is <c>iota</c> while nothing has interrupted the count, and
	/// the one before it plus one once something has. That is not a flourish: Go continues a
	/// <c>const</c> block by repeating the previous line's expression, which is right while the
	/// expression mentions <c>iota</c> and gives every later member the same value once one has said
	/// a number. Naming the previous constant says what C says — that an unvalued member is the one
	/// before it plus one — in the one way that stays true.
	/// </para>
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		string name = enumDecl.Name ?? UnnamedType;

		GenerateDocumentation(enumDecl, code);
		WriteExportNote(name, enumDecl.Visibility, code);
		code.WriteLine($"type {name} {SpellType(enumDecl.UnderlyingType ?? new TypeReference("int"))}");

		if (enumDecl.Members.Count == 0)
		{
			return;
		}

		code.NewLine();
		code.Write("const ");

		using ParenScope block = new(code);
		WriteAligned([.. EnumMembers(enumDecl, name)], code);
	}

	/// <summary>
	/// Gives the constants an enumeration declares, in the order they are written.
	/// </summary>
	/// <param name="enumDecl">The declaration being emitted.</param>
	/// <param name="name">The type's name.</param>
	/// <returns>One line per member.</returns>
	private static IEnumerable<AlignedLine> EnumMembers(EnumDeclaration enumDecl, string name)
	{
		string? previous = null;
		bool counting = true;

		foreach (EnumMember member in enumDecl.Members)
		{
			string constant = Prefixed(name, member.Name ?? UnnamedMember);
			string value;

			if (member.Value is not null)
			{
				value = $"{name} = {member.Value}";
				counting = false;
			}
			else if (previous is null)
			{
				value = $"{name} = iota";
			}
			else
			{
				// While iota is still counting, saying nothing is what continues it.
				value = counting ? string.Empty : $"{name} = {previous} + 1";
			}

			yield return new AlignedLine([], constant, value);
			previous = constant;
		}
	}

	/// <summary>
	/// Gives a constant the enumeration's name, unless it already carries it.
	/// </summary>
	/// <param name="name">The enumeration's name.</param>
	/// <param name="member">The member's name.</param>
	/// <returns>The constant's name.</returns>
	/// <remarks>
	/// The one thing already prefixed is a member somebody prefixed by hand, and prefixing it again
	/// would give them a <c>ColourColourRed</c> for having anticipated this.
	/// </remarks>
	private static string Prefixed(string name, string member) =>
		member.StartsWith(name, StringComparison.Ordinal) ? member : Join(name, member);

	/// <inheritdoc/>
	/// <remarks>
	/// A type alias, which is what <c>=</c> makes it: <c>type Origin = Point</c> is a second name for
	/// one type, where <c>type Origin Point</c> would be a second type with the same shape.
	/// </remarks>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		WriteExportNote(usingAlias.Name, usingAlias.Visibility, code);
		code.Write($"type {usingAlias.Name} = {SpellType(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName))}");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Go has no <c>static_assert</c>, and it has something that works as one. The keys of a map
	/// literal must be distinct, and a key that is a constant is checked while compiling — so a
	/// literal holding <c>false</c> and the condition holds two keys when the condition is true and
	/// the same key twice when it is false, which is a compile error naming the duplicate.
	/// <para>
	/// It needs no import, no build tag and no generics, and it fails at the line that made the
	/// promise. What it does need is a condition Go can evaluate while compiling, which is what a
	/// compile-time assertion means everywhere.
	/// </para>
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		if (assertion.Message is string message)
		{
			code.WriteLine($"{CommentPrefix} {message}");
		}

		code.WriteLine($"{CommentPrefix} A false condition repeats the false key, which Go refuses to compile.");
		code.Write($"var _ = map[bool]struct{{}}{{false: {{}}, {assertion.Condition ?? "false"}: {{}}}}");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Go is the one target here with no conditional expression at all. The C family has <c>?:</c>,
	/// Python spells the same thing with its operands reordered, and Rust makes <c>if</c> an
	/// expression — Go's <c>if</c> is a statement and yields nothing, so there is nothing to spell
	/// this as in the place it stands.
	/// <para>
	/// So it is lowered to the statement around it, which is what anybody writing Go by hand does:
	/// <c>if cond { return a }</c> followed by <c>return b</c>. That is exact rather than merely
	/// close — only the branch taken is evaluated, the same as a ternary — and, unlike every
	/// expression form Go has, it needs nobody to name the branches' type.
	/// </para>
	/// </remarks>
	protected override void GenerateReturnStatement(ReturnStatement returnStmt, CodeBlocker code)
	{
		Ensure.NotNull(returnStmt);
		Ensure.NotNull(code);

		if (returnStmt.Expression is not ConditionalExpression conditional)
		{
			base.GenerateReturnStatement(returnStmt, code);
			return;
		}

		// One arm: a return leaves the statement, so the second branch is what follows rather than
		// what an else holds.
		WriteChoice(conditional, code, bothArms: false, branch =>
		{
			code.Write("return ");
			GenerateInternal(branch, code);
			EndStatement(code);
		});
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The same lowering <see cref="GenerateReturnStatement"/> does, in the other statement that can
	/// hold a choice between two values. Here it needs both arms, since falling through would leave
	/// the target holding what it held before rather than the other branch.
	/// </remarks>
	protected override void GenerateAssignmentStatement(AssignmentStatement assignment, CodeBlocker code)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(code);

		if (assignment.Value is not ConditionalExpression conditional)
		{
			base.GenerateAssignmentStatement(assignment, code);
			return;
		}

		WriteChoice(conditional, code, bothArms: true, branch =>
		{
			GenerateInternal(assignment.Target, code);
			code.Write($" {GetAssignmentOperator(assignment.Operator)} ");
			GenerateInternal(branch, code);
			EndStatement(code);
		});
	}

	/// <summary>
	/// Writes a conditional as the <c>if</c> Go has in place of it.
	/// </summary>
	/// <param name="conditional">The expression being lowered.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="bothArms">
	/// Whether the second branch needs an <c>else</c> to hold it, or is simply what comes next —
	/// which it is when the first branch left the statement.
	/// </param>
	/// <param name="writeArm">How to write one branch once it has been chosen.</param>
	/// <remarks>
	/// The braces are written out rather than opened as a scope because of Go's semicolon insertion:
	/// a line break between <c>}</c> and <c>else</c> ends the statement, so the two have to share a
	/// line and nothing that writes a closing brace on its own can be used.
	/// </remarks>
	private void WriteChoice(
		ConditionalExpression conditional,
		CodeBlocker code,
		bool bothArms,
		Action<AstNode> writeArm)
	{
		code.Write("if ");
		WriteCondition(conditional.Condition, code);
		code.WriteLine(" {");

		code.Indent();
		writeArm(conditional.WhenTrue);
		code.Outdent();

		if (!bothArms)
		{
			code.WriteLine("}");
			writeArm(conditional.WhenFalse);
			return;
		}

		code.WriteLine("} else {");
		code.Indent();
		writeArm(conditional.WhenFalse);
		code.Outdent();
		code.WriteLine("}");
	}

	/// <summary>
	/// Writes the expression an <c>if</c> tests, without the parentheses every other position keeps.
	/// </summary>
	/// <param name="condition">The expression to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The AST carries no operator precedence, so an operator applied to operands is parenthesised
	/// wherever it stands — and <c>gofmt</c> removes exactly those parentheses from the clause of an
	/// <c>if</c>, a <c>for</c> or a <c>switch</c> and nowhere else. This is the one place this
	/// generator writes such a clause, so it is the one place the parentheses are left off.
	/// <para>
	/// Only the outermost pair: <c>gofmt</c> takes no view on the ones inside, which are what make
	/// the expression unambiguous in the first place.
	/// </para>
	/// </remarks>
	private void WriteCondition(AstNode condition, CodeBlocker code)
	{
		switch (condition)
		{
			case BinaryExpression binary:
				GenerateInternal(binary.Left, code);
				code.Write($" {GetOperatorSpelling(binary.Operator)} ");
				GenerateInternal(binary.Right, code);
				return;

			case UnaryExpression unary:
				code.Write(GetUnaryOperatorSpelling(unary.Operator));
				GenerateInternal(unary.Operand, code);
				return;

			default:
				GenerateInternal(condition, code);
				return;
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Where the statement around it could not take the lowering — nested inside another expression,
	/// or passed as an argument — what is left is a function literal called where it stands, which is
	/// the only expression Go has that can choose. It has to name what it answers with and the AST
	/// does not say, so the type is read off whichever branch says what it is, and is <c>any</c> when
	/// neither does — which is what this generator writes wherever a type was never given.
	/// </remarks>
	protected override void GenerateConditionalExpression(ConditionalExpression conditional, CodeBlocker code)
	{
		Ensure.NotNull(conditional);
		Ensure.NotNull(code);

		code.WriteLine($"func() {BranchType(conditional)} {{");
		code.Indent();

		WriteChoice(conditional, code, bothArms: false, branch =>
		{
			code.Write("return ");
			GenerateInternal(branch, code);
			EndStatement(code);
		});

		code.Outdent();
		code.Write("}()");
	}

	/// <summary>
	/// Spells what a conditional answers with, which Go makes the caller name.
	/// </summary>
	/// <param name="conditional">The expression being written.</param>
	/// <returns>The type as Go writes it.</returns>
	private static string BranchType(ConditionalExpression conditional) =>
		TypeOfValue(conditional.WhenTrue)
			?? TypeOfValue(conditional.WhenFalse)
			?? TypeMappings[UnknownTypeName];

	/// <summary>
	/// Reads a type off a value that says what it is.
	/// </summary>
	/// <param name="value">The value to read.</param>
	/// <returns>The type as Go writes it, or null where the value does not say.</returns>
	private static string? TypeOfValue(AstNode value) => value switch
	{
		LiteralExpression<string> or AstLeafNode<string> => StringTypeName,
		LiteralExpression<int> or AstLeafNode<int> => "int",
		LiteralExpression<bool> or AstLeafNode<bool> => "bool",
		LiteralExpression<double> => "float64",
		ConstructionExpression { Type: not null } built => SpellType(built.Type),
		_ => null,
	};

	/// <inheritdoc/>
	/// <remarks>
	/// Three shapes, and which one is written depends on what the expression is rather than on where
	/// it stands: a construction naming its members is a composite literal, one with no type at all
	/// is a composite literal whose type the declaration around it supplies, and one that names
	/// neither is a call — which in Go is a conversion when it takes one argument, since that is
	/// what <c>int64(n)</c> is.
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Type is null)
		{
			WriteElementList(construction, code, "{", "}", "{}");
			return;
		}

		string type = SpellType(construction.Type);

		if (construction.Arguments.Count == 0 || construction.Arguments.Any(argument => argument is MemberInitialiser))
		{
			code.Write(type);
			WriteElementList(construction, code, "{", "}", "{}");
			return;
		}

		code.Write($"{type}(");

		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			GenerateInternal(construction.Arguments[index], code);
		}

		code.Write(")");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Go's <c>main</c> takes no arguments and returns nothing, so a program that wants either
	/// reaches for <c>os</c>: the arguments are <c>os.Args</c>, and an exit code is handed to
	/// <c>os.Exit</c>.
	/// <para>
	/// A program that returns an exit code is written as a <c>run</c> answering one and a
	/// <c>main</c> exiting with what it answered — the same shape Python's <c>__main__</c> guard
	/// takes here, and for the same reason: it is what keeps the body's own <c>return</c> meaning
	/// what it says. It is also what Go's own documentation recommends, because <c>os.Exit</c> runs
	/// no deferred call and a <c>run</c> is the place to put them.
	/// </para>
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		string parameters = entryPoint.AcceptsArguments ? "args []string" : string.Empty;
		string arguments = entryPoint.AcceptsArguments ? "os.Args" : string.Empty;

		if (entryPoint.ReturnsExitCode)
		{
			code.Write($"func run({parameters}) int ");

			using (Scope run = new(code))
			{
				WriteBody(entryPoint.Body, code);
			}

			code.NewLine();
			code.Write("func main() ");

			using Scope main = new(code);
			code.WriteLine($"os.Exit(run({arguments}))");
			return;
		}

		code.Write("func main() ");

		using Scope body = new(code);

		if (entryPoint.AcceptsArguments)
		{
			code.WriteLine("args := os.Args");
		}

		WriteBody(entryPoint.Body, code);
	}

	/// <summary>
	/// Writes the note a declaration earns when its name disagrees with the visibility it asked for.
	/// </summary>
	/// <param name="name">The name being declared.</param>
	/// <param name="visibility">What the declaration said about who may see it.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteExportNote(string? name, Visibility visibility, CodeBlocker code)
	{
		if (ExportNote(name, visibility) is string note)
		{
			WriteInexpressible(code, note);
		}
	}

	/// <summary>
	/// Says what a declaration asked for, where Go's only way of saying it is the name it was given.
	/// </summary>
	/// <param name="name">The name being declared.</param>
	/// <param name="visibility">What the declaration said about who may see it.</param>
	/// <returns>The note, or null when the name already says it.</returns>
	/// <remarks>
	/// Go exports a name whose first letter is a capital and nothing else, so a declaration's
	/// visibility is not something a generator can write beside it — only something it can rename it
	/// into. Renaming would leave every reference to the old name behind, which is why this says so
	/// instead, and why it says nothing at all where the name and the declaration already agree.
	/// </remarks>
	private static string? ExportNote(string? name, Visibility visibility)
	{
		if (visibility == Visibility.Unspecified || string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
		{
			return null;
		}

		bool exported = char.IsUpper(name[0]);
		if (exported == (visibility == Visibility.Public))
		{
			return null;
		}

		string said = exported ? "exported" : "unexported";
		return $"{name} is {visibility.ToString().ToLowerInvariant()}: in Go that is the case of the first letter, so the name says {said} instead";
	}

	/// <summary>
	/// Writes one line of documentation as Go writes one.
	/// </summary>
	/// <param name="line">The line to write.</param>
	/// <returns>The comment line.</returns>
	private string DocumentationLine(string line) =>
		line.Length == 0 ? DocumentationPrefix : $"{DocumentationPrefix} {line}";

	/// <summary>
	/// One line of a block whose columns line up, and the comments belonging above it.
	/// </summary>
	/// <param name="Notes">What is written above the line.</param>
	/// <param name="Name">The first column.</param>
	/// <param name="Rest">The second column, or nothing where the line has only one.</param>
	private sealed record AlignedLine(IReadOnlyList<string> Notes, string Name, string Rest);

	/// <summary>
	/// Writes a block of lines with their second columns lined up.
	/// </summary>
	/// <param name="lines">The lines to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// What <c>gofmt</c> does to a struct's fields and to a constant block, reproduced rather than
	/// left for it to do: a generated file nobody has run the formatter over should already be what
	/// the formatter would write.
	/// <para>
	/// The rule is <c>gofmt</c>'s own, including its two edges. A line with one column — an embedded
	/// field — takes no part in the width, and a comment between two lines does not break the block,
	/// which is why the notes belong to the line rather than being written before the block.
	/// </para>
	/// </remarks>
	private static void WriteAligned(IReadOnlyList<AlignedLine> lines, CodeBlocker code)
	{
		int width = lines
			.Where(line => line.Rest.Length > 0)
			.Select(line => line.Name.Length)
			.DefaultIfEmpty(0)
			.Max();

		foreach (AlignedLine line in lines)
		{
			foreach (string note in line.Notes)
			{
				code.WriteLine(note);
			}

			code.WriteLine(line.Rest.Length == 0 ? line.Name : $"{line.Name.PadRight(width)} {line.Rest}");
		}
	}

	/// <summary>
	/// Spells a type in Go.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The Go source for it.</returns>
	/// <remarks>
	/// Go has one indirection and it is the pointer: there are no references, and nothing a type can
	/// be that answers <see cref="TypeReference.IsReadOnly"/>. What is left of
	/// <see cref="TypeIndirection.Reference"/> is "reached rather than copied", which is what a
	/// pointer is — except where the type is already a view of something else. A string, a slice and
	/// a map each hold a pointer already, so a pointer to one is a pointer to a pointer and nobody
	/// means that.
	/// </remarks>
	private static string SpellType(TypeReference type)
	{
		string core = SpellCoreType(type);

		return type.Indirection switch
		{
			TypeIndirection.Pointer => $"*{core}",
			TypeIndirection.Reference => IsView(core) ? core : $"*{core}",
			_ => core,
		};
	}

	/// <summary>
	/// Reports whether a type is already a view of what it holds.
	/// </summary>
	/// <param name="spelled">The type as Go spells it.</param>
	/// <returns>True when a pointer to it would be a pointer to a pointer.</returns>
	private static bool IsView(string spelled) =>
		string.Equals(spelled, StringTypeName, StringComparison.Ordinal)
			|| spelled.StartsWith("[]", StringComparison.Ordinal)
			|| spelled.StartsWith("map[", StringComparison.Ordinal);

	/// <summary>
	/// Spells a type without saying how it is reached.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The value form.</returns>
	private static string SpellCoreType(TypeReference type)
	{
		string core = SpellTypeName(type);
		return type.IsArray ? $"[]{core}" : core;
	}

	/// <summary>
	/// Spells a type's name and its arguments.
	/// </summary>
	/// <param name="type">The type whose name to spell.</param>
	/// <returns>The name as Go writes it.</returns>
	/// <remarks>
	/// <c>list</c> and <c>dict</c> are the two names the AST has that Go spells out of its own
	/// grammar rather than from a package: a sequence is a slice and a mapping is a map, and neither
	/// is a type anybody imported. A container named without arguments is a container of the most
	/// general thing there is, which is what the caller left unsaid.
	/// <para>
	/// A type with arguments is written with square brackets, which is where Go put its generics and
	/// the one place its spelling of a familiar thing surprises a reader of the others.
	/// </para>
	/// </remarks>
	private static string SpellTypeName(TypeReference type)
	{
		string unknown = TypeMappings[UnknownTypeName];

		if (string.Equals(type.Name, "list", StringComparison.OrdinalIgnoreCase))
		{
			return $"[]{(type.TypeArguments.Count == 1 ? SpellType(type.TypeArguments[0]) : unknown)}";
		}

		if (string.Equals(type.Name, "dict", StringComparison.OrdinalIgnoreCase))
		{
			return type.TypeArguments.Count == 2
				? $"map[{SpellType(type.TypeArguments[0])}]{SpellType(type.TypeArguments[1])}"
				: $"map[{StringTypeName}]{unknown}";
		}

		string name = TypeMappings.TryGetValue(type.Name, out string? mapped) ? mapped : type.Name;

		return type.TypeArguments.Count == 0
			? name
			: $"{name}[{string.Join(", ", type.TypeArguments.Select(SpellType))}]";
	}
}
