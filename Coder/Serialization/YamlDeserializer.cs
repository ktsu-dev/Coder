// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Serialization;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
			"compileTimeAssertion" => DeserializeCompileTimeAssertion(nodeData),
			"CompileTimeAssertion" => DeserializeCompileTimeAssertion(nodeData),
			"usingAlias" => DeserializeUsingAlias(nodeData),
			"UsingAlias" => DeserializeUsingAlias(nodeData),
			"memberInitialiser" => DeserializeMemberInitialiser(nodeData),
			"MemberInitialiser" => DeserializeMemberInitialiser(nodeData),
			"constructionExpression" => DeserializeConstructionExpression(nodeData),
			"ConstructionExpression" => DeserializeConstructionExpression(nodeData),
			"callExpression" => DeserializeCallExpression(nodeData),
			"CallExpression" => DeserializeCallExpression(nodeData),
			"conditionalExpression" => DeserializeConditionalExpression(nodeData),
			"ConditionalExpression" => DeserializeConditionalExpression(nodeData),
			"expressionStatement" => DeserializeExpressionStatement(nodeData),
			"ExpressionStatement" => DeserializeExpressionStatement(nodeData),
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

	private void DeserializeFunctionBasicProperties(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
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

		ReadFunctionShape(funcDecl, dict);

		DeserializeVisibility(funcDecl, dict);
		ReadStrings(dict, DocumentationKey, funcDecl.Documentation);
		DeserializeTypeParameters(dict, funcDecl.TypeParameters);
	}

	/// <summary>
	/// Reads what a function declares and how, leaving each as it was when the document is silent.
	/// </summary>
	/// <param name="funcDecl">The declaration being read into.</param>
	/// <param name="dict">The mapping the node was written as.</param>
	/// <remarks>
	/// Separate from the name and the return type only because one method reading every property a
	/// declaration has is more branches than the analyzer accepts. These are the ones that say what
	/// kind of declaration it is rather than what it is called.
	/// </remarks>
	private void ReadFunctionShape(FunctionDeclaration funcDecl, Dictionary<object, object> dict)
	{
		funcDecl.IsVirtual = ReadFlag(dict, "isVirtual", funcDecl.IsVirtual);
		funcDecl.IsAbstract = ReadFlag(dict, "isAbstract", funcDecl.IsAbstract);
		funcDecl.IsReadOnly = ReadFlag(dict, "isReadOnly", funcDecl.IsReadOnly);
		funcDecl.MustUseResult = ReadFlag(dict, "mustUseResult", funcDecl.MustUseResult);
		funcDecl.IsExplicit = ReadFlag(dict, "isExplicit", funcDecl.IsExplicit);
		funcDecl.IsCompileTimeEvaluable = ReadFlag(dict, "isCompileTimeEvaluable", funcDecl.IsCompileTimeEvaluable);
		funcDecl.IsNoThrow = ReadFlag(dict, "isNoThrow", funcDecl.IsNoThrow);
		funcDecl.IsFriend = ReadFlag(dict, "isFriend", funcDecl.IsFriend);

		if (dict.TryGetValue("initialisers", out object? initialisersObj) && initialisersObj is List<object> initialisers)
		{
			foreach (Dictionary<object, object> initialiserDict in Mappings(initialisers))
			{
				(object initialiserType, object initialiserData) = initialiserDict.First();
				if (DeserializeNode(initialiserType.ToString() ?? string.Empty, initialiserData) is MemberInitialiser member)
				{
					funcDecl.Initialisers.Add(member);
				}
			}
		}
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

		ReadStrings(dict, DocumentationKey, namespaceDecl.Documentation);
		DeserializeMembersInto(dict, namespaceDecl.Members);
		DeserializeMetadata(namespaceDecl, dict);

		return namespaceDecl;
	}

	private static CompileTimeAssertion DeserializeCompileTimeAssertion(object? nodeData)
	{
		CompileTimeAssertion assertion = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return assertion;
		}

		if (dict.TryGetValue("condition", out object? conditionObj))
		{
			assertion.Condition = conditionObj?.ToString();
		}

		if (dict.TryGetValue("message", out object? messageObj))
		{
			assertion.Message = messageObj?.ToString();
		}

		DeserializeMetadata(assertion, dict);
		return assertion;
	}

	private static UsingAlias DeserializeUsingAlias(object? nodeData)
	{
		UsingAlias usingAlias = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return usingAlias;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			usingAlias.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue("aliasedType", out object? typeObj))
		{
			usingAlias.AliasedType = typeObj?.ToString();
		}

		DeserializeVisibility(usingAlias, dict);
		ReadStrings(dict, DocumentationKey, usingAlias.Documentation);
		DeserializeMetadata(usingAlias, dict);

		return usingAlias;
	}

	private MemberInitialiser DeserializeMemberInitialiser(object? nodeData)
	{
		MemberInitialiser initialiser = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return initialiser;
		}

		if (dict.TryGetValue("name", out object? nameObj))
		{
			initialiser.Name = nameObj?.ToString();
		}

		if (dict.TryGetValue(ValueKey, out object? valueObj) &&
			valueObj is Dictionary<object, object> valueDict && valueDict.Count > 0)
		{
			(object valueType, object valueData) = valueDict.First();
			initialiser.Value = DeserializeNode(valueType.ToString() ?? string.Empty, valueData) as Expression;
		}

		DeserializeMetadata(initialiser, dict);
		return initialiser;
	}

	private ConstructionExpression DeserializeConstructionExpression(object? nodeData)
	{
		ConstructionExpression construction = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return construction;
		}

		if (dict.TryGetValue("type", out object? typeObj))
		{
			construction.Type = typeObj?.ToString();
		}

		if (dict.TryGetValue("arguments", out object? argumentsObj) && argumentsObj is List<object> arguments)
		{
			foreach (Dictionary<object, object> argumentDict in Mappings(arguments))
			{
				(object argumentType, object argumentData) = argumentDict.First();
				if (DeserializeNode(argumentType.ToString() ?? string.Empty, argumentData) is AstNode node)
				{
					construction.Arguments.Add(node);
				}
			}
		}

		DeserializeMetadata(construction, dict);
		return construction;
	}

	/// <summary>
	/// Reads back an expression written as a single-entry mapping of node type to node data.
	/// </summary>
	/// <param name="value">The mapping, as the deserializer produced it.</param>
	/// <returns>The expression, or null when the mapping holds nothing that is one.</returns>
	private Expression? DeserializeNestedExpression(object? value)
	{
		if (value is not Dictionary<object, object> dict)
		{
			return null;
		}

		foreach ((object nodeType, object nodeData) in dict)
		{
			if (DeserializeNode(nodeType.ToString() ?? string.Empty, nodeData) is Expression expression)
			{
				return expression;
			}
		}

		return null;
	}

	private CallExpression DeserializeCallExpression(object? nodeData)
	{
		CallExpression callExpr = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return callExpr;
		}

		if (dict.TryGetValue("callee", out object? calleeObj))
		{
			callExpr.Callee = calleeObj?.ToString() ?? string.Empty;
		}

		if (dict.TryGetValue("receiver", out object? receiverObj))
		{
			callExpr.Receiver = DeserializeNestedExpression(receiverObj);
		}

		if (dict.TryGetValue("arguments", out object? argumentsObj) && argumentsObj is List<object> arguments)
		{
			foreach (Dictionary<object, object> argumentDict in Mappings(arguments))
			{
				(object argumentType, object argumentData) = argumentDict.First();
				if (DeserializeNode(argumentType.ToString() ?? string.Empty, argumentData) is AstNode node)
				{
					callExpr.Arguments.Add(node);
				}
			}
		}

		if (dict.TryGetValue("expectedType", out object? typeObj))
		{
			callExpr.ExpectedType = typeObj?.ToString();
		}

		DeserializeMetadata(callExpr, dict);
		return callExpr;
	}

	private ConditionalExpression DeserializeConditionalExpression(object? nodeData)
	{
		ConditionalExpression conditional = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return conditional;
		}

		if (dict.TryGetValue("condition", out object? conditionObj) &&
			DeserializeNestedExpression(conditionObj) is Expression condition)
		{
			conditional.Condition = condition;
		}

		if (dict.TryGetValue("whenTrue", out object? whenTrueObj) &&
			DeserializeNestedExpression(whenTrueObj) is Expression whenTrue)
		{
			conditional.WhenTrue = whenTrue;
		}

		if (dict.TryGetValue("whenFalse", out object? whenFalseObj) &&
			DeserializeNestedExpression(whenFalseObj) is Expression whenFalse)
		{
			conditional.WhenFalse = whenFalse;
		}

		if (dict.TryGetValue("expectedType", out object? typeObj))
		{
			conditional.ExpectedType = typeObj?.ToString();
		}

		DeserializeMetadata(conditional, dict);
		return conditional;
	}

	private ExpressionStatement DeserializeExpressionStatement(object? nodeData)
	{
		ExpressionStatement statement = new();
		if (nodeData is not Dictionary<object, object> dict)
		{
			return statement;
		}

		if (dict.TryGetValue("expression", out object? expressionObj) &&
			DeserializeNestedExpression(expressionObj) is Expression expression)
		{
			statement.Expression = expression;
		}

		DeserializeMetadata(statement, dict);
		return statement;
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
		ReadStrings(dict, DocumentationKey, enumDecl.Documentation);

		if (dict.TryGetValue(MembersKey, out object? membersObj) && membersObj is List<object> members)
		{
			foreach (Dictionary<object, object> memberDict in Mappings(members))
			{
				(object memberType, object memberData) = memberDict.First();
				if (DeserializeNode(memberType.ToString() ?? string.Empty, memberData) is EnumMember value)
				{
					enumDecl.Members.Add(value);
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

		if (dict.TryGetValue(ValueKey, out object? valueObj))
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

		field.IsStatic = ReadFlag(dict, "isStatic", field.IsStatic);
		field.IsConstant = ReadFlag(dict, "isConstant", field.IsConstant);

		DeserializeVisibility(field, dict);
		ReadStrings(dict, DocumentationKey, field.Documentation);

		if (dict.TryGetValue("initialValue", out object? initialObj) &&
			initialObj is Dictionary<object, object> initialDict && initialDict.Count > 0)
		{
			(object valueType, object valueData) = initialDict.First();
			field.InitialValue = DeserializeNode(valueType.ToString() ?? string.Empty, valueData) as Expression;
		}

		DeserializeMetadata(field, dict);
		return field;
	}

	/// <summary>The key a node's members are written under.</summary>
	private const string MembersKey = "members";

	/// <summary>The key a node's single value is written under.</summary>
	private const string ValueKey = "value";

	/// <summary>The key a declaration's documentation is written under.</summary>
	private const string DocumentationKey = "documentation";

	/// <summary>
	/// Keeps the entries of a sequence that are non-empty mappings, which is the only shape a node
	/// can have been written as.
	/// </summary>
	/// <param name="entries">The sequence read from the document.</param>
	/// <returns>The entries that are nodes.</returns>
	/// <remarks>
	/// Filtering here rather than inside each loop says out loud that anything else in the sequence is
	/// skipped — a document can hold whatever someone typed, and a loop that quietly steps over half
	/// its input reads as though it does not.
	/// </remarks>
	private static IEnumerable<Dictionary<object, object>> Mappings(List<object> entries) =>
		entries.OfType<Dictionary<object, object>>().Where(entry => entry.Count > 0);

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
		if (!dict.TryGetValue(MembersKey, out object? membersObj) || membersObj is not List<object> members)
		{
			return;
		}

		foreach (Dictionary<object, object> memberDict in Mappings(members))
		{
			(object memberType, object memberData) = memberDict.First();
			if (DeserializeNode(memberType.ToString() ?? string.Empty, memberData) is AstNode node)
			{
				into.Add(node);
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

		classDecl.IsRecord = ReadFlag(dict, "record", classDecl.IsRecord);
		classDecl.IsPartial = ReadFlag(dict, "partial", classDecl.IsPartial);
		classDecl.IsReadOnly = ReadFlag(dict, "readOnly", classDecl.IsReadOnly);

		DeserializeVisibility(classDecl, dict);
		ReadStrings(dict, DocumentationKey, classDecl.Documentation);
		DeserializeTypeParameters(dict, classDecl.TypeParameters);
		DeserializeTypeList(dict, "interfaces", classDecl.Interfaces);
		DeserializeTypeList(dict, "specialisationArguments", classDecl.SpecialisationArguments);

		DeserializeClassMembers(classDecl, dict);
		DeserializeMetadata(classDecl, dict);

		return classDecl;
	}

	/// <summary>
	/// Reads a sequence of written type parameters into a collection.
	/// </summary>
	/// <param name="dict">The mapping to read from.</param>
	/// <param name="parameters">The collection to fill.</param>
	/// <remarks>
	/// One entry per parameter, carrying its constraints with it, because
	/// <see cref="TypeParameter.Parse"/> and <see cref="TypeParameter.ToString"/> are inverses and
	/// a parameter written on one line is a parameter a person can read.
	/// </remarks>
	private static void DeserializeTypeParameters(Dictionary<object, object> dict, Collection<TypeParameter> parameters)
	{
		if (!dict.TryGetValue("typeParameters", out object? writtenObj) || writtenObj is not List<object> written)
		{
			return;
		}

		IEnumerable<string> spelled = written
			.Select(parameter => parameter?.ToString() ?? string.Empty)
			.Where(text => text.Length > 0);

		foreach (string text in spelled)
		{
			parameters.Add(TypeParameter.Parse(text));
		}
	}

	/// <summary>
	/// Reads a sequence of written types into a collection.
	/// </summary>
	/// <param name="dict">The mapping to read from.</param>
	/// <param name="key">The key the sequence is written under.</param>
	/// <param name="types">The collection to fill.</param>
	/// <remarks>
	/// Both of a class declaration's type lists are read this way, and each is written as a
	/// sequence rather than one joined string for the same reason: a type argument can itself have
	/// type arguments, so a comma inside one is part of it as often as it separates two.
	/// </remarks>
	private static void DeserializeTypeList(Dictionary<object, object> dict, string key, Collection<TypeReference> types)
	{
		if (!dict.TryGetValue(key, out object? writtenObj) || writtenObj is not List<object> written)
		{
			return;
		}

		// A null or empty entry is not a type. Filtering before the loop rather than inside it so
		// that what the loop takes is what the loop does.
		IEnumerable<string> spelled = written
			.Select(type => type?.ToString() ?? string.Empty)
			.Where(text => text.Length > 0);

		foreach (string text in spelled)
		{
			types.Add(TypeReference.Parse(text));
		}
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

		if (!dict.TryGetValue(ValueKey, out object? value))
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
			if (dict.TryGetValue(ValueKey, out object? valueObj) && valueObj is Dictionary<object, object> valueDict)
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
