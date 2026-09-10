// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Serialization;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ktsu.Coder.Ast;
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
			"sourceFile" => DeserializeSourceFile(nodeData),
			"SourceFile" => DeserializeSourceFile(nodeData),
			"namespaceDeclaration" => DeserializeNamespaceDeclaration(nodeData),
			"NamespaceDeclaration" => DeserializeNamespaceDeclaration(nodeData),
			"enumDeclaration" => DeserializeEnumDeclaration(nodeData),
			"EnumDeclaration" => DeserializeEnumDeclaration(nodeData),
			"enumMember" => DeserializeEnumMember(nodeData),
			"EnumMember" => DeserializeEnumMember(nodeData),
			"fieldDeclaration" => DeserializeFieldDeclaration(nodeData),
			"FieldDeclaration" => DeserializeFieldDeclaration(nodeData),
			"classDeclaration" => DeserializeClassDeclaration(nodeData),
			"ClassDeclaration" => DeserializeClassDeclaration(nodeData),
			"functionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"FunctionDeclaration" => DeserializeFunctionDeclaration(nodeData),
			"entryPoint" => DeserializeEntryPoint(nodeData),
			"EntryPoint" => DeserializeEntryPoint(nodeData),
			"parameter" => DeserializeParameter(nodeData),
			"Parameter" => DeserializeParameter(nodeData),
			"returnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			"ReturnStatement" => DeserializeReturnStatement(nodeData ?? new object()),
			"binaryExpression" => DeserializeBinaryExpression(nodeData),
			"BinaryExpression" => DeserializeBinaryExpression(nodeData),
			"unaryExpression" => DeserializeUnaryExpression(nodeData),
			"UnaryExpression" => DeserializeUnaryExpression(nodeData),
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

		if (dict.TryGetValue("isStatic", out object? staticObj) &&
			bool.TryParse(staticObj?.ToString(), out bool isStatic))
		{
			funcDecl.IsStatic = isStatic;
		}

		if (dict.TryGetValue("isPure", out object? pureObj) &&
			bool.TryParse(pureObj?.ToString(), out bool isPure))
		{
			funcDecl.IsPure = isPure;
		}

		if (dict.TryGetValue("kind", out object? kindObj) &&
			Enum.TryParse(kindObj?.ToString(), out FunctionKind kind))
		{
			funcDecl.Kind = kind;
		}

		if (dict.TryGetValue("definition", out object? definitionObj) &&
			Enum.TryParse(definitionObj?.ToString(), out FunctionDefinition definition))
		{
			funcDecl.Definition = definition;
		}

		funcDecl.IsVirtual = ReadFlag(dict, "isVirtual", funcDecl.IsVirtual);
		funcDecl.IsAbstract = ReadFlag(dict, "isAbstract", funcDecl.IsAbstract);
		funcDecl.IsReadOnly = ReadFlag(dict, "isReadOnly", funcDecl.IsReadOnly);
		funcDecl.MustUseResult = ReadFlag(dict, "mustUseResult", funcDecl.MustUseResult);

		DeserializeVisibility(funcDecl, dict);
		ReadStrings(dict, "documentation", funcDecl.Documentation);
	}

	/// <summary>
	/// Reads a boolean, leaving it as it was when the document does not say.
	/// </summary>
	/// <param name="dict">The mapping the node was written as.</param>
	/// <param name="key">The key to read.</param>
	/// <param name="fallback">What to return when the document is silent.</param>
	/// <returns>The value read, or the fallback.</returns>
	private static bool ReadFlag(Dictionary<object, object> dict, string key, bool fallback) =>
		dict.TryGetValue(key, out object? value) && bool.TryParse(value?.ToString(), out bool flag)
			? flag
			: fallback;

	/// <summary>
	/// Reads a declaration's visibility, leaving it <see cref="Visibility.Unspecified"/> when the
	/// document does not say.
	/// </summary>
	/// <param name="declaration">The declaration being read into.</param>
	/// <param name="dict">The mapping the node was written as.</param>
	/// <remarks>
	/// <c>accessModifier</c> is read as well as <c>visibility</c>: documents written before visibility
	/// became an enumeration spell it that way, and a saved document should not stop opening because
	/// the library changed how it models the same idea.
	/// </remarks>
	private static void DeserializeVisibility(IHasVisibility declaration, Dictionary<object, object> dict)
	{
		if ((dict.TryGetValue("visibility", out object? visibilityObj) || dict.TryGetValue("accessModifier", out visibilityObj))
			&& Enum.TryParse(visibilityObj?.ToString(), ignoreCase: true, out Visibility visibility))
		{
			declaration.Visibility = visibility;
		}
	}

	private EntryPoint DeserializeEntryPoint(object? nodeData)
	{
		EntryPoint entryPoint = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return entryPoint;
		}

		if (dict.TryGetValue("acceptsArguments", out object? argumentsObj)
			&& bool.TryParse(argumentsObj?.ToString(), out bool acceptsArguments))
		{
			entryPoint.AcceptsArguments = acceptsArguments;
		}

		if (dict.TryGetValue("returnsExitCode", out object? exitCodeObj)
			&& bool.TryParse(exitCodeObj?.ToString(), out bool returnsExitCode))
		{
			entryPoint.ReturnsExitCode = returnsExitCode;
		}

		if (dict.TryGetValue("body", out object? bodyObj) && bodyObj is List<object> bodyList)
		{
			foreach (object statementObj in bodyList)
			{
				if (statementObj is not Dictionary<object, object> statementDict)
				{
					continue;
				}

				foreach ((object statementType, object statementData) in statementDict)
				{
					AstNode? statement = DeserializeNode(statementType.ToString() ?? string.Empty, statementData);
					if (statement != null)
					{
						entryPoint.Body.Add(statement);
					}
				}
			}
		}

		DeserializeMetadata(entryPoint, dict);

		return entryPoint;
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

	private SourceFile DeserializeSourceFile(object? nodeData)
	{
		SourceFile file = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return file;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			file.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("isHeader", out object? headerObj) &&
			bool.TryParse(headerObj?.ToString(), out bool isHeader))
		{
			file.IsHeader = isHeader;
		}

		ReadStrings(dict, "headerComment", file.HeaderComment);
		ReadStrings(dict, "imports", file.Imports);
		DeserializeMembersInto(dict, file.Members);
		DeserializeMetadata(file, dict);

		return file;
	}

	private NamespaceDeclaration DeserializeNamespaceDeclaration(object? nodeData)
	{
		NamespaceDeclaration namespaceDecl = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return namespaceDecl;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			namespaceDecl.Name = nameObj?.ToString();
		}

		ReadStrings(dict, "documentation", namespaceDecl.Documentation);
		DeserializeMembersInto(dict, namespaceDecl.Members);
		DeserializeMetadata(namespaceDecl, dict);

		return namespaceDecl;
	}

	private EnumDeclaration DeserializeEnumDeclaration(object? nodeData)
	{
		EnumDeclaration enumDecl = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return enumDecl;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			enumDecl.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("underlyingType", out object? underlyingObj))
		{
			enumDecl.UnderlyingType = underlyingObj?.ToString();
		}

		DeserializeVisibility(enumDecl, dict);
		ReadStrings(dict, "documentation", enumDecl.Documentation);

		if (dict.TryGetValue("members", out object? membersObj) && membersObj is List<object> members)
		{
			foreach (object member in members)
			{
				if (member is Dictionary<object, object> memberDict && memberDict.Count > 0)
				{
					(object memberType, object memberData) = memberDict.First();
					if (DeserializeNode(memberType.ToString() ?? string.Empty, memberData) is EnumMember value)
					{
						enumDecl.Members.Add(value);
					}
				}
			}
		}

		DeserializeMetadata(enumDecl, dict);
		return enumDecl;
	}

	private static EnumMember DeserializeEnumMember(object? nodeData)
	{
		EnumMember member = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return member;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			member.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("value", out object? valueObj))
		{
			member.Value = valueObj?.ToString();
		}

		DeserializeMetadata(member, dict);
		return member;
	}

	private FieldDeclaration DeserializeFieldDeclaration(object? nodeData)
	{
		FieldDeclaration field = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return field;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			field.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("type", out object? typeObj))
		{
			field.Type = typeObj?.ToString();
		}

		DeserializeVisibility(field, dict);
		ReadStrings(dict, "documentation", field.Documentation);

		if (dict.TryGetValue("initialValue", out object? initialObj) &&
			initialObj is Dictionary<object, object> initialDict && initialDict.Count > 0)
		{
			(object valueType, object valueData) = initialDict.First();
			field.InitialValue = DeserializeNode(valueType.ToString() ?? string.Empty, valueData) as Expression;
		}

		DeserializeMetadata(field, dict);
		return field;
	}

	/// <summary>
	/// Reads a sequence of strings into a collection, leaving it alone when the key is absent.
	/// </summary>
	/// <param name="dict">The mapping the node was written as.</param>
	/// <param name="key">The key to read.</param>
	/// <param name="into">The collection to fill.</param>
	private static void ReadStrings(Dictionary<object, object> dict, string key, Collection<string> into)
	{
		if (dict.TryGetValue(key, out object? value) && value is List<object> lines)
		{
			foreach (object line in lines)
			{
				into.Add(line?.ToString() ?? string.Empty);
			}
		}
	}

	/// <summary>
	/// Reads a node's members into a collection.
	/// </summary>
	/// <param name="dict">The mapping the node was written as.</param>
	/// <param name="into">The collection to fill.</param>
	private void DeserializeMembersInto(Dictionary<object, object> dict, Collection<AstNode> into)
	{
		if (!dict.TryGetValue("members", out object? membersObj) || membersObj is not List<object> members)
		{
			return;
		}

		foreach (object member in members)
		{
			if (member is Dictionary<object, object> memberDict && memberDict.Count > 0)
			{
				(object memberType, object memberData) = memberDict.First();
				if (DeserializeNode(memberType.ToString() ?? string.Empty, memberData) is AstNode node)
				{
					into.Add(node);
				}
			}
		}
	}

	private ClassDeclaration DeserializeClassDeclaration(object? nodeData)
	{
		ClassDeclaration classDecl = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return classDecl;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			classDecl.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("baseType", out object? baseTypeObj))
		{
			classDecl.BaseType = baseTypeObj?.ToString();
		}

		if (dict.TryGetValue("kind", out object? kindObj) &&
			Enum.TryParse(kindObj?.ToString(), out TypeDeclarationKind kind))
		{
			classDecl.Kind = kind;
		}

		DeserializeVisibility(classDecl, dict);
		ReadStrings(dict, "documentation", classDecl.Documentation);

		DeserializeClassMembers(classDecl, dict);
		DeserializeMetadata(classDecl, dict);

		return classDecl;
	}

	private void DeserializeClassMembers(ClassDeclaration classDecl, Dictionary<object, object> dict)
	{
		if (!dict.TryGetValue("members", out object? membersObj) || membersObj is not List<object> memberList)
		{
			return;
		}

		foreach (object memberObj in memberList)
		{
			if (memberObj is not Dictionary<object, object> memberDict)
			{
				continue;
			}

			foreach ((object memberType, object memberData) in memberDict)
			{
				AstNode? member = DeserializeNode(memberType.ToString() ?? string.Empty, memberData);
				if (member != null)
				{
					classDecl.Members.Add(member);
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
		HashSet<string> knownKeys = ["name", "returnType", "visibility", "accessModifier", "parameters", "body", "metadata"];

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

	private UnaryExpression DeserializeUnaryExpression(object? nodeData)
	{
		UnaryExpression unaryExpr = new();
		if (nodeData is Dictionary<object, object> dict)
		{
			// Deserialize operator
			if (dict.TryGetValue("operator", out object? operatorObj) &&
				Enum.TryParse<UnaryOperator>(operatorObj.ToString(), out UnaryOperator unaryOp))
			{
				unaryExpr.Operator = unaryOp;
			}

			// Deserialize operand
			if (dict.TryGetValue("operand", out object? operandObj) && operandObj is Dictionary<object, object> operandDict)
			{
				foreach ((object operandType, object operandData) in operandDict)
				{
					AstNode? operandNode = DeserializeNode(operandType.ToString() ?? string.Empty, operandData);
					if (operandNode is Expression operandExpr)
					{
						unaryExpr.Operand = operandExpr;
						break;
					}
				}
			}

			// Deserialize expected type
			if (dict.TryGetValue("expectedType", out object? typeObj))
			{
				unaryExpr.ExpectedType = typeObj.ToString();
			}

			DeserializeMetadata(unaryExpr, dict);
		}

		return unaryExpr;
	}

	private static Expression? DeserializeLiteralExpression(string nodeType, object? nodeData)
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

			DeserializeVisibility(varDecl, dict);

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
