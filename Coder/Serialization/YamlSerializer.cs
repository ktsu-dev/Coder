// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Serialization;

using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Handles serialization of AST nodes to YAML format.
/// </summary>
public class YamlSerializer
{
	private readonly ISerializer _serializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="YamlSerializer"/> class.
	/// </summary>
	public YamlSerializer()
	{
		_serializer = new SerializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.DisableAliases()
			.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
			.Build();
	}

	/// <summary>
	/// Serializes an AST node into a YAML string.
	/// </summary>
	/// <param name="node">The AST node to serialize.</param>
	/// <returns>A YAML string representation of the AST node.</returns>
	public string Serialize(AstNode node)
	{
		Ensure.NotNull(node);

		// Create a dictionary to represent the YAML structure
		Dictionary<string, object> root = [];
		SerializeNode(node, root);

		// Serialize the dictionary to YAML
		return _serializer.Serialize(root);
	}

	private static void SerializeNode(AstNode node, Dictionary<string, object> target)
	{
		string nodeKey = ToCamelCase(node.GetNodeTypeName());

		// Handle leaf nodes
		if (TrySerializeLeafNode(node, nodeKey, target))
		{
			return;
		}

		// Handle composite nodes
		Dictionary<string, object> nodeData = [];
		SerializeCompositeNode(node, nodeData);
		target[nodeKey] = nodeData;
	}

	private static bool TrySerializeLeafNode(AstNode node, string nodeKey, Dictionary<string, object> target)
	{
		switch (node)
		{
			case AstLeafNode<string> stringLeaf when stringLeaf.Value != null:
				target[nodeKey] = stringLeaf.Value;
				return true;
			case AstLeafNode<int> intLeaf:
				target[nodeKey] = intLeaf.Value;
				return true;
			case AstLeafNode<bool> boolLeaf:
				target[nodeKey] = boolLeaf.Value;
				return true;
			default:
				return false;
		}
	}

	private static void SerializeCompositeNode(AstNode node, Dictionary<string, object> nodeData)
	{
		switch (node)
		{
			case SourceFile file:
				SerializeSourceFile(file, nodeData);
				break;
			case NamespaceDeclaration namespaceDecl:
				SerializeNamespaceDeclaration(namespaceDecl, nodeData);
				break;
			case EnumDeclaration enumDecl:
				SerializeEnumDeclaration(enumDecl, nodeData);
				break;
			case EnumMember enumMember:
				SerializeEnumMember(enumMember, nodeData);
				break;
			case FieldDeclaration field:
				SerializeFieldDeclaration(field, nodeData);
				break;
			case ClassDeclaration classDecl:
				SerializeClassDeclaration(classDecl, nodeData);
				break;
			case FunctionDeclaration funcDecl:
				SerializeFunctionDeclaration(funcDecl, nodeData);
				break;
			case EntryPoint entryPoint:
				SerializeEntryPoint(entryPoint, nodeData);
				break;
			case Parameter param:
				SerializeParameter(param, nodeData);
				break;
			default:
				SerializeOtherNode(node, nodeData);
				break;
		}
	}

