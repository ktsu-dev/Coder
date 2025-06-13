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
		Dictionary<string, object> rootDict = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
		if (rootDict == null || rootDict.Count == 0)
		{
			return null;
		}

		// Process the first node type found
		(string nodeType, object nodeData) = rootDict.First();
		return DeserializeNode(nodeType, nodeData);
	}

	private AstNode? DeserializeNode(string nodeType, object? nodeData)
	{
		return nodeType switch
		{
			"functionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"FunctionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"parameter" => DeserializeParameter(nodeData),
			"Parameter" => DeserializeParameter(nodeData),
			"returnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			"ReturnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			_ when nodeType.StartsWith("leaf<", StringComparison.OrdinalIgnoreCase) || nodeType.StartsWith("Leaf<", StringComparison.OrdinalIgnoreCase) => DeserializeLeafNode(nodeType, nodeData),
			_ => null,
		};
	}

	private static AstNode? DeserializeLeafNode(string nodeType, object? nodeData)
	{
		Match match = MyRegex().Match(nodeType);
		if (!match.Success || match.Groups.Count < 2)
		{
			return null;
		}

		string valueType = match.Groups[1].Value;

		return valueType switch
		{
			"String" => new AstLeafNode<string>(nodeData?.ToString() ?? string.Empty),
			"Int32" when int.TryParse(nodeData?.ToString(), out int intValue) => new AstLeafNode<int>(intValue),
			"Boolean" when bool.TryParse(nodeData?.ToString(), out bool boolValue) => new AstLeafNode<bool>(boolValue),
			_ => null,
		};
	}

	private FunctionDeclaration DeserializeFunctionDeclaration(object? nodeData)
	{
		FunctionDeclaration funcDecl = new();
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
		if (dict.TryGetValue("name", out object? nameObj))
		{
			funcDecl.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("returnType", out object? returnTypeObj))
		{
			funcDecl.ReturnType = returnTypeObj?.ToString();
		}
	}

	private static void DeserializeFunctionParameters(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("parameters", out object? paramsObj) || paramsObj is not List<object> paramsList)
		{
			return;
		}

		foreach (object paramObj in paramsList)
		{
			if (paramObj is Dictionary<object, object> paramDict)
			{
				Parameter param = DeserializeParameter(paramDict);
				if (param != null)
				{
					funcDecl.Parameters.Add(param);
				}
			}
		}
	}

	private void DeserializeFunctionBody(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("body", out object? bodyObj) || bodyObj is not List<object> bodyList)
		{
			return;
		}

		foreach (object stmtObj in bodyList)
		{
			if (stmtObj is Dictionary<object, object> stmtDict)
			{
				foreach ((object stmtType, object stmtData) in stmtDict)
				{
					AstNode? stmt = DeserializeNode(stmtType.ToString() ?? string.Empty, stmtData);
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
		if (!dict.TryGetValue("metadata", out object? metadataObj) || metadataObj is not Dictionary<object, object> metadataDict)
		{
			return;
		}

		node.Metadata.Clear();
		foreach ((object key, object value) in metadataDict)
		{
			node.Metadata[key.ToString() ?? string.Empty] = value;
		}
	}

	private void DeserializeFunctionChildren(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		HashSet<string> knownKeys = ["name", "returnType", "parameters", "body", "metadata"];

		foreach ((object key, object value) in dict)
		{
			string keyString = key.ToString() ?? string.Empty;
			if (knownKeys.Contains(keyString) || value is not Dictionary<object, object> childDict)
			{
				continue;
			}

			foreach ((object childType, object childData) in childDict)
			{
				AstNode? child = DeserializeNode(childType.ToString() ?? string.Empty, childData);
				if (child != null)
				{
					funcDecl.SetChild(keyString, child);
				}
			}
		}
	}

	private static Parameter DeserializeParameter(object? nodeData)
	{
		Parameter param = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("name", out object? nameObj))
			{
				param.Name = nameObj?.ToString();
			}

			if (dict.TryGetValue("type", out object? typeObj))
			{
				param.Type = typeObj?.ToString();
			}

			if (dict.TryGetValue("isOptional", out object? optionalObj) &&
				bool.TryParse(optionalObj?.ToString(), out bool isOptional))
			{
				param.IsOptional = isOptional;

				if (dict.TryGetValue("defaultValue", out object? defaultObj))
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
		ReturnStatement returnStmt = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("expression", out object? exprObj) && exprObj is Dictionary<object, object> exprDict)
			{
				foreach ((object exprType, object exprData) in exprDict)
				{
					AstNode? expr = DeserializeNode(exprType.ToString() ?? string.Empty, exprData);
					returnStmt.SetExpression(expr);
					break; // Only one expression in a return statement
				}
			}

			// Parse metadata if present
			DeserializeMetadata(returnStmt, dict);
		}

		return returnStmt;
	}

	[GeneratedRegex(@"[Ll]eaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();
}
