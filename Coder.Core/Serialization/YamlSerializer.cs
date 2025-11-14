// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Serialization;

using System.Collections.Generic;
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
		ArgumentNullException.ThrowIfNull(node);

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
			case FunctionDeclaration funcDecl:
				SerializeFunctionDeclaration(funcDecl, nodeData);
				break;
			case Parameter param:
				SerializeParameter(param, nodeData);
				break;
			case ReturnStatement returnStmt:
				SerializeReturnStatement(returnStmt, nodeData);
				break;
			case BinaryExpression binaryExpr:
				SerializeBinaryExpression(binaryExpr, nodeData);
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
			nodeData["returnType"] = funcDecl.ReturnType;
		}

		if (funcDecl.Parameters.Count > 0)
		{
			nodeData["parameters"] = SerializeParameters(funcDecl.Parameters);
		}

		if (funcDecl.Body.Count > 0)
		{
			nodeData["body"] = SerializeBodyStatements(funcDecl.Body);
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
			nodeData["type"] = param.Type;
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

	private static void SerializeLiteralExpression<T>(LiteralExpression<T> literal, Dictionary<string, object> nodeData)
	{
		if (literal.Value != null)
		{
			nodeData["value"] = literal.Value;
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
			nodeData["type"] = varDecl.Type;
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

		if (varDecl.AccessModifier != null)
		{
			nodeData["accessModifier"] = varDecl.AccessModifier;
		}
	}

	private static void SerializeAssignmentStatement(AssignmentStatement assignment, Dictionary<string, object> nodeData)
	{
		Dictionary<string, object> targetData = [];
		SerializeNode(assignment.Target, targetData);
		nodeData["target"] = targetData;

		Dictionary<string, object> valueData = [];
		SerializeNode(assignment.Value, valueData);
		nodeData["value"] = valueData;

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