	/// <summary>
	/// Writes the nodes that are not declarations.
	/// </summary>
	/// <param name="node">The node being serialized.</param>
	/// <param name="nodeData">The mapping to write into.</param>
	/// <remarks>
	/// Split from the declarations only because one switch over every node the AST has is more
	/// branches than the analyzer accepts. The line is the same one the AST already draws.
	/// </remarks>
	private static void SerializeOtherNode(AstNode node, Dictionary<string, object> nodeData)
	{
		switch (node)
		{
			case CompileTimeAssertion assertion:
				SerializeCompileTimeAssertion(assertion, nodeData);
				break;
			case UsingAlias usingAlias:
				SerializeUsingAlias(usingAlias, nodeData);
				break;
			case MemberInitialiser initialiser:
				SerializeMemberInitialiser(initialiser, nodeData);
				break;
			case ConstructionExpression construction:
				SerializeConstructionExpression(construction, nodeData);
				break;
			case CallExpression callExpr:
				SerializeCallExpression(callExpr, nodeData);
				break;
			case ConditionalExpression conditional:
				SerializeConditionalExpression(conditional, nodeData);
				break;
			case ExpressionStatement statement:
				SerializeExpressionStatement(statement, nodeData);
				break;
			case ReturnStatement returnStmt:
				SerializeReturnStatement(returnStmt, nodeData);
				break;
			case BinaryExpression binaryExpr:
				SerializeBinaryExpression(binaryExpr, nodeData);
				break;
			case UnaryExpression unaryExpr:
				SerializeUnaryExpression(unaryExpr, nodeData);
				break;
			case LiteralExpression<string> stringLit:
				SerializeLiteralExpression(stringLit, nodeData);
				break;
			case LiteralExpression<int> intLit:
				SerializeLiteralExpression(intLit, nodeData);
				break;
			case LiteralExpression<bool> boolLit:
				SerializeLiteralExpression(boolLit, nodeData);
				break;
			case LiteralExpression<double> doubleLit:
				SerializeLiteralExpression(doubleLit, nodeData);
				break;
			case VariableReference varRef:
				SerializeVariableReference(varRef, nodeData);
				break;
			case VariableDeclaration varDecl:
				SerializeVariableDeclaration(varDecl, nodeData);
				break;
			case AssignmentStatement assignment:
				SerializeAssignmentStatement(assignment, nodeData);
				break;
			default:
				// Handle unknown node types - no specific serialization needed
				break;
		}

		// Handle generic composite node children
		if (node is AstCompositeNode compositeNode)
		{
			SerializeCompositeChildren(compositeNode, nodeData);
		}

		// Add metadata if present
		if (node.Metadata.Count > 0)
		{
			nodeData["metadata"] = node.Metadata;
		}
	}

	private static void SerializeFunctionDeclaration(FunctionDeclaration funcDecl, Dictionary<string, object> nodeData)
	{
		if (funcDecl.Name != null)
		{
			nodeData["name"] = funcDecl.Name;
		}

		if (funcDecl.ReturnType != null)
		{
			nodeData["returnType"] = funcDecl.ReturnType.ToString();
		}

		SerializeVisibility(funcDecl, nodeData);
		SerializeDocumentation(funcDecl, nodeData);

		// Written only when true: a modifier nobody asked for should not appear in the document, the
		// same way an unspecified visibility does not.
		if (funcDecl.IsStatic)
		{
			nodeData["isStatic"] = funcDecl.IsStatic;
		}

		if (funcDecl.IsPure)
		{
			nodeData["isPure"] = funcDecl.IsPure;
		}

		SerializeFunctionShape(funcDecl, nodeData);

		if (funcDecl.Parameters.Count > 0)
		{
			nodeData["parameters"] = SerializeParameters(funcDecl.Parameters);
		}

		if (funcDecl.Body.Count > 0)
		{
			nodeData["body"] = SerializeBodyStatements(funcDecl.Body);
		}
	}

	/// <summary>
	/// Writes a declaration's visibility, when it has one.
	/// </summary>
	/// <param name="declaration">The declaration being serialized.</param>
	/// <param name="nodeData">The mapping to write into.</param>
	/// <remarks>
	/// Written in lower case, which is how every target language spells the modifier and how someone
	/// hand-editing the YAML would expect to type it. <see cref="Visibility.Unspecified"/> is written
	/// as nothing at all: it means the declaration carries no visibility, and a key saying so would
	/// only be noise in the diff.
	/// </remarks>
	private static void SerializeVisibility(IHasVisibility declaration, Dictionary<string, object> nodeData)
	{
		if (declaration.Visibility != Visibility.Unspecified)
		{
			nodeData["visibility"] = declaration.Visibility.ToString().ToLowerInvariant();
		}
	}

