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
			"binaryExpression" => DeserializeBinaryExpression(nodeData),
			"BinaryExpression" => DeserializeBinaryExpression(nodeData),
			"variableReference" => DeserializeVariableReference(nodeData),
			"VariableReference" => DeserializeVariableReference(nodeData),
			"variableDeclaration" => DeserializeVariableDeclaration(nodeData),
			"VariableDeclaration" => DeserializeVariableDeclaration(nodeData),
			"assignmentStatement" => DeserializeAssignmentStatement(nodeData),
			"AssignmentStatement" => DeserializeAssignmentStatement(nodeData),
			_ when nodeType.StartsWith("literal<", StringComparison.OrdinalIgnoreCase) || nodeType.StartsWith("Literal<", StringComparison.OrdinalIgnoreCase) => DeserializeLiteralExpression(nodeType, nodeData),
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

	private BinaryExpression DeserializeBinaryExpression(object? nodeData)
	{
		BinaryExpression binaryExpr = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			// Deserialize left operand
			if (dict.TryGetValue("left", out object? leftObj) && leftObj is Dictionary<object, object> leftDict)
			{
				foreach ((object leftType, object leftData) in leftDict)
				{
					AstNode? leftNode = DeserializeNode(leftType.ToString() ?? string.Empty, leftData);
					if (leftNode is Expression leftExpr)
					{
						binaryExpr.Left = leftExpr;
						break;
					}
				}
			}

			// Deserialize operator
			if (dict.TryGetValue("operator", out object? operatorObj) &&
				Enum.TryParse<BinaryOperator>(operatorObj.ToString(), out BinaryOperator binaryOp))
			{
				binaryExpr.Operator = binaryOp;
			}

			// Deserialize right operand
			if (dict.TryGetValue("right", out object? rightObj) && rightObj is Dictionary<object, object> rightDict)
			{
				foreach ((object rightType, object rightData) in rightDict)
				{
					AstNode? rightNode = DeserializeNode(rightType.ToString() ?? string.Empty, rightData);
					if (rightNode is Expression rightExpr)
					{
						binaryExpr.Right = rightExpr;
						break;
					}
				}
			}

			// Deserialize expected type
			if (dict.TryGetValue("expectedType", out object? typeObj))
			{
				binaryExpr.ExpectedType = typeObj.ToString();
			}

			DeserializeMetadata(binaryExpr, dict);
		}

		return binaryExpr;
	}

	private static AstNode? DeserializeLiteralExpression(string nodeType, object? nodeData)
	{
		Match match = LiteralRegex().Match(nodeType);
		if (!match.Success || match.Groups.Count < 2)
		{
			return null;
		}

		string valueType = match.Groups[1].Value;
		if (nodeData is not Dictionary<object, object> dict)
		{
			return null;
		}

		if (!dict.TryGetValue("value", out object? value))
		{
			return null;
		}

		Expression? result = valueType switch
		{
			"String" => new LiteralExpression<string>(value?.ToString() ?? string.Empty),
			"Int32" when int.TryParse(value?.ToString(), out int intValue) => new LiteralExpression<int>(intValue),
			"Boolean" when bool.TryParse(value?.ToString(), out bool boolValue) => new LiteralExpression<bool>(boolValue),
			"Double" when double.TryParse(value?.ToString(), out double doubleValue) => new LiteralExpression<double>(doubleValue),
			_ => null,
		};

		if (result != null && dict.TryGetValue("expectedType", out object? typeObj))
		{
			result.ExpectedType = typeObj.ToString();
		}

		return result;
	}

	private static VariableReference DeserializeVariableReference(object? nodeData)
	{
		VariableReference varRef = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("name", out object? nameObj))
			{
				varRef.Name = nameObj?.ToString() ?? string.Empty;
			}

			if (dict.TryGetValue("expectedType", out object? typeObj))
			{
				varRef.ExpectedType = typeObj.ToString();
			}

			DeserializeMetadata(varRef, dict);
		}

		return varRef;
	}

	private VariableDeclaration DeserializeVariableDeclaration(object? nodeData)
	{
		VariableDeclaration varDecl = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			if (dict.TryGetValue("name", out object? nameObj))
			{
				varDecl.Name = nameObj?.ToString() ?? string.Empty;
			}

			if (dict.TryGetValue("type", out object? typeObj))
			{
				varDecl.Type = typeObj.ToString();
			}

			if (dict.TryGetValue("initialValue", out object? initialObj) && initialObj is Dictionary<object, object> initialDict)
			{
				foreach ((object initialType, object initialData) in initialDict)
				{
					AstNode? initialNode = DeserializeNode(initialType.ToString() ?? string.Empty, initialData);
					if (initialNode is Expression initialExpr)
					{
						varDecl.InitialValue = initialExpr;
						break;
					}
				}
			}

			if (dict.TryGetValue("isConstant", out object? constantObj) && bool.TryParse(constantObj.ToString(), out bool isConstant))
			{
				varDecl.IsConstant = isConstant;
			}

			if (dict.TryGetValue("isTypeInferred", out object? inferredObj) && bool.TryParse(inferredObj.ToString(), out bool isInferred))
			{
				varDecl.IsTypeInferred = isInferred;
			}

			if (dict.TryGetValue("accessModifier", out object? modifierObj))
			{
				varDecl.AccessModifier = modifierObj.ToString();
			}

			DeserializeMetadata(varDecl, dict);
		}

		return varDecl;
	}

	private AssignmentStatement DeserializeAssignmentStatement(object? nodeData)
	{
		AssignmentStatement assignment = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			// Deserialize target
			if (dict.TryGetValue("target", out object? targetObj) && targetObj is Dictionary<object, object> targetDict)
			{
				foreach ((object targetType, object targetData) in targetDict)
				{
					AstNode? targetNode = DeserializeNode(targetType.ToString() ?? string.Empty, targetData);
					if (targetNode is Expression targetExpr)
					{
						assignment.Target = targetExpr;
						break;
					}
				}
			}

			// Deserialize value
			if (dict.TryGetValue("value", out object? valueObj) && valueObj is Dictionary<object, object> valueDict)
			{
				foreach ((object valueType, object valueData) in valueDict)
				{
					AstNode? valueNode = DeserializeNode(valueType.ToString() ?? string.Empty, valueData);
					if (valueNode is Expression valueExpr)
					{
						assignment.Value = valueExpr;
						break;
					}
				}
			}

			// Deserialize operator
			if (dict.TryGetValue("operator", out object? operatorObj) &&
				Enum.TryParse<AssignmentOperator>(operatorObj.ToString(), out AssignmentOperator assignOp))
			{
				assignment.Operator = assignOp;
			}

			DeserializeMetadata(assignment, dict);
		}

		return assignment;
	}

	[GeneratedRegex(@"[Ll]eaf<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex MyRegex();

	[GeneratedRegex(@"[Ll]iteral<(\w+)>", RegexOptions.Compiled)]
	private static partial Regex LiteralRegex();
}
