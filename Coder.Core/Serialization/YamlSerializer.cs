// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Serialization;

using System.Collections.Generic;
using ktsu.Coder.Core.Ast;
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
		var root = new Dictionary<string, object>();
		SerializeNode(node, root);

		// Serialize the dictionary to YAML
		return _serializer.Serialize(root);
	}

	private static void SerializeNode(AstNode node, Dictionary<string, object> target)
	{
		var nodeKey = node.GetNodeTypeName();

		// Handle leaf nodes
		if (TrySerializeLeafNode(node, nodeKey, target))
		{
			return;
		}

		// Handle composite nodes
		var nodeData = new Dictionary<string, object>();
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
		var parameterList = new List<Dictionary<string, object>>();
		foreach (var param in parameters)
		{
			var paramData = new Dictionary<string, object>();
			SerializeParameter(param, paramData);
			parameterList.Add(paramData);
		}

		return parameterList;
	}

	private static List<Dictionary<string, object>> SerializeBodyStatements(IEnumerable<AstNode> statements)
	{
		var bodyStatements = new List<Dictionary<string, object>>();
		foreach (var statement in statements)
		{
			var statementData = new Dictionary<string, object>();
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
			var expressionData = new Dictionary<string, object>();
			SerializeNode(returnStmt.Expression, expressionData);
			nodeData["expression"] = expressionData;
		}
	}

	private static void SerializeCompositeChildren(AstCompositeNode compositeNode, Dictionary<string, object> nodeData)
	{
		foreach (var (key, childNode) in compositeNode.Children)
		{
			if (!key.Equals("Expression", StringComparison.OrdinalIgnoreCase)) // Already handled above
			{
				var childData = new Dictionary<string, object>();
				SerializeNode(childNode, childData);
				nodeData[key.ToLowerInvariant()] = childData;
			}
		}
	}
}