	private static void SerializeEntryPoint(EntryPoint entryPoint, Dictionary<string, object> nodeData)
	{
		if (entryPoint.AcceptsArguments)
		{
			nodeData["acceptsArguments"] = entryPoint.AcceptsArguments;
		}

		if (entryPoint.ReturnsExitCode)
		{
			nodeData["returnsExitCode"] = entryPoint.ReturnsExitCode;
		}

		if (entryPoint.Body.Count > 0)
		{
			nodeData["body"] = SerializeBodyStatements(entryPoint.Body);
		}
	}

	private static void SerializeSourceFile(SourceFile file, Dictionary<string, object> nodeData)
	{
		if (file.Name != null)
		{
			nodeData["name"] = file.Name;
		}

		if (file.IsHeader)
		{
			nodeData["isHeader"] = file.IsHeader;
		}

		if (file.HeaderComment.Count > 0)
		{
			nodeData["headerComment"] = file.HeaderComment.ToList();
		}

		if (file.Imports.Count > 0)
		{
			nodeData["imports"] = file.Imports.ToList();
		}

		if (file.Members.Count > 0)
		{
			nodeData[MembersKey] = SerializeBodyStatements(file.Members);
		}
	}

	private static void SerializeNamespaceDeclaration(NamespaceDeclaration namespaceDecl, Dictionary<string, object> nodeData)
	{
		if (namespaceDecl.Name != null)
		{
			nodeData["name"] = namespaceDecl.Name;
		}

		SerializeDocumentation(namespaceDecl, nodeData);

		if (namespaceDecl.Members.Count > 0)
		{
			nodeData[MembersKey] = SerializeBodyStatements(namespaceDecl.Members);
		}
	}

	/// <summary>The key a node's single value is written under.</summary>
	private const string ValueKey = "value";

	/// <summary>The key a node's members are written under.</summary>
	private const string MembersKey = "members";

	private static void SerializeCompileTimeAssertion(CompileTimeAssertion assertion, Dictionary<string, object> nodeData)
	{
		if (assertion.Condition != null)
		{
			nodeData["condition"] = assertion.Condition;
		}

		if (assertion.Message != null)
		{
			nodeData["message"] = assertion.Message;
		}
	}

	private static void SerializeUsingAlias(UsingAlias usingAlias, Dictionary<string, object> nodeData)
	{
		if (usingAlias.Name != null)
		{
			nodeData["name"] = usingAlias.Name;
		}

		if (usingAlias.AliasedType != null)
		{
			nodeData["aliasedType"] = usingAlias.AliasedType.ToString();
		}

		SerializeVisibility(usingAlias, nodeData);
		SerializeDocumentation(usingAlias, nodeData);
	}

	private static void SerializeMemberInitialiser(MemberInitialiser initialiser, Dictionary<string, object> nodeData)
	{
		if (initialiser.Name != null)
		{
			nodeData["name"] = initialiser.Name;
		}

		if (initialiser.Value != null)
		{
			Dictionary<string, object> valueData = [];
			SerializeNode(initialiser.Value, valueData);
			nodeData[ValueKey] = valueData;
		}
	}

	private static void SerializeConstructionExpression(ConstructionExpression construction, Dictionary<string, object> nodeData)
	{
		if (construction.Type != null)
		{
			nodeData["type"] = construction.Type.ToString();
		}

		if (construction.Arguments.Count > 0)
		{
			nodeData["arguments"] = SerializeBodyStatements(construction.Arguments);
		}
	}

	private static void SerializeCallExpression(CallExpression callExpr, Dictionary<string, object> nodeData)
	{
		nodeData["callee"] = callExpr.Callee;

		if (callExpr.Receiver is not null)
		{
			Dictionary<string, object> receiverData = [];
			SerializeNode(callExpr.Receiver, receiverData);
			nodeData["receiver"] = receiverData;
		}

		if (callExpr.Arguments.Count > 0)
		{
			nodeData["arguments"] = SerializeBodyStatements(callExpr.Arguments);
		}

		if (callExpr.ExpectedType != null)
		{
			nodeData["expectedType"] = callExpr.ExpectedType;
		}
	}

