// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Globalization;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates JavaScript code from AST nodes.
/// </summary>
/// <remarks>
/// JavaScript is dynamically typed, so the <see cref="Parameter.Type"/> and
/// <see cref="FunctionDeclaration.ReturnType"/> carried by the AST have no place in the output and
/// are deliberately dropped. Equality maps to the strict operators (<c>===</c>, <c>!==</c>) rather
/// than the coercing ones, because the AST's <see cref="BinaryOperator.Equal"/> means value equality
/// in every other generator and <c>==</c> would not.
/// </remarks>
public class JavaScriptGenerator : StandardLanguageGenerator
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "javascript";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "JavaScript";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "js";

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript's documentation convention is a <c>/** */</c> block, which is a shape the shared
	/// emitter's line-at-a-time form cannot write. A <c>//</c> comment carries the same lines to the
	/// same reader without pretending to be JSDoc, which a tool would then read and find no tags in.
	/// </remarks>
	protected override string DocumentationPrefix => "//";

	/// <inheritdoc/>
	protected override string? SpellImport(string import) => $"import \"{import}\";";

	/// <summary>
	/// Emits an enumeration declared inside a class, as a static member of it.
	/// </summary>
	/// <param name="enumDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A class body is not a block: <c>const</c> is a syntax error inside one, so the namespace-scope
	/// spelling cannot simply be nested. A static field is the same object reachable by the same
	/// name, which is what nesting was for.
	/// </remarks>
	private void GenerateNestedEnum(EnumDeclaration enumDecl, CodeBlocker code)
	{
		GenerateDocumentation(enumDecl, code);
		code.WriteLine($"static {enumDecl.Name ?? "UnnamedEnum"} = Object.freeze({{");

		using (IndentScope members = new(code))
		{
			WriteEnumMembers(enumDecl, code);
		}

		code.WriteLine("});");
	}

	/// <summary>
	/// Writes an enumeration's members as the properties of an object literal.
	/// </summary>
	/// <param name="enumDecl">The declaration whose members to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private static void WriteEnumMembers(EnumDeclaration enumDecl, CodeBlocker code)
	{
		for (int index = 0; index < enumDecl.Members.Count; index++)
		{
			EnumMember member = enumDecl.Members[index];
			string value = member.Value ?? index.ToString(CultureInfo.InvariantCulture);
			code.WriteLine($"{member.Name ?? "UNNAMED"}: {value},");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript has nothing that is checked before the program runs, so what was asserted is written as
	/// a comment. Dropping it would leave a file that looks like one still making the guarantee.
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		WriteInexpressible(code, $"asserted at build time: {assertion.Condition}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript has no types to alias. The name is bound to whatever the alias named, which is a
	/// constructor often enough to be worth writing rather than dropping.
	/// </remarks>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		code.Write($"const {usingAlias.Name} = {usingAlias.AliasedType?.Name ?? "Object"}");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript has nothing that names the member an argument is for, so arguments that do become
	/// an object literal — passed to the constructor when a type is named, on their own when one is
	/// not. That is the options-object convention rather than a translation of the C++ form, and it
	/// is the only shape in this language where the names survive at all.
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		bool named = construction.Arguments.Any(argument => argument is MemberInitialiser);

		if (construction.Type is null)
		{
			WriteLiteral(construction, code, named);
			return;
		}

		code.Write($"new {construction.Type.Name}(");

		if (named)
		{
			WriteLiteral(construction, code, true);
			code.Write(")");
			return;
		}

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

	/// <summary>
	/// Writes a construction's arguments as an object or array literal.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="named">Whether the arguments name the members they are for.</param>
	private void WriteLiteral(ConstructionExpression construction, CodeBlocker code, bool named)
	{
		code.Write(named ? "{ " : "[");

		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			if (construction.Arguments[index] is MemberInitialiser member)
			{
				code.Write($"{member.Name}: ");
				GenerateInternal(member.Value ?? new VariableReference(string.Empty), code);
				continue;
			}

			GenerateInternal(construction.Arguments[index], code);
		}

		code.Write(named ? " }" : "]");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript has no enumeration. A frozen object is the convention: the members are reachable by
	/// name, and freezing is what stops one being reassigned somewhere far from here. A member with
	/// no value of its own is numbered from its position.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(enumDecl, code);
		code.WriteLine($"const {enumDecl.Name ?? "UnnamedEnum"} = Object.freeze({{");

		using (IndentScope members = new(code))
		{
			WriteEnumMembers(enumDecl, code);
		}

		code.WriteLine("});");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A class field, which JavaScript writes without a type since it has none to write. A field with
	/// no initialiser is still declared: the property then exists on every instance, which is what
	/// makes the shape of an object predictable rather than growing as it is assigned to.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		GenerateDocumentation(field, code);
		code.Write(MemberName(field.Name ?? "unnamed", field.Visibility));

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);

		code.Write($"function {funcDecl.Name ?? "unnamedFunction"}(");
		GenerateParameterList(funcDecl.Parameters, code);

		// The line is left open, so the scope's brace lands on it: JavaScript braces hang.
		code.Write(") ");

		using Scope body = new(code);
		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A function inside a class body is written as a method — <c>name(args) { }</c> — because
	/// JavaScript's <c>function</c> keyword is a syntax error there. That is why the members are
	/// emitted here rather than through <see cref="StandardLanguageGenerator.GenerateClassMembers"/>.
	/// <para>
	/// A private member is spelled with the <c>#</c> prefix, which is JavaScript's own private syntax
	/// and enforced by the runtime. The other three visibilities have no spelling: JavaScript draws
	/// the line at private, and a <c>protected</c> or <c>internal</c> member is an ordinary one.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		// C++ can attach a declaration to a type it does not own, by specialising a template on it.
		// Nothing here can, so the fact is written down rather than lost: what follows is an
		// ordinary declaration, and the comment says which type it was the declaration for.
		if (classDecl.IsSpecialisation)
		{
			WriteInexpressible(code, $"specialised for {string.Join(", ", classDecl.SpecialisationArguments)}");
		}

		WriteAnnotations(classDecl.Annotations, code);
		WriteTypePromises(classDecl, code);
		WriteTypeParametersDown(classDecl.TypeParameters, code);

		// JavaScript extends one thing and has no interfaces at all, so anything the declaration
		// implements is written down rather than lost: a duck-typed object is expected to have the
		// members, and nothing in the file would otherwise say which ones.
		if (classDecl.Interfaces.Count > 0)
		{
			WriteInexpressible(code, $"implements {string.Join(", ", classDecl.Interfaces.Select(contract => contract.Name))}");
		}

		code.Write($"class {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.BaseType is TypeReference baseType)
		{
			code.Write($" extends {baseType.Name}");
		}

		// The line is left open, so the scope's brace lands on it: JavaScript braces hang.
		code.Write(" ");

		using Scope body = new(code);
		foreach (AstNode member in classDecl.Members)
		{
			switch (member)
			{
				case FunctionDeclaration method:
					GenerateMethod(method, code);
					break;

				case EnumDeclaration nested:
					GenerateNestedEnum(nested, code);
					break;

				// A field is not a variable: `let` is a statement keyword and a syntax error in a
				// class body, so the declaration is emitted as the name and its initializer alone.
				case VariableDeclaration field:
					GenerateField(field, code);
					break;

				default:
					GenerateInternal(member, code);
					break;
			}
		}
	}

	/// <summary>
	/// Emits a variable declaration as a class field.
	/// </summary>
	/// <param name="field">The declaration to emit as a field.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateField(VariableDeclaration field, CodeBlocker code)
	{
		// `static` is how a class holds one value rather than one per instance, which is what a
		// constant member means. JavaScript has no `const` for a field: the keyword declares a
		// binding in a scope, and a class body is not one.
		if (field.IsConstant)
		{
			code.Write("static ");
		}

		code.Write(MemberName(field.Name, field.Visibility));

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Emits a function as a class method, which drops the <c>function</c> keyword.
	/// </summary>
	/// <param name="method">The function to emit as a method.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateMethod(FunctionDeclaration method, CodeBlocker code)
	{
		GenerateDocumentation(method, code);

		if (method.Definition != FunctionDefinition.Provided)
		{
			string state = method.Definition == FunctionDefinition.Defaulted ? "supplied by the language" : "deleted";
			WriteInexpressible(code, $"{method.Name} is {state}, which JavaScript has no way to say.");
			return;
		}

		if (method.Kind is FunctionKind.Destructor or FunctionKind.Operator or FunctionKind.ConversionOperator)
		{
			WriteInexpressible(code, $"{method.Name} has no JavaScript spelling.");
			return;
		}

		if (method.IsStatic)
		{
			code.Write("static ");
		}

		code.Write(method.Kind == FunctionKind.Constructor
			? "constructor"
			: MemberName(method.Name ?? "unnamedMethod", method.Visibility));

		code.Write("(");
		GenerateParameterList(method.Parameters, code);
		code.Write(") ");

		using Scope body = new(code);

		// A method a subclass has to supply is one whose body refuses. JavaScript has no declaration
		// without a definition, so the definition is where that is said.
		if (method.IsAbstract)
		{
			code.WriteLine($"throw new Error(\"{method.Name} must be implemented\");");
			return;
		}

		// JavaScript assigns where C++ initialises, before the body's own statements and in the order
		// declared, which is what the initialiser means where there is no initialiser list.
		foreach (MemberInitialiser initialiser in method.Initialisers)
		{
			code.Write($"this.{initialiser.Name} = ");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}

			EndStatement(code);
		}

		foreach (AstNode statement in method.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write(parameter.Name ?? $"param{position}");
		AppendDefaultValue(parameter, code);
	}

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		// `const` needs an initializer, so an uninitialized constant has to be declared with `let`.
		string keyword = varDecl.IsConstant && varDecl.InitialValue is not null ? "const" : "let";
		code.Write($"{keyword} {varDecl.Name}");

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Spells a class member's name for its visibility.
	/// </summary>
	/// <param name="name">The member's name in the AST.</param>
	/// <param name="visibility">The visibility it was declared with.</param>
	/// <returns>The name as the class body should spell it.</returns>
	/// <remarks>
	/// <c>#</c> is a part of the name in JavaScript rather than a modifier in front of it, so private
	/// members are spelled here rather than by writing a keyword before the declaration.
	/// </remarks>
	private static string MemberName(string name, Visibility visibility) =>
		visibility == Visibility.Private ? $"#{name}" : name;

	/// <inheritdoc/>
	/// <remarks>
	/// JavaScript has no entry point of its own — a module runs top to bottom — so the function is
	/// emitted along with the call that runs it. The arguments and the exit code are Node's
	/// (<c>process.argv</c>, <c>process.exit</c>); a browser has neither, and there is nothing more
	/// portable to reach for.
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		code.Write("function main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("args");
		}

		// The line is left open, so the scope's brace lands on it: JavaScript braces hang.
		code.Write(") ");

		using (Scope body = new(code))
		{
			foreach (AstNode statement in entryPoint.Body)
			{
				GenerateInternal(statement, code);
			}
		}

		code.WriteLine();

		// The call is what makes the file a program rather than a definition of one.
		string arguments = entryPoint.AcceptsArguments ? "process.argv.slice(2)" : string.Empty;
		code.WriteLine(entryPoint.ReturnsExitCode
			? $"process.exit(main({arguments}));"
			: $"main({arguments});");
	}

	/// <summary>
	/// Maps a binary operator to its JavaScript spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Only equality differs from the C-family set: the AST's <see cref="BinaryOperator.Equal"/>
	/// means value equality, which is <c>===</c> in JavaScript. <c>==</c> coerces and would not.
	/// </remarks>
	protected override string GetOperatorSpelling(BinaryOperator op) => op switch
	{
		BinaryOperator.Equal => "===",
		BinaryOperator.NotEqual => "!==",
		_ => GetBinaryOperator(op)
	};
}
