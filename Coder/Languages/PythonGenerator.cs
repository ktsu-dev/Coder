// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Globalization;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates Python code from AST nodes.
/// </summary>
/// <remarks>
/// Python has neither access modifiers nor constants, so <see cref="Visibility"/> and
/// <see cref="VariableDeclaration.IsConstant"/> are deliberately dropped, the same way JavaScript
/// drops the types the AST carries. The conventions Python does have for both — a leading underscore
/// for a non-public member, an upper-case name for a constant — are spellings of the identifier
/// rather than modifiers on the declaration, and renaming a declaration here would leave every
/// <see cref="VariableReference"/> to it naming something that no longer exists.
/// </remarks>
public class PythonGenerator : StandardLanguageGenerator
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "python";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "Python";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "py";

	/// <summary>
	/// Spells a boolean literal. Python capitalizes them.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <returns>The keyword Python uses.</returns>
	protected override string FormatBoolean(bool value) => value ? "True" : "False";

	/// <summary>
	/// Ends a statement. Python has no terminator, and the function body emits the line breaks, so
	/// this deliberately writes nothing.
	/// </summary>
	/// <param name="code">The writer, left untouched.</param>
	protected override void EndStatement(CodeBlocker code)
	{
		// Python statements end at the newline the caller writes.
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python's documentation is a docstring rather than a comment, and a docstring belongs inside
	/// the construct it documents — which is a different shape from every other language here. Rather
	/// than move the lines somewhere the other three cannot follow, they are emitted as ordinary
	/// <c>#</c> comments: the reader still gets them, and nothing claims to be a docstring that is
	/// not one.
	/// </remarks>
	protected override string DocumentationPrefix => "#";

	/// <inheritdoc/>
	protected override string CommentPrefix => "#";

	/// <inheritdoc/>
	protected override string? SpellImport(string import) => $"import {import}";

	/// <inheritdoc/>
	/// <remarks>
	/// An alias is an ordinary assignment in Python, which is what a type alias is there.
	/// </remarks>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		code.WriteLine($"{usingAlias.Name} = {PythonTypeFromGenericType(usingAlias.AliasedType ?? new TypeReference("object"))}");
	}

	/// <inheritdoc/>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		code.Write($"{PythonTypeFromGenericType(construction.Type ?? new TypeReference("object"))}(");
		WriteArguments(construction, code);
		code.Write(")");
	}

	/// <summary>
	/// Writes a construction's arguments, separated by commas.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteArguments(ConstructionExpression construction, CodeBlocker code)
	{
		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			GenerateInternal(construction.Arguments[index], code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python has no enumeration syntax; <c>enum.Enum</c> is a class. A member with no value of its
	/// own is numbered from its position, matching what a language with real enumerations would give
	/// it. The <c>from enum import Enum</c> this needs belongs to the file rather than to the
	/// declaration.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(enumDecl, code);
		code.WriteLine($"class {enumDecl.Name ?? "UnnamedEnum"}(Enum):");

		using IndentScope body = new(code);
		if (enumDecl.Members.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		for (int index = 0; index < enumDecl.Members.Count; index++)
		{
			EnumMember member = enumDecl.Members[index];
			string value = member.Value ?? index.ToString(CultureInfo.InvariantCulture);
			code.WriteLine($"{member.Name ?? "UNNAMED"} = {value}");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field is written as an annotated class attribute. One with no initialiser is left as a bare
	/// annotation, which is what a dataclass and a type checker both read as "this field exists and
	/// has this type" without also claiming a value for it.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		GenerateDocumentation(field, code);
		code.Write(field.Name ?? "unnamed");

		if (field.Type is TypeReference type)
		{
			code.Write($": {PythonTypeFromGenericType(type)}");
		}

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		code.WriteLine();
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);

		// Function signature
		code.Write($"def {funcDecl.Name ?? "unnamed_function"}(");
		GenerateParameterList(funcDecl.Parameters, code);
		code.Write(")");

		// Add return type hint if available
		if (funcDecl.ReturnType != null)
		{
			code.Write($" -> {PythonTypeFromGenericType(funcDecl.ReturnType)}");
		}

		code.WriteLine(":");

		// Python's body is delimited by indentation alone, so there is no brace scope to open.
		using IndentScope body = new(code);

		// Function body
		if (funcDecl.Body.Count == 0)
		{
			// Empty function body needs a pass statement
			code.WriteLine("pass");
		}
		else
		{
			// Generate each statement in the body. EndStatement writes nothing for Python, so the
			// line break is this loop's to write.
			foreach (AstNode statement in funcDecl.Body)
			{
				GenerateInternal(statement, code);
				code.WriteLine();
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python's suite may not be empty, so a class with no members gets a <c>pass</c>, the same way an
	/// empty function body does.
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		code.Write($"class {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.BaseType is TypeReference baseType)
		{
			code.Write($"({PythonTypeFromGenericType(baseType)})");
		}

		code.WriteLine(":");

		// Python's body is delimited by indentation alone, so there is no brace scope to open.
		using IndentScope members = new(code);

		if (classDecl.Members.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		// EndStatement writes nothing for Python, so the line break is this loop's to write.
		foreach (AstNode member in classDecl.Members)
		{
			if (member is FunctionDeclaration method)
			{
				GenerateMethod(method, code);
			}
			else
			{
				GenerateInternal(member, code);
			}

			code.WriteLine();
		}
	}

	/// <summary>
	/// Emits a function as a method, which in Python means giving it the receiver as its first
	/// parameter.
	/// </summary>
	/// <param name="method">The function to emit as a method.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// <c>self</c> is Python's spelling of the receiver a method is called on. It is not carried in
	/// the AST — no other target language has it — so it is supplied here rather than being something
	/// the user has to remember to add as a parameter and then remove for every other language.
	/// <para>
	/// A static method is the one case where that receiver is not supplied, because
	/// <c>@staticmethod</c> is what says there is none. Every other language spells <c>static</c>
	/// alongside the signature; Python spells it by changing the signature.
	/// </para>
	/// </remarks>
	private void GenerateMethod(FunctionDeclaration method, CodeBlocker code)
	{
		GenerateDocumentation(method, code);

		if (method.Definition != FunctionDefinition.Provided)
		{
			string state = method.Definition == FunctionDefinition.Defaulted ? "supplied by the language" : "deleted";
			WriteInexpressible(code, $"{method.Name} is {state}, which Python has no way to say.");
			return;
		}

		if (method.Kind is FunctionKind.Operator or FunctionKind.ConversionOperator)
		{
			WriteInexpressible(code, $"operator {method.Name} has no Python spelling.");
			return;
		}

		WriteMethodSignature(method, code);

		using IndentScope body = new(code);

		// A method a derived class has to supply is a method whose body is a refusal. Python has no
		// declaration without a definition, so the definition says what calling it means.
		if (method.IsAbstract)
		{
			code.WriteLine("raise NotImplementedError");
			return;
		}

		WriteInitialiserAssignments(method, code);

		if (method.Body.Count == 0 && method.Initialisers.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		foreach (AstNode statement in method.Body)
		{
			GenerateInternal(statement, code);
			code.WriteLine();
		}
	}

	/// <summary>
	/// Writes a method's signature, up to and including the colon that opens its suite.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The receiver is supplied here rather than carried in the AST, because no other target language
	/// has one — and it is left out of a static method, which is what <c>@staticmethod</c> means.
	/// </remarks>
	private void WriteMethodSignature(FunctionDeclaration method, CodeBlocker code)
	{
		if (method.IsStatic)
		{
			code.WriteLine("@staticmethod");
		}

		code.Write($"def {SpellMethodName(method)}(");

		bool needsSeparator = !method.IsStatic;
		if (needsSeparator)
		{
			code.Write("self");
		}

		for (int index = 0; index < method.Parameters.Count; index++)
		{
			if (needsSeparator)
			{
				code.Write(", ");
			}

			GenerateParameter(method.Parameters[index], code, index);
			needsSeparator = true;
		}

		code.Write(")");

		if (method.ReturnType is not null && method.Kind == FunctionKind.Method)
		{
			code.Write($" -> {PythonTypeFromGenericType(method.ReturnType)}");
		}

		code.WriteLine(":");
	}

	/// <summary>
	/// Writes what the type's members start at, as assignments at the top of the body.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Python assigns where C++ initialises, in the order declared, which is what the initialiser
	/// means where there is no initialiser list to put it in.
	/// </remarks>
	private void WriteInitialiserAssignments(FunctionDeclaration method, CodeBlocker code)
	{
		foreach (MemberInitialiser initialiser in method.Initialisers)
		{
			code.Write($"self.{initialiser.Name} = ");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}

			code.WriteLine();
		}
	}

	/// <summary>
	/// Spells the name a method is written under.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <returns>The name as Python writes it.</returns>
	/// <remarks>
	/// A constructor and a destructor have fixed names in Python rather than the type's, so whatever
	/// the declaration is called is ignored for those two.
	/// </remarks>
	private static string SpellMethodName(FunctionDeclaration method) => method.Kind switch
	{
		FunctionKind.Constructor => "__init__",
		FunctionKind.Destructor => "__del__",
		_ => method.Name ?? "unnamed_method",
	};

	/// <inheritdoc/>
	/// <remarks>
	/// The function is emitted with the <c>__main__</c> guard that runs it, which is how a Python
	/// file is both a script and an importable module. Arguments and the exit code go through
	/// <c>sys</c>, so the import it needs is emitted with it — the entry point is the top of a file,
	/// which is the one place a generator can put an import without knowing what else the document
	/// holds.
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		bool needsSys = entryPoint.AcceptsArguments || entryPoint.ReturnsExitCode;
		if (needsSys)
		{
			code.WriteLine("import sys");
			code.WriteLine();
			code.WriteLine();
		}

		code.Write("def main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("args");
		}

		code.WriteLine("):");

		// Python's body is delimited by indentation alone, so there is no brace scope to open.
		using (IndentScope body = new(code))
		{
			if (entryPoint.Body.Count == 0)
			{
				code.WriteLine("pass");
			}
			else
			{
				// EndStatement writes nothing for Python, so the line break is this loop's to write.
				foreach (AstNode statement in entryPoint.Body)
				{
					GenerateInternal(statement, code);
					code.WriteLine();
				}
			}
		}

		// PEP 8 puts two blank lines between a top-level definition and what follows it.
		code.WriteLine();
		code.WriteLine();
		code.WriteLine("if __name__ == \"__main__\":");

		using IndentScope guard = new(code);
		string call = entryPoint.AcceptsArguments ? "main(sys.argv[1:])" : "main()";
		code.WriteLine(entryPoint.ReturnsExitCode ? $"sys.exit({call})" : call);
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write(parameter.Name ?? $"param{position}");

		// Type hints are optional in Python, so they are emitted only when the AST carries one.
		if (parameter.Type is not null)
		{
			code.Write($": {PythonTypeFromGenericType(parameter.Type)}");
		}

		AppendDefaultValue(parameter, code);
	}

	/// <summary>
	/// Spells a type in Python.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The Python source for it.</returns>
	/// <remarks>
	/// Python parameterises a type with brackets rather than angle brackets, which is only spellable
	/// now that the arguments are a list rather than part of a name. Read-only-ness and indirection
	/// have no spelling in Python at all, so neither is emitted.
	/// </remarks>
	private static string PythonTypeFromGenericType(TypeReference type)
	{
		string name = type.Name.ToLowerInvariant() switch
		{
			"int" => "int",
			"string" => "str",
			"bool" => "bool",
			"float" => "float",
			"double" => "float",
			"void" => "None",
			_ => type.Name
		};

		return type.TypeArguments.Count == 0
			? name
			: $"{name}[{string.Join(", ", type.TypeArguments.Select(PythonTypeFromGenericType))}]";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A constant is emitted as an ordinary assignment: Python has no constant declaration, and the
	/// upper-case naming that stands in for one is a convention about the identifier rather than
	/// something the declaration can say.
	/// </remarks>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		code.Write(varDecl.Name);

		if (varDecl.InitialValue != null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}
		else
		{
			// Python requires initialization, so use None for uninitialized variables
			code.Write(" = None");
		}
	}

	/// <summary>
	/// Maps a binary operator to its Python spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>Only the logical operators differ from the C-family set: Python spells them as words.</remarks>
	protected override string GetOperatorSpelling(BinaryOperator op) => op switch
	{
		BinaryOperator.LogicalAnd => "and",
		BinaryOperator.LogicalOr => "or",
		_ => GetBinaryOperator(op)
	};

	/// <summary>
	/// Maps a unary operator to its Python spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Only logical negation differs from the C-family set: Python spells it as a word. The emitter
	/// supplies the separating space, so this returns the bare keyword.
	/// </remarks>
	protected override string GetUnaryOperatorSpelling(UnaryOperator op) => op switch
	{
		UnaryOperator.LogicalNot => "not",
		_ => GetUnaryOperator(op)
	};
}