	private static void SerializeConditionalExpression(ConditionalExpression conditional, Dictionary<string, object> nodeData)
	{
		Dictionary<string, object> conditionData = [];
		SerializeNode(conditional.Condition, conditionData);
		nodeData["condition"] = conditionData;

		Dictionary<string, object> whenTrueData = [];
		SerializeNode(conditional.WhenTrue, whenTrueData);
		nodeData["whenTrue"] = whenTrueData;

		Dictionary<string, object> whenFalseData = [];
		SerializeNode(conditional.WhenFalse, whenFalseData);
		nodeData["whenFalse"] = whenFalseData;

		if (conditional.ExpectedType != null)
		{
			nodeData["expectedType"] = conditional.ExpectedType;
		}
	}

	private static void SerializeExpressionStatement(ExpressionStatement statement, Dictionary<string, object> nodeData)
	{
		Dictionary<string, object> expressionData = [];
		SerializeNode(statement.Expression, expressionData);
		nodeData["expression"] = expressionData;
	}

	private static void SerializeEnumDeclaration(EnumDeclaration enumDecl, Dictionary<string, object> nodeData)
	{
		if (enumDecl.Name != null)
		{
			nodeData["name"] = enumDecl.Name;
		}

		if (enumDecl.UnderlyingType != null)
		{
			nodeData["underlyingType"] = enumDecl.UnderlyingType.ToString();
		}

		SerializeVisibility(enumDecl, nodeData);
		SerializeDocumentation(enumDecl, nodeData);

		if (enumDecl.Members.Count > 0)
		{
			nodeData[MembersKey] = SerializeBodyStatements(enumDecl.Members);
		}
	}

	private static void SerializeEnumMember(EnumMember member, Dictionary<string, object> nodeData)
	{
		if (member.Name != null)
		{
			nodeData["name"] = member.Name;
		}

		if (member.Value != null)
		{
			nodeData[ValueKey] = member.Value;
		}
	}

	private static void SerializeFieldDeclaration(FieldDeclaration field, Dictionary<string, object> nodeData)
	{
		if (field.Name != null)
		{
			nodeData["name"] = field.Name;
		}

		if (field.Type != null)
		{
			nodeData["type"] = field.Type.ToString();
		}

		if (field.IsStatic)
		{
			nodeData["isStatic"] = field.IsStatic;
		}

		if (field.IsConstant)
		{
			nodeData["isConstant"] = field.IsConstant;
		}

		SerializeVisibility(field, nodeData);
		SerializeDocumentation(field, nodeData);

		if (field.InitialValue != null)
		{
			Dictionary<string, object> initialValueData = [];
			SerializeNode(field.InitialValue, initialValueData);
			nodeData["initialValue"] = initialValueData;
		}
	}

	/// <summary>
	/// Writes what a function declares and how, omitting whatever it did not ask for.
	/// </summary>
	/// <param name="funcDecl">The declaration being serialized.</param>
	/// <param name="nodeData">The mapping to write into.</param>
	/// <remarks>
	/// Separate from the name and the return type only because one method writing every property a
	/// declaration has is more branches than the analyzer accepts. Nothing is written for a property
	/// left at its default, so a document says only what someone chose.
	/// </remarks>
	private static void SerializeFunctionShape(FunctionDeclaration funcDecl, Dictionary<string, object> nodeData)
	{
		if (funcDecl.Kind != FunctionKind.Method)
		{
			nodeData["kind"] = funcDecl.Kind.ToString();
		}

		if (funcDecl.Definition != FunctionDefinition.Provided)
		{
			nodeData["definition"] = funcDecl.Definition.ToString();
		}

		if (funcDecl.IsVirtual)
		{
			nodeData["isVirtual"] = funcDecl.IsVirtual;
		}

		if (funcDecl.IsAbstract)
		{
			nodeData["isAbstract"] = funcDecl.IsAbstract;
		}

		if (funcDecl.IsReadOnly)
		{
			nodeData["isReadOnly"] = funcDecl.IsReadOnly;
		}

		if (funcDecl.MustUseResult)
		{
			nodeData["mustUseResult"] = funcDecl.MustUseResult;
		}

		if (funcDecl.IsExplicit)
		{
			nodeData["isExplicit"] = funcDecl.IsExplicit;
		}

		if (funcDecl.IsCompileTimeEvaluable)
		{
			nodeData["isCompileTimeEvaluable"] = funcDecl.IsCompileTimeEvaluable;
		}

		if (funcDecl.IsNoThrow)
		{
			nodeData["isNoThrow"] = funcDecl.IsNoThrow;
		}

		if (funcDecl.IsFriend)
		{
			nodeData["isFriend"] = funcDecl.IsFriend;
		}

		if (funcDecl.Initialisers.Count > 0)
		{
			nodeData["initialisers"] = SerializeBodyStatements(funcDecl.Initialisers);
		}
	}

