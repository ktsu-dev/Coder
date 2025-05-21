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
			"functionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"parameter" => DeserializeParameter(nodeData),
			"returnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			_ when nodeType.StartsWith("leaf<", StringComparison.OrdinalIgnoreCase) => DeserializeLeafNode(nodeType, nodeData),
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
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("name", out var nameObj))
			{
				funcDecl.Name = nameObj?.ToString();
			}

			if (dict.TryGetValue("returnType", out var returnTypeObj))
			{
				funcDecl.ReturnType = returnTypeObj?.ToString();
			}

			// Deserialize parameters if present
			if (dict.TryGetValue("parameters", out var paramsObj) && paramsObj is List<object> paramsList)
			{
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

			// Deserialize body statements if present
			if (dict.TryGetValue("body", out var bodyObj) && bodyObj is List<object> bodyList)
			{
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

			// Parse metadata if present
			if (dict.TryGetValue("metadata", out var metadataObj) &&
				metadataObj is Dictionary<object, object> metadataDict)
			{
				funcDecl.Metadata.Clear();
				foreach (var (key, value) in metadataDict)
				{
					funcDecl.Metadata[key.ToString() ?? string.Empty] = value;
				}
			}

			// Deserialize other children from the dictionary
			foreach (var (key, value) in dict)
			{
				if (key.ToString() != "name" &&
					key.ToString() != "returnType" &&
					key.ToString() != "parameters" &&
					key.ToString() != "body" &&
					key.ToString() != "metadata" &&
					value is Dictionary<object, object> childDict)
				{
					foreach (var (childType, childData) in childDict)
					{
						var child = DeserializeNode(childType.ToString() ?? string.Empty, childData);
						if (child != null)
						{
							funcDecl.SetChild(key.ToString() ?? string.Empty, child);
						}
					}
				}
			}
		}

		return funcDecl;
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
			if (dict.TryGetValue("metadata", out var metadataObj) &&
				metadataObj is Dictionary<object, object> metadataDict)
			{
				param.Metadata = [];
				foreach (var (key, value) in metadataDict)
				{
					param.Metadata[key.ToString() ?? string.Empty] = value;
				}
			}
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
			if (dict.TryGetValue("metadata", out var metadataObj) &&
				metadataObj is Dictionary<object, object> metadataDict)
			{
				returnStmt.Metadata = [];
				foreach (var (key, value) in metadataDict)
				{
					returnStmt.Metadata[key.ToString() ?? string.Empty] = value;
				}
			}
		}

		return returnStmt;
	}

	[GeneratedRegex(@"Leaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();
}
