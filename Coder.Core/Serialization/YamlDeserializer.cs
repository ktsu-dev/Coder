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
/// Handles deserialization from YAML format to AST nodes.
/// </summary>
public partial partial class YamlDeserializer
{
	private readonly IDeserializer _deserializer;
	private static readonly Regex _leafNodeRegex = MyRegex();

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
	public AstNode Deserialize(string yaml)
	{
		if (string.IsNullOrWhiteSpace(yaml))
		{
			throw new ArgumentException("YAML string cannot be null or whitespace.", nameof(yaml));
		}

		// Deserialize YAML to a dictionary
		var root = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
		if (root == null || root.Count == 0)
		{
			throw new InvalidOperationException("Deserialized YAML is empty.");
		}

		// There should be only one root node type
		var (nodeTypeName, nodeData) = root.First();
		return DeserializeNode(nodeTypeName, nodeData);
	}

	private AstNode DeserializeNode(string nodeTypeName, object nodeData)
	{
		if (nodeTypeName == "ReturnStatement")
		{
			return DeserializeReturnStatement(nodeData);
		}
		else if (nodeTypeName == "FunctionDeclaration")
		{
			return DeserializeFunctionDeclaration(nodeData);
		}
		else if (nodeTypeName == "Parameter")
		{
			return DeserializeParameter(nodeData);
		}
		else
		{
			// Check if it's a leaf node
			var match = _leafNodeRegex.Match(nodeTypeName);
			if (match.Success)
			{
				return DeserializeLeafNode(match.Groups[1].Value, nodeData);
			}
		}

		throw new NotSupportedException($"Unsupported node type: {nodeTypeName}");
	}

	private static AstNode DeserializeLeafNode(string typeNameStr, object value)
	{
		if (typeNameStr == "String")
		{
			return new AstLeafNode<string>(value?.ToString() ?? string.Empty);
		}
		else if (typeNameStr == "Int32" && value != null)
		{
			if (value is int intValue)
			{
				return new AstLeafNode<int>(intValue);
			}

			if (int.TryParse(value.ToString(), out var parsedInt))
			{
				return new AstLeafNode<int>(parsedInt);
			}
		}
		else if (typeNameStr == "Boolean" && value != null)
		{
			if (value is bool boolValue)
			{
				return new AstLeafNode<bool>(boolValue);
			}

			if (bool.TryParse(value.ToString(), out var parsedBool))
			{
				return new AstLeafNode<bool>(parsedBool);
			}
		}

		throw new NotSupportedException($"Unsupported leaf node type: {typeNameStr}");
	}

	private FunctionDeclaration DeserializeFunctionDeclaration(object nodeData)
	{
		var funcDecl = new FunctionDeclaration();
		if (nodeData is Dictionary<object, object> dict)
		{
			// Get the basic properties
			if (dict.TryGetValue("name", out var nameObj))
			{
				funcDecl.Name = nameObj?.ToString();
			}

			if (dict.TryGetValue("returnType", out var returnTypeObj))
			{
				funcDecl.ReturnType = returnTypeObj?.ToString();
			}

			// Parse parameters
			if (dict.TryGetValue("parameters", out var paramsObj) && paramsObj is List<object> paramsList)
			{
				foreach (var paramObj in paramsList)
				{
					if (paramObj is Dictionary<object, object> paramDict)
					{
						var param = new Parameter();

						if (paramDict.TryGetValue("name", out var paramNameObj))
						{
							param.Name = paramNameObj?.ToString();
						}

						if (paramDict.TryGetValue("type", out var paramTypeObj))
						{
							param.Type = paramTypeObj?.ToString();
						}

						if (paramDict.TryGetValue("isOptional", out var optionalObj) &&
							bool.TryParse(optionalObj?.ToString(), out var isOptional))
						{
							param.IsOptional = isOptional;

							if (paramDict.TryGetValue("defaultValue", out var defaultObj))
							{
								param.DefaultValue = defaultObj?.ToString();
							}
						}

						funcDecl.Parameters.Add(param);
					}
				}
			}

			// Parse body
			if (dict.TryGetValue("body", out var bodyObj) && bodyObj is List<object> bodyList)
			{
				foreach (var stmtObj in bodyList)
				{
					if (stmtObj is Dictionary<object, object> stmtDict)
					{
						foreach (var (stmtType, stmtData) in stmtDict)
						{
							var stmt = DeserializeNode(stmtType.ToString() ?? string.Empty, stmtData);
							funcDecl.Body.Add(stmt);
						}
					}
				}
			}

			// Parse metadata if present
			if (dict.TryGetValue("metadata", out var metadataObj) &&
				metadataObj is Dictionary<object, object> metadataDict)
			{
				funcDecl.Metadata = [];
				foreach (var (key, value) in metadataDict)
				{
					funcDecl.Metadata[key.ToString() ?? string.Empty] = value;
				}
			}
		}

		return funcDecl;
	}

	private static Parameter DeserializeParameter(object nodeData)
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

<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
    }
} 
=======
	}

	[GeneratedRegex(@"Leaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();
}
>>>>>>> After
	}

	[GeneratedRegex(@"Leaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();
}
