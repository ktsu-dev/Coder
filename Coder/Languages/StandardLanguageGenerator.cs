// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// A <see cref="LanguageGeneratorBase"/> that owns the node dispatch, so a generator supplies only
/// the syntax its language does not share with the others.
/// </summary>
/// <remarks>
/// Every generator's dispatch was the same switch: structural nodes to their emitters, everything
/// else to <see cref="LanguageGeneratorBase.TryGenerateCommonNode"/>, an unrecognised node to a
/// <see cref="NotSupportedException"/>. Only the operator spelling and the language's name in that
/// message differed. It lives here once, and a derived generator writes three methods —
/// <see cref="GenerateFunctionDeclaration"/>, <see cref="GenerateVariableDeclaration"/> and
/// <see cref="GenerateParameter"/> — plus an operator override when its language needs one.
/// <para>
/// <c>CSharpGenerator</c> deliberately does not derive from this. It indents with a precomputed
/// string rather than a level, emits a comment instead of throwing for an unrecognised node, and
/// stringifies a return expression rather than recursing into it. Those are differences to settle on
/// their own terms, not to smuggle in here.
/// </para>
/// </remarks>
public abstract class StandardLanguageGenerator : LanguageGeneratorBase
{
	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public override bool CanGenerate(AstNode astNode) => CanGenerateStandardNodes(astNode);

	/// <summary>
	/// Dispatches a node to the emitter for its shape.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <exception cref="NotSupportedException">The node is not one this generator emits.</exception>
	protected sealed override void GenerateInternal(AstNode node, CodeBlocker code)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(code);

		switch (node)
		{
			case ClassDeclaration classDecl:
				GenerateClassDeclaration(classDecl, code);
				break;

			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, code);
				break;

			case SourceFile file:
				GenerateSourceFile(file, code);
				break;

			case NamespaceDeclaration namespaceDecl:
				GenerateNamespaceDeclaration(namespaceDecl, code);
				break;

			case UsingAlias usingAlias:
				GenerateUsingAlias(usingAlias, code);
				break;

			case ConstructionExpression construction:
				GenerateConstructionExpression(construction, code);
				break;

			case EnumDeclaration enumDecl:
				GenerateEnumDeclaration(enumDecl, code);
				break;

			case FieldDeclaration field:
				GenerateFieldDeclaration(field, code);
				break;

			case EntryPoint entryPoint:
				GenerateEntryPoint(entryPoint, code);
				break;

			case Parameter parameter:
				GenerateParameter(parameter, code, 0);
				break;

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, code);
				break;

			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, code, GetOperatorSpelling(binaryExpr.Operator));
				break;

			case UnaryExpression unaryExpr:
				GenerateUnaryExpression(unaryExpr, code, GetUnaryOperatorSpelling(unaryExpr.Operator));
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, code);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, code);
				break;

			default:
				if (!TryGenerateCommonNode(node, code))
				{
					throw new NotSupportedException($"Unsupported node type for {DisplayName} generation: {node.GetNodeTypeName()}");
				}

				break;
		}
	}

	/// <summary>
	/// Emits a function declaration, including its body.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code);

	/// <summary>
	/// Emits a class declaration, including its members.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code);

	/// <summary>
	/// Emits a namespace and its members.
	/// </summary>
	/// <param name="namespaceDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The default emits the members and nothing around them, which is right for a language whose
	/// unit of naming is the file. A language that writes a namespace overrides this.
	/// </remarks>
	protected virtual void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);

		bool first = true;
		foreach (AstNode member in namespaceDecl.Members)
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Emits an alias giving a type a second name.
	/// </summary>
	/// <param name="usingAlias">The alias to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code);

	/// <summary>
	/// Emits an expression that builds a value.
	/// </summary>
	/// <param name="construction">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code);

	/// <summary>
	/// Emits an enumeration declaration, including its members.
	/// </summary>
	/// <param name="enumDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code);

	/// <summary>
	/// Emits a field of a type.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code);

	/// <summary>
	/// Emits the program's entry point, and whatever else the language needs in order to run it.
	/// </summary>
	/// <param name="entryPoint">The entry point to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code);

	/// <summary>
	/// Emits the members of a class, one after another.
	/// </summary>
	/// <param name="classDecl">The class whose members to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A member is dispatched like any other node, so a nested class, a method and a field each come
	/// out through the emitter for its own shape and nesting needs no separate code path.
	/// </remarks>
	protected void GenerateClassMembers(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);

		foreach (AstNode member in classDecl.Members)
		{
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Emits a variable declaration as a complete statement.
	/// </summary>
	/// <param name="varDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code);

	/// <summary>
	/// Emits one parameter, without any separator.
	/// </summary>
	/// <param name="parameter">The parameter to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="position">The parameter's position, used to name an unnamed parameter.</param>
	protected abstract void GenerateParameter(Parameter parameter, CodeBlocker code, int position);

	/// <summary>
	/// Appends a parameter's default value, when it has one.
	/// </summary>
	/// <param name="parameter">The parameter whose default to append.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A default value is how every target language expresses <see cref="Parameter.IsOptional"/>;
	/// none of them has separate syntax for it.
	/// </remarks>
	protected static void AppendDefaultValue(Parameter parameter, CodeBlocker code)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" = {parameter.DefaultValue}");
		}
	}

	/// <summary>
	/// Emits a comma-separated parameter list, without the surrounding parentheses.
	/// </summary>
	/// <param name="parameters">The parameters to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateParameterList(IReadOnlyList<Parameter> parameters, CodeBlocker code)
	{
		Ensure.NotNull(parameters);
		Ensure.NotNull(code);

		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
			{
				code.Write(", ");
			}

			GenerateParameter(parameters[i], code, i);
		}
	}

	/// <summary>
	/// Spells a binary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetBinaryOperator"/>.
	/// </remarks>
	protected virtual string GetOperatorSpelling(BinaryOperator op) => GetBinaryOperator(op);

	/// <summary>
	/// Spells a unary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetUnaryOperator"/>. A word operator is
	/// separated from its operand by the emitter, so an override returns the bare word.
	/// </remarks>
	protected virtual string GetUnaryOperatorSpelling(UnaryOperator op) => GetUnaryOperator(op);
}
