// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Serialization;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ktsu.Coder.Core.Ast;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Handles deserialization of YAML strings back into AST nodes.
/// </summary>
public partial class YamlDeserializer
{
	private readonly IDeserializer _deserializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="YamlDeserializer"/> class.
	/// </summary>
	public YamlDeserializer()
	{
		_deserializer = new DeserializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.Build();
	}

	/// <summary>
	/// Deserializes a YAML string into an AST node.
	/// </summary>
	/// <param name="yaml">The YAML string to deserialize.</param>
	/// <returns>The deserialized AST node.</returns>
	public AstNode? Deserialize(string yaml)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

		// Deserialize YAML to a dictionary
		var rootDict = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
		if (rootDict == null || rootDict.Count == 0)
		{
			return null;
		}

		// Process the first node type found
		var (nodeType, nodeData) = rootDict.First();
		return DeserializeNode(nodeType, nodeData);
	}

	private AstNode? DeserializeNode(string nodeType, object? nodeData)
	{
		return nodeType switch
		{
			"FunctionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"Parameter" => DeserializeParameter(nodeData),
			"ReturnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			_ when nodeType.StartsWith("Leaf<", StringComparison.OrdinalIgnoreCase) => DeserializeLeafNode(nodeType, nodeData),
			_ => null,
		};
	}

	private static AstNode? DeserializeLeafNode(string nodeType, object? nodeData)
	{
		var match = MyRegex().Match(nodeType);
		if (!match.Success || match.Groups.Count < 2)
		{
			return null;
		}

		var valueType = match.Groups[1].Value;

		return valueType switch
		{
			"String" => new AstLeafNode<string>(nodeData?.ToString() ?? string.Empty),
			"Int32" when int.TryParse(nodeData?.ToString(), out var intValue) => new AstLeafNode<int>(intValue),
			"Boolean" when bool.TryParse(nodeData?.ToString(), out var boolValue) => new AstLeafNode<bool>(boolValue),
			_ => null,
		};
	}

	private FunctionDeclaration DeserializeFunctionDeclaration(object? nodeData)
	{
		var funcDecl = new FunctionDeclaration();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return funcDecl;
		}

		DeserializeFunctionBasicProperties(funcDecl, dict);
		DeserializeFunctionParameters(funcDecl, dict);
		DeserializeFunctionBody(funcDecl, dict);
		DeserializeMetadata(funcDecl, dict);
		DeserializeFunctionChildren(funcDecl, dict);

		return funcDecl;
	}

	private static void DeserializeFunctionBasicProperties(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		if (dict.TryGetValue("name", out var nameObj))
		{
			funcDecl.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("returnType", out var returnTypeObj))
		{
			funcDecl.ReturnType = returnTypeObj?.ToString();
		}
	}

	private static void DeserializeFunctionParameters(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("parameters", out var paramsObj) || paramsObj is not List<object> paramsList)
		{
			return;
		}

		foreach (var paramObj in paramsList)
		{
			if (paramObj is Dictionary<object, object> paramDict)
			{
				var param = DeserializeParameter(paramDict);
				if (param != null)
				{
					funcDecl.Parameters.Add(param);
				}
			}
		}
	}

	private void DeserializeFunctionBody(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("body", out var bodyObj) || bodyObj is not List<object> bodyList)
		{
			return;
		}

		foreach (var stmtObj in bodyList)
		{
			if (stmtObj is Dictionary<object, object> stmtDict)
			{
				foreach (var (stmtType, stmtData) in stmtDict)
				{
					var stmt = DeserializeNode(stmtType.ToString() ?? string.Empty, stmtData);
					if (stmt != null)
					{
						funcDecl.Body.Add(stmt);
					}
				}
			}
		}
	}

	private static void DeserializeMetadata(AstNode node, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("metadata", out var metadataObj) || metadataObj is not Dictionary<object, object> metadataDict)
		{
			return;
		}

		node.Metadata.Clear();
		foreach (var (key, value) in metadataDict)
		{
			node.Metadata[key.ToString() ?? string.Empty] = value;
		}
	}

	private void DeserializeFunctionChildren(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		var knownKeys = new HashSet<string> { "name", "returnType", "parameters", "body", "metadata" };

		foreach (var (key, value) in dict)
		{
			var keyString = key.ToString() ?? string.Empty;
			if (knownKeys.Contains(keyString) || value is not Dictionary<object, object> childDict)
			{
				continue;
			}

			foreach (var (childType, childData) in childDict)
			{
				var child = DeserializeNode(childType.ToString() ?? string.Empty, childData);
				if (child != null)
				{
					funcDecl.SetChild(keyString, child);
				}
			}
		}
	}

	private static Parameter DeserializeParameter(object? nodeData)
	{
		var param = new Parameter();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("name", out var nameObj))
			{
				param.Name = nameObj?.ToString();
			}

			if (dict.TryGetValue("type", out var typeObj))
			{
				param.Type = typeObj?.ToString();
			}

			if (dict.TryGetValue("isOptional", out var optionalObj) &&
				bool.TryParse(optionalObj?.ToString(), out var isOptional))
			{
				param.IsOptional = isOptional;

				if (dict.TryGetValue("defaultValue", out var defaultObj))
				{
					param.DefaultValue = defaultObj?.ToString();
				}
			}

			// Parse metadata if present
			DeserializeMetadata(param, dict);
		}

		return param;
	}

	private ReturnStatement DeserializeReturnStatement(object nodeData)
	{
		var returnStmt = new ReturnStatement();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("expression", out var exprObj) && exprObj is Dictionary<object, object> exprDict)
			{
				foreach (var (exprType, exprData) in exprDict)
				{
					var expr = DeserializeNode(exprType.ToString() ?? string.Empty, exprData);
					returnStmt.SetExpression(expr);
					break; // Only one expression in a return statement
				}
			}

			// Parse metadata if present
			DeserializeMetadata(returnStmt, dict);
		}

		return returnStmt;
	}

	[GeneratedRegex(@"Leaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();
}
