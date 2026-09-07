// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Text;
using ktsu.Coder.Ast;

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
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	/// <exception cref="NotSupportedException">The node is not one this generator emits.</exception>
	protected sealed override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(builder);

		switch (node)
		{
			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, builder, indentLevel);
				break;

			case Parameter parameter:
				GenerateParameter(parameter, builder, 0);
				break;

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, builder, indentLevel);
				break;

			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, builder, GetOperatorSpelling(binaryExpr.Operator));
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, builder, indentLevel);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, builder, indentLevel);
				break;

			default:
				if (!TryGenerateCommonNode(node, builder))
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
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected abstract void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel);

	/// <summary>
	/// Emits a variable declaration as a complete statement.
	/// </summary>
	/// <param name="varDecl">The declaration to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected abstract void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel);

	/// <summary>
	/// Emits one parameter, without any separator.
	/// </summary>
	/// <param name="parameter">The parameter to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="position">The parameter's position, used to name an unnamed parameter.</param>
	protected abstract void GenerateParameter(Parameter parameter, StringBuilder builder, int position);

	/// <summary>
	/// Appends a parameter's default value, when it has one.
	/// </summary>
	/// <param name="parameter">The parameter whose default to append.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <remarks>
	/// A default value is how every target language expresses <see cref="Parameter.IsOptional"/>;
	/// none of them has separate syntax for it.
	/// </remarks>
	protected static void AppendDefaultValue(Parameter parameter, StringBuilder builder)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(builder);

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			builder.Append(" = ");
			builder.Append(parameter.DefaultValue);
		}
	}

	/// <summary>
	/// Emits a comma-separated parameter list, without the surrounding parentheses.
	/// </summary>
	/// <param name="parameters">The parameters to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	protected void GenerateParameterList(IReadOnlyList<Parameter> parameters, StringBuilder builder)
	{
		Ensure.NotNull(parameters);
		Ensure.NotNull(builder);

		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}

			GenerateParameter(parameters[i], builder, i);
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
}