	/// <summary>
	/// Writes a declaration's documentation, when it has any.
	/// </summary>
	/// <param name="declaration">The declaration being serialized.</param>
	/// <param name="nodeData">The mapping to write into.</param>
	private static void SerializeDocumentation(IHasDocumentation declaration, Dictionary<string, object> nodeData)
	{
		if (declaration.Documentation.Count > 0)
		{
			nodeData["documentation"] = declaration.Documentation.ToList();
		}
	}

	private static void SerializeClassDeclaration(ClassDeclaration classDecl, Dictionary<string, object> nodeData)
	{
		if (classDecl.Name != null)
		{
			nodeData["name"] = classDecl.Name;
		}

		if (classDecl.Kind != TypeDeclarationKind.Class)
		{
			nodeData["kind"] = classDecl.Kind.ToString();
		}

		if (classDecl.BaseType != null)
		{
			nodeData["baseType"] = classDecl.BaseType.ToString();
		}

		if (classDecl.SpecialisationArguments.Count > 0)
		{
			// Each argument on its own, rather than joined: a type argument can itself have type
			// arguments, so a comma is part of one of them as often as it is a separator.
			nodeData["specialisationArguments"] =
				classDecl.SpecialisationArguments.Select(argument => argument.ToString()).ToList();
		}

		SerializeVisibility(classDecl, nodeData);
		SerializeDocumentation(classDecl, nodeData);

		if (classDecl.Members.Count > 0)
		{
			// Members are serialized the same way a function body is: each is a node in its own right.
			nodeData[MembersKey] = SerializeBodyStatements(classDecl.Members);
		}
	}

	private static List<Dictionary<string, object>> SerializeParameters(IEnumerable<Parameter> parameters)
	{
		List<Dictionary<string, object>> parameterList = [];
		foreach (Parameter param in parameters)
		{
			Dictionary<string, object> paramData = [];
			SerializeParameter(param, paramData);
			parameterList.Add(paramData);
		}

		return parameterList;
	}

	private static List<Dictionary<string, object>> SerializeBodyStatements(IEnumerable<AstNode> statements)
	{
		List<Dictionary<string, object>> bodyStatements = [];
		foreach (AstNode statement in statements)
		{
			Dictionary<string, object> statementData = [];
			SerializeNode(statement, statementData);
			bodyStatements.Add(statementData);
		}

		return bodyStatements;
	}

	private static void SerializeParameter(Parameter param, Dictionary<string, object> nodeData)
	{
		if (param.Name != null)
		{
			nodeData["name"] = param.Name;
		}

		if (param.Type != null)
		{
			nodeData["type"] = param.Type.ToString();
		}

		if (param.IsOptional)
		{
			nodeData["isOptional"] = param.IsOptional;

			if (param.DefaultValue != null)
			{
				nodeData["defaultValue"] = param.DefaultValue;
			}
		}
	}

	private static void SerializeReturnStatement(ReturnStatement returnStmt, Dictionary<string, object> nodeData)
	{
		if (returnStmt.Expression != null)
		{
			Dictionary<string, object> expressionData = [];
			SerializeNode(returnStmt.Expression, expressionData);
			nodeData["expression"] = expressionData;
		}
	}

