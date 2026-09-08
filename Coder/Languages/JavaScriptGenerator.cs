// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

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
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

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
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		code.Write($"class {classDecl.Name ?? "UnnamedClass"}");

		if (!string.IsNullOrEmpty(classDecl.BaseType))
		{
			code.Write($" extends {classDecl.BaseType}");
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
		code.Write(field.Name);

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
		code.Write($"{method.Name ?? "unnamedMethod"}(");
		GenerateParameterList(method.Parameters, code);
		code.Write(") ");

		using Scope body = new(code);
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