	private static void SerializeCompositeChildren(AstCompositeNode compositeNode, Dictionary<string, object> nodeData)
	{
		foreach ((string key, AstNode childNode) in compositeNode.Children)
		{
			if (!key.Equals("Expression", StringComparison.OrdinalIgnoreCase)) // Already handled above
			{
				Dictionary<string, object> childData = [];
				SerializeNode(childNode, childData);
				nodeData[key.ToLowerInvariant()] = childData;
			}
		}
	}

	private static void SerializeBinaryExpression(BinaryExpression binaryExpr, Dictionary<string, object> nodeData)
	{
		Dictionary<string, object> leftData = [];
		SerializeNode(binaryExpr.Left, leftData);
		nodeData["left"] = leftData;

		nodeData["operator"] = binaryExpr.Operator.ToString();

		Dictionary<string, object> rightData = [];
		SerializeNode(binaryExpr.Right, rightData);
		nodeData["right"] = rightData;

		if (binaryExpr.ExpectedType != null)
		{
			nodeData["expectedType"] = binaryExpr.ExpectedType;
		}
	}

	private static void SerializeUnaryExpression(UnaryExpression unaryExpr, Dictionary<string, object> nodeData)
	{
		nodeData["operator"] = unaryExpr.Operator.ToString();

		Dictionary<string, object> operandData = [];
		SerializeNode(unaryExpr.Operand, operandData);
		nodeData["operand"] = operandData;

		if (unaryExpr.ExpectedType != null)
		{
			nodeData["expectedType"] = unaryExpr.ExpectedType;
		}
	}

	private static void SerializeLiteralExpression<T>(LiteralExpression<T> literal, Dictionary<string, object> nodeData)
	{
		if (literal.Value != null)
		{
			nodeData[ValueKey] = literal.Value;
		}

		if (literal.ExpectedType != null)
		{
			nodeData["expectedType"] = literal.ExpectedType;
		}
	}

	private static void SerializeVariableReference(VariableReference varRef, Dictionary<string, object> nodeData)
	{
		nodeData["name"] = varRef.Name;

		if (varRef.ExpectedType != null)
		{
			nodeData["expectedType"] = varRef.ExpectedType;
		}
	}

	private static void SerializeVariableDeclaration(VariableDeclaration varDecl, Dictionary<string, object> nodeData)
	{
		nodeData["name"] = varDecl.Name;

		if (varDecl.Type != null)
		{
			nodeData["type"] = varDecl.Type.ToString();
		}

		if (varDecl.InitialValue != null)
		{
			Dictionary<string, object> initialValueData = [];
			SerializeNode(varDecl.InitialValue, initialValueData);
			nodeData["initialValue"] = initialValueData;
		}

		if (varDecl.IsConstant)
		{
			nodeData["isConstant"] = varDecl.IsConstant;
		}

		if (varDecl.IsTypeInferred)
		{
			nodeData["isTypeInferred"] = varDecl.IsTypeInferred;
		}

		SerializeVisibility(varDecl, nodeData);
	}

	private static void SerializeAssignmentStatement(AssignmentStatement assignment, Dictionary<string, object> nodeData)
	{
		Dictionary<string, object> targetData = [];
		SerializeNode(assignment.Target, targetData);
		nodeData["target"] = targetData;

		Dictionary<string, object> valueData = [];
		SerializeNode(assignment.Value, valueData);
		nodeData[ValueKey] = valueData;

		nodeData["operator"] = assignment.Operator.ToString();
	}

	/// <summary>
	/// Converts a PascalCase string to camelCase.
	/// </summary>
	/// <param name="pascalCase">The PascalCase string to convert.</param>
	/// <returns>The camelCase equivalent string.</returns>
	private static string ToCamelCase(string pascalCase) =>
		string.IsNullOrEmpty(pascalCase) ? pascalCase : char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
}
