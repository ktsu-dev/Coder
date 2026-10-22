// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// A <see cref="LanguageGeneratorBase"/> that owns the node dispatch, so a generator supplies only
/// the syntax its language does not share with the others.
/// </summary>
/// <remarks>
/// Every generator's dispatch was the same switch: structural nodes to their emitters, everything
/// else to <see cref="LanguageGeneratorBase.TryGenerateCommonNode"/>, an unrecognised node to a
/// <see cref="NotSupportedException"/>. Only the operator spelling and the language's name in that
/// message differed. It lives here once, and a derived generator writes three methods —
/// <see cref="GenerateFunctionDeclaration"/>, <see cref="GenerateVariableDeclaration"/> and
/// <see cref="GenerateParameter"/> — plus an operator override when its language needs one.
/// <para>
/// <c>CSharpGenerator</c> deliberately does not derive from this. It indents with a precomputed
/// string rather than a level, emits a comment instead of throwing for an unrecognised node, and
/// stringifies a return expression rather than recursing into it. Those are differences to settle on
/// their own terms, not to smuggle in here.
/// </para>
/// </remarks>
public abstract class StandardLanguageGenerator : LanguageGeneratorBase
{
	private readonly Stack<StaticTypeScope> staticTypeScopes = new();

	/// <summary>
	/// The variables declared directly in a namespace or a file, which are items rather than locals.
	/// </summary>
	private readonly HashSet<VariableDeclaration> moduleVariables = new(ReferenceEqualityComparer.Instance);

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public override bool CanGenerate(AstNode astNode) => CanGenerateStandardNodes(astNode);

	/// <summary>
	/// Finds non-static fields whose initial values the target must write during construction.
	/// </summary>
	/// <param name="declaration">The type whose instance fields are being considered.</param>
	/// <param name="needsConstruction">Whether the field's initializer belongs in its constructor.</param>
	/// <returns>The fields to initialize per instance.</returns>
	protected static IReadOnlyList<FieldDeclaration> InstanceInitialised(
		ClassDeclaration declaration,
		Func<FieldDeclaration, bool> needsConstruction)
	{
		Ensure.NotNull(declaration);
		Ensure.NotNull(needsConstruction);
		return [.. declaration.Members
			.OfType<FieldDeclaration>()
			.Where(field => !field.IsStatic && !field.IsConstant && field.InitialValue is not null && needsConstruction(field))];
	}

	/// <summary>
	/// Finds every instance member that says what it starts at, for the targets whose constructors
	/// have to start it there because the member cannot say so itself.
	/// </summary>
	/// <param name="declaration">The type whose instance members are being considered.</param>
	/// <returns>Each member's name and declared starting value, in declaration order.</returns>
	/// <remarks>
	/// A member of a type may be a <see cref="FieldDeclaration"/> or a <see cref="VariableDeclaration"/>,
	/// and every struct writer treats both as an instance member, so both are gathered here. Gathering
	/// only the first left a variable's starting value out of every constructor that did not set it:
	/// a missing field in Rust, and a silent zero in Go and C. A static or constant field belongs to
	/// the type rather than to each instance, so it is not one of them.
	/// </remarks>
	protected static IReadOnlyList<MemberInitialiser> InstanceDefaults(ClassDeclaration declaration)
	{
		Ensure.NotNull(declaration);
		List<MemberInitialiser> defaults = [];
		foreach (AstNode member in declaration.Members)
		{
			switch (member)
			{
				case VariableDeclaration { InitialValue: Expression value } variable:
					defaults.Add(new MemberInitialiser(variable.Name ?? string.Empty, value));
					break;

				case FieldDeclaration { IsStatic: false, IsConstant: false, InitialValue: Expression value } field:
					defaults.Add(new MemberInitialiser(field.Name ?? string.Empty, value));
					break;

				default:
					break;
			}
		}

		return defaults;
	}

	/// <summary>
	/// Writes the per-instance field initializers that belong at the top of a constructor.
	/// </summary>
	/// <param name="declaration">The type whose instance fields are being initialized.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="needsConstruction">Whether the field's initializer belongs in its constructor.</param>
	/// <param name="writeInitialisation">How to emit an individual field initialization.</param>
	protected static void WriteConstructorPrologue(
		ClassDeclaration declaration,
		CodeBlocker code,
		Func<FieldDeclaration, bool> needsConstruction,
		Action<FieldDeclaration, CodeBlocker> writeInitialisation)
	{
		Ensure.NotNull(code);
		Ensure.NotNull(writeInitialisation);
		foreach (FieldDeclaration field in InstanceInitialised(declaration, needsConstruction))
		{
			writeInitialisation(field, code);
		}
	}

	/// <summary>
	/// Starts a scope containing the types declared by a function's parameters and return type.
	/// </summary>
	/// <param name="function">The function whose body is about to be generated.</param>
	protected void PushStaticTypeScope(FunctionDeclaration function)
	{
		Ensure.NotNull(function);
		Dictionary<string, TypeReference> types = new(StringComparer.Ordinal);
		foreach (Parameter parameter in function.Parameters)
		{
			if (parameter.Name is string name && parameter.Type is TypeReference type)
			{
				types[name] = type;
			}
		}

		staticTypeScopes.Push(new StaticTypeScope(types, function.ReturnType));
	}

	/// <summary>
	/// Starts a scope for a property accessor's body and ends it when the returned guard is disposed.
	/// </summary>
	/// <param name="property">The property whose accessor is about to be generated.</param>
	/// <param name="isSetter">True for the setter, whose <paramref name="valueName"/> parameter holds the property's type; false for the getter, which returns it.</param>
	/// <param name="valueName">The name the target gives the setter's incoming value.</param>
	/// <returns>The guard whose disposal ends the scope.</returns>
	/// <remarks>
	/// An accessor is a function the AST does not spell as a <see cref="FunctionDeclaration"/>, so
	/// without this its body has no scope and a typed local or the setter's value is not known to be
	/// an integer.
	/// </remarks>
	protected IDisposable OpenStaticTypeScope(PropertyDeclaration property, bool isSetter, string valueName = "value")
	{
		Ensure.NotNull(property);
		Ensure.NotNull(valueName);
		Dictionary<string, TypeReference> types = new(StringComparer.Ordinal);
		if (isSetter && property.Type is TypeReference type)
		{
			types[valueName] = type;
		}

		staticTypeScopes.Push(new StaticTypeScope(types, isSetter ? null : property.Type));
		return new StaticTypeScopeGuard(this);
	}

	/// <summary>
	/// Starts a scope for an entry point's body and ends it when the returned guard is disposed.
	/// </summary>
	/// <param name="entryPoint">The entry point whose body is about to be generated.</param>
	/// <returns>The guard whose disposal ends the scope.</returns>
	/// <remarks>
	/// The scope is what records the body's typed locals. An entry point that returns an exit code
	/// returns an <c>int</c>.
	/// </remarks>
	protected IDisposable OpenStaticTypeScope(EntryPoint entryPoint)
	{
		Ensure.NotNull(entryPoint);
		TypeReference? returnType = entryPoint.ReturnsExitCode ? new TypeReference("int") : null;
		staticTypeScopes.Push(new StaticTypeScope(new Dictionary<string, TypeReference>(StringComparer.Ordinal), returnType));
		return new StaticTypeScopeGuard(this);
	}

	/// <summary>
	/// Ends the most recently opened function type scope.
	/// </summary>
	protected void PopStaticTypeScope() => staticTypeScopes.Pop();

	/// <summary>
	/// Starts a function's type scope and ends it when the returned guard is disposed.
	/// </summary>
	/// <param name="function">The function whose body is about to be generated.</param>
	/// <returns>The guard whose disposal ends the scope.</returns>
	protected IDisposable OpenStaticTypeScope(FunctionDeclaration function)
	{
		PushStaticTypeScope(function);
		return new StaticTypeScopeGuard(this);
	}

	/// <summary>
	/// Reads whether both operands of a binary expression are statically known to be integers.
	/// </summary>
	/// <param name="binary">The expression to inspect.</param>
	/// <returns>True when the AST establishes an integer type for each operand.</returns>
	/// <remarks>
	/// This is what separates <c>-7 / 2</c>, which the AST means as truncating integer division because
	/// that is what it is in C, C++, C#, Go and Rust, from a division the AST cannot say anything about.
	/// An operand whose type is unknown is not assumed to be an integer.
	/// </remarks>
	protected bool IsIntegerArithmetic(BinaryExpression binary)
	{
		Ensure.NotNull(binary);
		return IsIntegerType(StaticType(binary.Left)) && IsIntegerType(StaticType(binary.Right));
	}

	/// <summary>
	/// Reads whether a bitwise <c>&amp;</c>, <c>|</c> or <c>^</c> is applied to two operands statically
	/// known to be <c>bool</c>.
	/// </summary>
	/// <param name="op">The operator.</param>
	/// <param name="left">The left operand.</param>
	/// <param name="right">The right operand.</param>
	/// <returns>True for a bitwise operator whose operands are both known to be <c>bool</c>.</returns>
	/// <remarks>
	/// C#, C, C++, Rust and Python apply these to booleans and give a boolean, evaluating both sides.
	/// Go defines them on integers only and JavaScript coerces to a number, so those targets respell
	/// them (#268). An operand of unknown type is not assumed to be a <c>bool</c>.
	/// </remarks>
	protected bool IsBooleanBitwise(BinaryOperator op, Expression left, Expression right)
	{
		Ensure.NotNull(left);
		Ensure.NotNull(right);
		return op is BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor
			&& IsBoolType(StaticType(left))
			&& IsBoolType(StaticType(right));
	}

	/// <summary>
	/// Reads the binary operator a bitwise compound assignment applies.
	/// </summary>
	/// <param name="op">The compound assignment operator.</param>
	/// <returns>The bitwise operator, or null for any other assignment.</returns>
	protected static BinaryOperator? BitwiseOperatorOf(AssignmentOperator op) => op switch
	{
		AssignmentOperator.BitwiseAndAssign => BinaryOperator.BitwiseAnd,
		AssignmentOperator.BitwiseOrAssign => BinaryOperator.BitwiseOr,
		AssignmentOperator.BitwiseXorAssign => BinaryOperator.BitwiseXor,
		_ => null,
	};

	private static bool IsBoolType(TypeReference? type) =>
		type is { IsArray: false, Indirection: TypeIndirection.None, TypeArguments.Count: 0, Name: "bool" };

	private static bool IsIntegerType(TypeReference? type) =>
		type is { IsArray: false, Indirection: TypeIndirection.None, TypeArguments.Count: 0 }
		&& IntegerTypeNames.Contains(type.Name);

	/// <summary>
	/// Reads the floating-point type a remainder of two operands is taken in.
	/// </summary>
	/// <param name="dividend">The left operand.</param>
	/// <param name="divisor">The right operand.</param>
	/// <returns><c>double</c> or <c>float</c>, or null when neither operand is known to be floating.</returns>
	/// <remarks>
	/// Either operand being floating is enough, because that is what makes the arithmetic floating in
	/// every target: an integer beside a <c>double</c> is converted to it. A <c>double</c> on either
	/// side makes it <c>double</c>, which is the usual arithmetic conversion's answer too.
	/// </remarks>
	protected string? FloatingRemainderType(Expression dividend, Expression divisor)
	{
		Ensure.NotNull(dividend);
		Ensure.NotNull(divisor);
		string? left = FloatingTypeName(StaticType(dividend));
		string? right = FloatingTypeName(StaticType(divisor));
		if (left is null && right is null)
		{
			return null;
		}

		return left == "double" || right == "double" ? "double" : "float";
	}

	private static string? FloatingTypeName(TypeReference? type) =>
		type is { IsArray: false, Indirection: TypeIndirection.None, TypeArguments.Count: 0, Name: "float" or "double" }
			? type.Name
			: null;

	/// <summary>
	/// Gets whether the target spells a floating-point remainder as a call rather than as <c>%</c>.
	/// </summary>
	/// <remarks>
	/// C, C++ and Go define <c>%</c> on integers only, so a remainder of floating operands has to be
	/// <c>fmod</c> or <c>math.Mod</c> there. Python's <c>%</c> takes floats but floors, so it writes
	/// <c>math.fmod</c> to keep the dividend's sign. C#, JavaScript and Rust's <c>%</c> already
	/// truncates on floating operands.
	/// </remarks>
	protected virtual bool SpellsFloatingRemainderAsCall => false;

	/// <summary>
	/// Writes the call that takes a floating-point remainder, for a target whose <c>%</c> cannot.
	/// </summary>
	/// <param name="dividend">The left operand.</param>
	/// <param name="divisor">The right operand.</param>
	/// <param name="floatingType">The type the remainder is taken in, from <see cref="FloatingRemainderType"/>.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <exception cref="NotSupportedException">The target spells the remainder as <c>%</c>.</exception>
	protected virtual void WriteFloatingRemainder(Expression dividend, Expression divisor, string floatingType, CodeBlocker code) =>
		throw new NotSupportedException($"{GetType().Name} spells a floating-point remainder as %.");

	/// <summary>
	/// Reports whether a file takes a remainder that <see cref="WriteFloatingRemainder"/> will write.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <returns>True when a <c>%</c> or <c>%=</c> in it has a floating-point operand.</returns>
	/// <remarks>
	/// The imports are written before anything they are for, so the question is asked of the AST
	/// rather than of the output. Each function's, accessor's and entry point's parameters and locals
	/// are put in scope the way its emitter puts them, which is what makes the answer agree with what
	/// is written; a file that imports what it does not use does not compile in Go.
	/// </remarks>
	protected bool ContainsFloatingRemainder(SourceFile file)
	{
		Ensure.NotNull(file);
		if (SelfAndDescendants(file).Any(IsFloatingRemainder))
		{
			return true;
		}

		return SelfAndDescendants(file).Any(node => node switch
		{
			FunctionDeclaration function => ScopeContainsFloatingRemainder(() => OpenStaticTypeScope(function), [function]),

			// An accessor and an entry point are bodies their emitters scope as well (#278). Go, C
			// and C++ write a setter as a function taking `value`, which is the name this scope gives
			// it too.
			PropertyDeclaration property =>
				ScopeContainsFloatingRemainder(() => OpenStaticTypeScope(property, isSetter: false), property.GetterBody)
				|| ScopeContainsFloatingRemainder(() => OpenStaticTypeScope(property, isSetter: true), property.SetterBody),
			EntryPoint entryPoint => ScopeContainsFloatingRemainder(() => OpenStaticTypeScope(entryPoint), entryPoint.Body),
			_ => false,
		});
	}

	/// <summary>
	/// Reports whether a body takes a floating remainder once its locals are in the given scope.
	/// </summary>
	/// <param name="openScope">Opens the scope its emitter opens.</param>
	/// <param name="body">The nodes the scope covers.</param>
	/// <returns>True when a <c>%</c> or <c>%=</c> in the body has a floating-point operand.</returns>
	private bool ScopeContainsFloatingRemainder(Func<IDisposable> openScope, IEnumerable<AstNode> body)
	{
		using (openScope())
		{
			AstNode[] nodes = [.. body.SelectMany(SelfAndDescendants)];

			// A local can take its type from another, so one pass per local settles every chain however
			// the walk happens to order them.
			VariableDeclaration[] locals = [.. nodes.OfType<VariableDeclaration>()];
			for (int pass = 0; pass < locals.Length; pass++)
			{
				foreach (VariableDeclaration local in locals)
				{
					RegisterStaticType(local);
				}
			}

			return nodes.Any(IsFloatingRemainder);
		}
	}

	private bool IsFloatingRemainder(AstNode node) => node switch
	{
		BinaryExpression { Operator: BinaryOperator.Modulo } binary => FloatingRemainderType(binary.Left, binary.Right) is not null,
		AssignmentStatement { Operator: AssignmentOperator.ModuloAssign } assignment => FloatingRemainderType(assignment.Target, assignment.Value) is not null,
		_ => false,
	};

	private static readonly HashSet<string> IntegerTypeNames = new(StringComparer.Ordinal)
	{
		"int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
	};

	/// <summary>
	/// Records a local's declared or inferred type for later references.
	/// </summary>
	/// <param name="declaration">The local declaration being generated.</param>
	protected void RegisterStaticType(VariableDeclaration declaration)
	{
		Ensure.NotNull(declaration);
		if (staticTypeScopes.TryPeek(out StaticTypeScope? scope) && declaration.Name is string name)
		{
			TypeReference? type = declaration.Type
				?? (declaration.InitialValue is Expression expression ? StaticType(expression) : null);
			if (type is not null)
			{
				scope.Types[name] = type;
			}
		}
	}

	/// <summary>
	/// Reports whether a variable is declared directly in a namespace or a file rather than in a body.
	/// </summary>
	/// <param name="declaration">The declaration to test.</param>
	/// <returns>True when it is a module-scope variable.</returns>
	/// <remarks>
	/// Several targets spell a local as a statement — Rust's <c>let</c>, Go's <c>:=</c> — which is
	/// refused outside a function, so a variable of a namespace or a file has to be told apart by
	/// where it was declared rather than by its own shape.
	/// </remarks>
	protected bool IsModuleVariable(VariableDeclaration declaration) => moduleVariables.Contains(declaration);

	/// <summary>
	/// Records the variables a namespace or a file declares directly.
	/// </summary>
	/// <param name="members">The scope's members.</param>
	private void HoldModuleVariables(IEnumerable<AstNode> members)
	{
		foreach (VariableDeclaration variable in members.OfType<VariableDeclaration>())
		{
			moduleVariables.Add(variable);
		}
	}

	/// <summary>
	/// Reads the width an inferred local must be written with, because the target would infer another.
	/// </summary>
	/// <param name="initialValue">The local's initialiser.</param>
	/// <returns><c>long</c> or <c>float</c> when the initialiser establishes one; otherwise null.</returns>
	/// <remarks>
	/// Rust and Go infer an unconstrained integer as their default integer and a float as a 64-bit one,
	/// so a long or a float that nothing later pins down would silently narrow or widen — and in Go,
	/// whose arithmetic never converts implicitly, a later <c>float32</c> operand does not compile. An
	/// <c>int</c> or a <c>double</c> is what those targets infer anyway, so it is left to inference.
	/// </remarks>
	protected TypeReference? InferredLiteralWidth(Expression initialValue) =>
		StaticType(initialValue) is { IsArray: false, Indirection: TypeIndirection.None, TypeArguments.Count: 0, Name: "long" or "float" } width
			? width
			: null;

	/// <summary>
	/// Reads the type an expression can establish from its own annotation, shape, or current scope.
	/// </summary>
	/// <param name="expression">The expression to inspect.</param>
	/// <returns>The known type, or null when the AST does not establish one.</returns>
	protected TypeReference? StaticType(Expression expression)
	{
		Ensure.NotNull(expression);
		if (expression.ExpectedType is string expectedType)
		{
			return new TypeReference(expectedType);
		}

		switch (expression)
		{
			case LiteralExpression<string>:
				return new TypeReference("string");
			case LiteralExpression<int>:
				return new TypeReference("int");
			case LiteralExpression<long>:
				return new TypeReference("long");
			case LiteralExpression<float>:
				return new TypeReference("float");
			case LiteralExpression<double>:
				return new TypeReference("double");
			case LiteralExpression<bool>:
				return new TypeReference("bool");
			case UnaryExpression { Operator: UnaryOperator.LogicalNot }:
				return new TypeReference("bool");
			case ConstructionExpression { Type: not null } construction:
				return construction.Type;
			case CallExpression { Receiver: null } call:
				return DeclaredReturnType(call.Callee);
			case CallExpression { Receiver: Expression receiver } call:
				return DeclaredMethodReturnType(StaticType(receiver), call.Callee);
			case ConditionalExpression conditional:
				return Promoted(StaticType(conditional.WhenTrue), StaticType(conditional.WhenFalse));
			case BinaryExpression binary when binary.Operator is BinaryOperator.Equal
				or BinaryOperator.NotEqual
				or BinaryOperator.LessThan
				or BinaryOperator.LessThanOrEqual
				or BinaryOperator.GreaterThan
				or BinaryOperator.GreaterThanOrEqual
				or BinaryOperator.LogicalAnd
				or BinaryOperator.LogicalOr:
				return new TypeReference("bool");
			case BinaryExpression binary:
				return Promoted(StaticType(binary.Left), StaticType(binary.Right));
			case VariableReference reference:
				foreach (StaticTypeScope scope in staticTypeScopes)
				{
					if (scope.Types.TryGetValue(reference.Name, out TypeReference? type))
					{
						return type;
					}
				}

				return DeclaredMemberType(reference.Name);
			default:
				return null;
		}
	}

	/// <summary>
	/// Reads the type of a return statement from its enclosing function.
	/// </summary>
	/// <param name="statement">The return statement.</param>
	/// <returns>The function's return type, or null when it has none.</returns>
	protected TypeReference? StaticType(ReturnStatement statement)
	{
		Ensure.NotNull(statement);
		return staticTypeScopes.TryPeek(out StaticTypeScope? scope) ? scope.ReturnType : null;
	}

	/// <summary>
	/// Reads the return type of the one function a receiver-less call can name.
	/// </summary>
	/// <param name="callee">The name the call is written with.</param>
	/// <returns>The function's return type, or null when no function or more than one has the name.</returns>
	/// <remarks>
	/// A call's result is otherwise of unknown type, so <c>twice(a) / 4</c> would be true division in
	/// Python and JavaScript even though <c>twice</c> is declared <c>int</c> beside it (#273). Only a
	/// name that exactly one <see cref="FunctionDeclaration"/> in what is being generated has is
	/// resolved: an overload or a namesake elsewhere could return something else, and a callee
	/// declared outside the tree says nothing here, so both keep the type unknown.
	/// </remarks>
	private TypeReference? DeclaredReturnType(string callee) => DeclaredFunction(callee)?.ReturnType;

	/// <summary>
	/// Finds the one function a receiver-less call can name.
	/// </summary>
	/// <param name="callee">The name the call is written with.</param>
	/// <returns>The function, or null when no function or more than one has the name.</returns>
	private FunctionDeclaration? DeclaredFunction(string callee)
	{
		if (GenerationRoot is null || string.IsNullOrEmpty(callee))
		{
			return null;
		}

		FunctionDeclaration[] matches = [.. SelfAndDescendants(GenerationRoot)
			.OfType<FunctionDeclaration>()
			.Where(function => function.Name == callee)
			.Take(2)];
		return matches.Length == 1 ? matches[0] : null;
	}

	/// <summary>
	/// Reads the declared type of the parameter a call's argument is passed as.
	/// </summary>
	/// <param name="call">The call.</param>
	/// <param name="index">The argument's position.</param>
	/// <returns>
	/// The parameter's type, or null when the call does not name exactly one declared function or
	/// method, or that function has no parameter at the position.
	/// </returns>
	private TypeReference? DeclaredParameterType(CallExpression call, int index)
	{
		FunctionDeclaration? function = call.Receiver is Expression receiver
			? DeclaredMethod(StaticType(receiver), call.Callee)
			: DeclaredFunction(call.Callee);
		return function is not null && index < function.Parameters.Count
			? function.Parameters[index].Type
			: null;
	}

	/// <summary>
	/// Reads the declared type of the one field or member variable a name no function scope declares
	/// can mean.
	/// </summary>
	/// <param name="name">The name the reference is written with.</param>
	/// <returns>The member's declared type, or null when no member or more than one has the name.</returns>
	/// <remarks>
	/// A file-scope <c>int</c> constant read by a function was otherwise of unknown type, so
	/// <c>LIMIT / 2</c> was true division in Python and JavaScript, and C typed a local copied from it
	/// <c>void*</c> (#304). Only a <see cref="FieldDeclaration"/>, or a <see cref="VariableDeclaration"/>
	/// declared as a member of a file, namespace or type rather than in a body, is a candidate, and only
	/// a name exactly one of them has is resolved, the same rule <see cref="DeclaredReturnType"/> keeps.
	/// A parameter or local of the name is found in the scopes first and so still wins.
	/// </remarks>
	private TypeReference? DeclaredMemberType(string name)
	{
		if (GenerationRoot is null || string.IsNullOrEmpty(name))
		{
			return null;
		}

		IEnumerable<AstNode> containers = SelfAndDescendants(GenerationRoot);
		IEnumerable<VariableDeclaration> memberVariables = containers.SelectMany(node => node switch
		{
			SourceFile file => file.Members,
			NamespaceDeclaration space => space.Members,
			ClassDeclaration type => type.Members,
			_ => Enumerable.Empty<AstNode>(),
		}).OfType<VariableDeclaration>();

		TypeReference?[] matches = [.. SelfAndDescendants(GenerationRoot)
			.OfType<FieldDeclaration>()
			.Where(field => field.Name == name)
			.Select(field => field.Type)
			.Concat(memberVariables.Where(variable => variable.Name == name).Select(variable => variable.Type))
			.Take(2)];
		return matches.Length == 1 ? matches[0] : null;
	}

	/// <summary>
	/// Reads the return type of the one method a call on a receiver of a known type can name.
	/// </summary>
	/// <param name="receiverType">The receiver's type, or null when it is unknown.</param>
	/// <param name="callee">The name the call is written with.</param>
	/// <returns>
	/// The method's return type, or null when the receiver's type is unknown, is not exactly one class
	/// in what is being generated, or that class has no method or more than one with the name.
	/// </returns>
	/// <remarks>
	/// The receiver-less case (#273) left <c>b.size() / 2</c> of unknown type, so Python and JavaScript
	/// still divided it truly although <c>Box.size</c> is declared <c>int</c> (#280). Only the class's
	/// own members are looked at: a method it inherits, an overload, or a class declared outside the
	/// tree could return something else, so each keeps the type unknown.
	/// </remarks>
	private TypeReference? DeclaredMethodReturnType(TypeReference? receiverType, string callee) =>
		DeclaredMethod(receiverType, callee)?.ReturnType;

	/// <summary>
	/// Finds the one method a call on a receiver of a known type can name.
	/// </summary>
	/// <param name="receiverType">The receiver's type, or null when it is unknown.</param>
	/// <param name="callee">The name the call is written with.</param>
	/// <returns>The method, or null where <see cref="DeclaredMethodReturnType"/> knows no type.</returns>
	private FunctionDeclaration? DeclaredMethod(TypeReference? receiverType, string callee)
	{
		if (GenerationRoot is null || receiverType is null or { IsArray: true } || string.IsNullOrEmpty(callee))
		{
			return null;
		}

		ClassDeclaration[] classes = [.. SelfAndDescendants(GenerationRoot)
			.OfType<ClassDeclaration>()
			.Where(declaration => declaration.Name == receiverType.Name)
			.Take(2)];
		if (classes.Length != 1)
		{
			return null;
		}

		FunctionDeclaration[] matches = [.. classes[0].Members
			.OfType<FunctionDeclaration>()
			.Where(function => function.Name == callee)
			.Take(2)];
		return matches.Length == 1 ? matches[0] : null;
	}

	/// <summary>
	/// The numeric types in the order a mixed operation widens to, narrowest first.
	/// </summary>
	private static readonly string[] NumericPromotionOrder = ["int", "long", "float", "double"];

	/// <summary>
	/// Combines the types of two operands, or of a conditional's two branches, into the type of the
	/// whole.
	/// </summary>
	/// <param name="first">The first operand's type, if known.</param>
	/// <param name="second">The second operand's type, if known.</param>
	/// <returns>
	/// The wider of two numeric types, so <c>1 + 0.5</c> and <c>ready ? 1 : 2.5</c> are a
	/// <c>double</c> as they are in every C-family language; otherwise the first known type.
	/// </returns>
	protected static TypeReference? Promoted(TypeReference? first, TypeReference? second)
	{
		int firstRank = NumericRank(first);
		int secondRank = NumericRank(second);
		return firstRank >= 0 && secondRank > firstRank ? second : first ?? second;
	}

	/// <summary>
	/// Reports where a type stands in <see cref="NumericPromotionOrder"/>.
	/// </summary>
	/// <param name="type">The type to place.</param>
	/// <returns>Its position, or -1 when it is not a plain numeric type.</returns>
	private static int NumericRank(TypeReference? type) =>
		type is null ? -1 : Array.IndexOf(NumericPromotionOrder, type.ToString());

	/// <summary>
	/// Gets whether the target refuses to widen a number implicitly, so a narrower value meeting a
	/// wider type has to be converted in the source.
	/// </summary>
	/// <remarks>
	/// The AST follows the C family, where an <c>int</c> beside a <c>long</c> or a <c>double</c> is
	/// widened, and an <c>int</c> can be assigned, returned or passed as either. Rust and Go convert
	/// nothing between numeric types, so <c>a + b</c> on an <c>i64</c> and an <c>i32</c> does not
	/// compile there (#238). Those two override this and <see cref="WriteWidened"/>.
	/// </remarks>
	protected virtual bool WidensNumbersExplicitly => false;

	/// <summary>
	/// Writes a value converted to a wider numeric type, for a target that does not widen implicitly.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="wider">The wider type it meets.</param>
	/// <param name="code">The writer to emit into.</param>
	protected virtual void WriteWidened(Expression value, TypeReference wider, CodeBlocker code) => GenerateInternal(value, code);

	/// <summary>
	/// Writes a value where a type receives it, converting it first when it is a narrower number the
	/// target does not widen on its own.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="target">The type receiving it, or null where none is known.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Only widening is converted. Narrowing is never implicit in the AST, so a value whose type is
	/// unknown, not numeric, or already as wide is written as it is.
	/// </remarks>
	protected void GenerateWidenedTo(AstNode value, TypeReference? target, CodeBlocker code)
	{
		Ensure.NotNull(value);
		if (WidensNumbersExplicitly
			&& value is Expression expression
			&& target is not null
			&& StaticType(expression) is TypeReference from
			&& IsWidening(from, target))
		{
			WriteWidened(expression, target, code);
			return;
		}

		GenerateInternal(value, code);
	}

	/// <summary>
	/// Reports whether a value of one numeric type is widened by meeting another.
	/// </summary>
	/// <param name="from">The value's type.</param>
	/// <param name="to">The type it meets.</param>
	/// <returns>True when <paramref name="to"/> is the wider of the two.</returns>
	private static bool IsWidening(TypeReference from, TypeReference to)
	{
		int fromRank = NumericRank(from);
		return (fromRank >= 0 && NumericRank(to) > fromRank)
			|| (IsIntegerType(from) && FloatingTypeName(to) is not null);
	}

	/// <summary>
	/// Reports whether a target type is a floating-point one, for the targets that write an integer
	/// literal meeting it as a floating literal.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <returns>True for <c>float</c> and <c>double</c>.</returns>
	protected static bool IsFloatingType(TypeReference? type) => FloatingTypeName(type) is not null;

	private sealed record StaticTypeScope(Dictionary<string, TypeReference> Types, TypeReference? ReturnType);

	private sealed class StaticTypeScopeGuard(StandardLanguageGenerator generator) : IDisposable
	{
		public void Dispose() => generator.PopStaticTypeScope();
	}

	/// <summary>
	/// Dispatches a node to the emitter for its shape.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <exception cref="NotSupportedException">The node is not one this generator emits.</exception>
	protected sealed override void GenerateInternal(AstNode node, CodeBlocker code)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(code);

		if (IsRootHoldingEntryPoint(node))
		{
			GenerateWithEntryPointsAfter(node, code);
			return;
		}

		switch (node)
		{
			case ClassDeclaration classDecl:
				GenerateClassDeclaration(classDecl, code);
				break;

			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, code);
				break;

			case SourceFile file:
				HoldModuleVariables(file.Members);
				GenerateSourceFile(WithEntryPointsAtFileScope(file), code);
				break;

			case NamespaceDeclaration namespaceDecl:
				HoldModuleVariables(namespaceDecl.Members);
				GenerateNamespaceDeclaration(namespaceDecl, code);
				break;

			case CompileTimeAssertion assertion:
				GenerateCompileTimeAssertion(assertion, code);
				break;

			case UsingAlias usingAlias:
				GenerateUsingAlias(usingAlias, code);
				break;

			case ConstructionExpression construction:
				GenerateConstructionExpression(construction, code);
				break;

			case CallExpression call:
				GenerateCallExpression(call, code);
				break;

			case ConditionalExpression conditional:
				GenerateConditionalExpression(conditional, code);
				break;

			case ExpressionStatement statement:
				GenerateExpressionStatement(statement, code);
				break;

			case EnumDeclaration enumDecl:
				GenerateEnumDeclaration(enumDecl, code);
				break;

			case PropertyDeclaration property:
				GeneratePropertyDeclaration(property, code);
				break;

			case FieldDeclaration field:
				GenerateFieldDeclaration(field, code);
				break;

			case EntryPoint entryPoint:
				GenerateEntryPoint(entryPoint, code);
				break;

			case Parameter parameter:
				GenerateParameter(parameter, code, 0);
				break;

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, code);
				break;

			case BinaryExpression binaryExpr:
				GenerateBinary(binaryExpr, code);
				break;

			case UnaryExpression unaryExpr:
				GenerateUnary(unaryExpr, code);
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, code);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, code);
				break;

			default:
				if (!TryGenerateCommonNode(node, code))
				{
					throw new NotSupportedException($"Unsupported node type for {DisplayName} generation: {node.GetNodeTypeName()}");
				}

				break;
		}
	}

	/// <summary>
	/// Emits a function declaration, including its body.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code);

	/// <summary>
	/// Emits a class declaration, including its members.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code);

	/// <summary>
	/// Emits a namespace and its members.
	/// </summary>
	/// <param name="namespaceDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The default emits the members and nothing around them, which is right for a language whose
	/// unit of naming is the file. A language that writes a namespace overrides this.
	/// </remarks>
	protected virtual void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		WritePreamble(namespaceDecl, code, PreambleSite.Type);
		WriteMembers(namespaceDecl.Members, code);
	}

	/// <summary>
	/// Writes a run of declarations, separated as this language separates them.
	/// </summary>
	/// <param name="members">The declarations to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The same walk <see cref="LanguageGeneratorBase.GenerateSourceFile"/> does over a file's
	/// members, for the emitters that have a run of declarations to write and no file around them.
	/// </remarks>
	protected void WriteMembers(IEnumerable<AstNode> members, CodeBlocker code)
	{
		Ensure.NotNull(members);
		Ensure.NotNull(code);

		AstNode? previous = null;
		foreach (AstNode member in members)
		{
			if (previous is not null && NeedsSeparation(previous, member))
			{
				code.NewLine();
			}

			previous = member;
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Reports whether two adjacent declarations belong in one block.
	/// </summary>
	/// <param name="previous">The declaration already written.</param>
	/// <param name="member">The declaration about to be written.</param>
	/// <returns>True when no blank line belongs between them.</returns>
	/// <remarks>
	/// Two of a kind that say nothing about themselves stay together, which is what keeps a run of
	/// aliases, of constants or of assertions about one type reading as one block rather than as a
	/// paragraph each. A documented member needs air above it or its first comment line butts against
	/// the member before it and reads as belonging to that one.
	/// <para>
	/// The rule rather than the override, because <see cref="LanguageGeneratorBase.NeedsSeparation"/> is
	/// the question and this is one answer to it: the C family, Rust and Go all give this one, while Python and
	/// JavaScript keep the default of a blank line between everything.
	/// </para>
	/// </remarks>
	protected static bool GroupsWith(AstNode previous, AstNode member)
	{
		Ensure.NotNull(previous);
		Ensure.NotNull(member);

		return previous.GetType() == member.GetType()
			&& !IsDocumented(previous)
			&& !IsDocumented(member);
	}

	/// <summary>
	/// Reports whether a declaration carries documentation.
	/// </summary>
	/// <param name="member">The declaration to test.</param>
	/// <returns>True when it does.</returns>
	protected static bool IsDocumented(AstNode member) =>
		member is IHasDocumentation documented && documented.Documentation.Count > 0;

	/// <summary>
	/// Emits something that must be true when the program is built.
	/// </summary>
	/// <param name="assertion">The assertion to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code);

	/// <summary>
	/// Emits an alias giving a type a second name.
	/// </summary>
	/// <param name="usingAlias">The alias to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code);

	/// <summary>
	/// Emits an expression that builds a value.
	/// </summary>
	/// <param name="construction">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code);

	/// <summary>
	/// Emits an enumeration declaration, including its members.
	/// </summary>
	/// <param name="enumDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code);

	/// <summary>
	/// Emits a field of a type.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code);

	/// <summary>
	/// Emits a property, as whichever of the two things it is in this language.
	/// </summary>
	/// <param name="declaration">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The default is for the four targets that have no properties, and it does not approximate
	/// one: it separates the property into the declarations it is made of and emits those through
	/// the emitters that already exist. A property whose accessors have no bodies is a field with a
	/// storage location the compiler supplies, so it comes out as a field; one with bodies is a
	/// pair of functions, so it comes out as the pair. Both are what a person would have written.
	/// <para>
	/// The one thing the field form loses is that a property may be readable and not writable,
	/// which a plain field cannot be. That is said rather than dropped.
	/// </para>
	/// </remarks>
	protected virtual void GeneratePropertyDeclaration(PropertyDeclaration declaration, CodeBlocker code)
	{
		Ensure.NotNull(declaration);
		Ensure.NotNull(code);

		bool first = true;
		foreach (AstNode lowered in Separate(declaration, []))
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;
			GenerateInternal(lowered, code);
		}
	}

	/// <summary>
	/// Gets the convention this target spells an invented member name in.
	/// </summary>
	/// <remarks>
	/// For the names a generator has to make up rather than the ones it was given. A property
	/// becoming a pair of functions has no name for its setter until somebody invents one, and
	/// inventing it in the wrong convention is how generated code announces itself.
	/// </remarks>
	protected virtual NamingStyle MemberNaming => NamingStyle.Camel;

	/// <summary>
	/// Gets a value indicating whether a data member and a member function of one type need
	/// different names in this target.
	/// </summary>
	/// <remarks>
	/// The question a separated property's getter has to ask, because its name is invented: a
	/// property <c>Count</c> over a field <c>count</c> becomes <c>count()</c> in snake case, and C++
	/// refuses a function beside a field of the same name where Rust allows it and C prefixes it out
	/// of the way. A target that answers yes has a colliding getter spelled with a <c>get</c> prefix.
	/// </remarks>
	protected virtual bool MemberNamesMustDiffer => false;

	/// <summary>
	/// A type's members, with every property separated into the declarations it is made of.
	/// </summary>
	/// <param name="classDecl">The type being emitted.</param>
	/// <returns>The declaration itself when it holds no properties; otherwise a copy of it whose
	/// members are fields and functions.</returns>
	/// <remarks>
	/// Done to the member list rather than at the point each member is written, and that is the
	/// whole of why it works. Three of these generators route a type's members by what they are —
	/// Rust puts the data in a <c>struct</c> and the behaviour in an <c>impl</c>, Go writes the
	/// fields in the type and the methods beside it, C lowers a method to a free function taking
	/// the instance — and every one of those routes reads the member list before anything is
	/// written. A property separated afterwards arrives too late to be routed and comes out
	/// wherever it happened to be standing.
	/// <para>
	/// So it is separated first, and each generator's existing routing then sees an ordinary field
	/// or an ordinary function and needs to know nothing about properties at all.
	/// </para>
	/// </remarks>
	protected ClassDeclaration Separated(ClassDeclaration classDecl)
	{
		Ensure.NotNull(classDecl);

		if (!classDecl.Members.OfType<PropertyDeclaration>().Any())
		{
			return classDecl;
		}

		ClassDeclaration separated = (ClassDeclaration)classDecl.Clone();
		for (int index = 0; index < classDecl.Annotations.Count; index++)
		{
			RememberAnnotationClone(separated.Annotations[index], classDecl.Annotations[index]);
		}

		separated.Members.Clear();

		HashSet<string> taken = MemberNamesMustDiffer ? NamesOf(classDecl.Members) : [];

		foreach (AstNode member in classDecl.Members)
		{
			if (member is PropertyDeclaration property)
			{
				foreach (AstNode part in Separate(property, taken))
				{
					separated.Members.Add(part);
				}

				continue;
			}

			separated.Members.Add(member);
		}

		return separated;
	}

	/// <summary>
	/// Picks a name for a function the generator invents beside the user's, one the generated input
	/// does not already declare.
	/// </summary>
	/// <param name="preferred">The name to use when nothing in the input declares it.</param>
	/// <param name="fallback">The stem to use otherwise, numbered if the input declares that too.</param>
	/// <returns>A name no declaration in the input has.</returns>
	/// <remarks>
	/// Any declaration of the name anywhere in the input counts, not only one beside the invented
	/// function, so the answer does not depend on how a target scopes what it lowers (#299).
	/// </remarks>
	protected string UnclaimedName(string preferred, string fallback)
	{
		Ensure.NotNull(preferred);
		Ensure.NotNull(fallback);
		HashSet<string> taken = GenerationRoot is null ? [] : NamesOf(SelfAndDescendants(GenerationRoot));
		if (!taken.Contains(preferred))
		{
			return preferred;
		}

		string name = fallback;
		for (int suffix = 2; taken.Contains(name); suffix++)
		{
			name = string.Create(CultureInfo.InvariantCulture, $"{fallback}{suffix}");
		}

		return name;
	}

	/// <summary>
	/// The names a type's members already hold before any property is separated into functions.
	/// </summary>
	/// <param name="members">The type's members.</param>
	/// <returns>The names, including the field an automatic property becomes.</returns>
	private static HashSet<string> NamesOf(IEnumerable<AstNode> members) =>
		new(
			members
				.Select(member => member switch
				{
					FieldDeclaration field => field.Name,
					FunctionDeclaration function => function.Name,
					ClassDeclaration type => type.Name,
					EnumDeclaration enumeration => enumeration.Name,
					UsingAlias alias => alias.Name,
					PropertyDeclaration property when property.IsAutomatic => property.Name ?? "value",
					_ => null,
				})
				.OfType<string>(),
			StringComparer.Ordinal);

	/// <summary>
	/// Separates a property into the declarations it is made of.
	/// </summary>
	/// <param name="property">The property to separate.</param>
	/// <param name="taken">The member names already in use where they must differ, which an invented
	/// getter name is added to; empty where the target lets a field and a function share a name.</param>
	/// <returns>A field, or the accessors, in the order they should be written.</returns>
	private IEnumerable<AstNode> Separate(PropertyDeclaration property, HashSet<string> taken)
	{
		if (property.IsAutomatic)
		{
			// The one thing the field form cannot carry: a property may be readable and not
			// writable, and a field is neither or both. Said in the field's own documentation
			// rather than in a note beside it, so it travels with the declaration to wherever this
			// target puts it.
			IEnumerable<string> documentation = property.CanWrite
				? property.Documentation
				: [.. property.Documentation, $"{property.Name} is read-only, which a field is not."];

			yield return new FieldDeclaration(property.Name ?? "value", property.Type)
			{
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,
				Documentation = [.. documentation],
				Annotations = [.. property.Annotations.Select(CloneAnnotation)],
			};

			yield break;
		}

		if (property.CanRead)
		{
			string getterName = property.GetterName(MemberNaming);
			if (MemberNamesMustDiffer && taken.Contains(getterName))
			{
				getterName = NameStyles.Spell($"get {property.Name ?? "value"}", MemberNaming);
			}

			taken.Add(getterName);

			FunctionDeclaration getter = new(getterName)
			{
				ReturnType = property.Type,
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,

				// A getter leaves its instance alone, but a static one has no instance to leave alone,
				// and C++ refuses `const` on a static member function.
				IsReadOnly = !property.IsStatic,
				Documentation = [.. property.Documentation],
				Annotations = [.. property.Annotations.Select(CloneAnnotation)],
				Body = [.. property.GetterBody.Select(statement => statement.Clone())],
			};

			yield return getter;
		}

		if (property.CanWrite)
		{
			FunctionDeclaration setter = new(property.SetterName(MemberNaming))
			{
				ReturnType = new TypeReference("void"),
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,
				Body = [.. property.SetterBody.Select(statement => statement.Clone())],
			};

			setter.Parameters.Add(new Parameter("value") { Type = property.Type ?? new TypeReference("object") });
			yield return setter;
		}
	}

	/// <summary>
	/// Gets a value indicating whether an entry point declared in a namespace is written at file scope.
	/// </summary>
	/// <remarks>
	/// True for a language whose toolchain looks for its entry point only at file scope and whose
	/// namespaces are real scopes: C++ does not link a <c>main</c> declared in a namespace, and Rust
	/// does not find one declared in a module (#128). The languages that write a namespace's members
	/// straight into the file have nothing to move.
	/// </remarks>
	protected virtual bool EntryPointsLeaveNamespaces => false;

	/// <summary>
	/// Whether the root being generated is the copy with its entry points taken out.
	/// </summary>
	private bool generatingView;

	/// <summary>
	/// The namespaces each entry point written at file scope was declared in, outermost first.
	/// </summary>
	private Dictionary<EntryPoint, IReadOnlyList<string>> relocatedEntryPoints = new(ReferenceEqualityComparer.Instance);

	/// <summary>
	/// Gets the namespaces an entry point was declared in before it was moved to file scope.
	/// </summary>
	/// <param name="entryPoint">The entry point being written.</param>
	/// <returns>The namespace names, outermost first, or nothing when it was declared outside one.</returns>
	/// <remarks>
	/// The body was written expecting the names declared beside it to be in scope, so a generator
	/// that moved it out of its namespace brings that namespace's names back into the body.
	/// </remarks>
	protected IReadOnlyList<string> NamespacesLeftBy(EntryPoint entryPoint) =>
		relocatedEntryPoints.TryGetValue(entryPoint, out IReadOnlyList<string>? path) ? path : [];

	/// <summary>
	/// Determines whether a node is a type or namespace generated on its own with an entry point in it.
	/// </summary>
	/// <param name="node">The node being dispatched.</param>
	/// <returns>True when its entry points have to be written after it, at file scope.</returns>
	private bool IsRootHoldingEntryPoint(AstNode node) =>
		!generatingView && ReferenceEquals(node, GenerationRoot) && HoldsEntryPoint(node);

	/// <summary>
	/// Determines whether a type or namespace has an entry point anywhere inside it.
	/// </summary>
	/// <param name="node">The node to search.</param>
	/// <returns>True when one does.</returns>
	private static bool HoldsEntryPoint(AstNode node) =>
		node is ClassDeclaration or NamespaceDeclaration && SelfAndDescendants(node).OfType<EntryPoint>().Any();

	/// <summary>
	/// Returns a file whose entry points all stand where the toolchain looks for them.
	/// </summary>
	/// <param name="file">The file as the document declared it.</param>
	/// <returns>The file itself when nothing moves, otherwise a copy with the entry points last.</returns>
	/// <remarks>
	/// No language here other than C# finds an entry point inside a type: C++ does not link a member
	/// <c>main</c>, C cannot put a function in a struct, and Rust and Go route a type's members by
	/// what they are and dropped an entry point without a word. So an entry point in a type, and in a
	/// language that asks for it one in a namespace too, is written at file scope after everything
	/// else (#128). The containers are copies holding the document's own members, so the members
	/// still meet every check by identity.
	/// </remarks>
	private SourceFile WithEntryPointsAtFileScope(SourceFile file)
	{
		relocatedEntryPoints = new(ReferenceEqualityComparer.Instance);
		if (!file.Members.Any(HoldsEntryPoint))
		{
			return file;
		}

		Dictionary<EntryPoint, IReadOnlyList<string>> relocated = new(ReferenceEqualityComparer.Instance);
		List<EntryPoint> moved = [];
		List<AstNode> kept = WithoutEntryPoints(file.Members, [], moveHere: false, relocated, moved);
		relocatedEntryPoints = relocated;

		SourceFile copy = (SourceFile)file.Clone();
		copy.Members.Clear();
		foreach (AstNode member in kept.Concat(moved))
		{
			copy.Members.Add(member);
		}

		return copy;
	}

	/// <summary>
	/// Emits a type or namespace generated on its own, then the entry points it holds at file scope.
	/// </summary>
	/// <param name="root">The type or namespace being generated.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateWithEntryPointsAfter(AstNode root, CodeBlocker code)
	{
		Dictionary<EntryPoint, IReadOnlyList<string>> relocated = new(ReferenceEqualityComparer.Instance);
		List<EntryPoint> moved = [];
		AstNode view = WithoutEntryPoints([root], [], moveHere: false, relocated, moved)[0];
		relocatedEntryPoints = relocated;

		generatingView = true;
		try
		{
			GenerateInternal(view, code);
		}
		finally
		{
			generatingView = false;
		}

		foreach (EntryPoint entryPoint in moved)
		{
			code.NewLine();
			GenerateInternal(entryPoint, code);
		}
	}

	/// <summary>
	/// Copies a run of members, leaving out the entry points that belong at file scope.
	/// </summary>
	/// <param name="members">The members as declared.</param>
	/// <param name="namespaces">The namespaces enclosing them, outermost first.</param>
	/// <param name="moveHere">Whether an entry point standing directly among them is moved.</param>
	/// <param name="relocated">Where each moved entry point is recorded with its namespaces.</param>
	/// <param name="moved">The moved entry points, in the order declared.</param>
	/// <returns>The members that stay, with any container that lost an entry point copied.</returns>
	private List<AstNode> WithoutEntryPoints(
		IEnumerable<AstNode> members,
		IReadOnlyList<string> namespaces,
		bool moveHere,
		Dictionary<EntryPoint, IReadOnlyList<string>> relocated,
		List<EntryPoint> moved)
	{
		List<AstNode> kept = [];
		foreach (AstNode member in members)
		{
			int before = moved.Count;
			switch (member)
			{
				case EntryPoint entryPoint when moveHere:
					relocated[entryPoint] = namespaces;
					moved.Add(entryPoint);
					continue;

				case ClassDeclaration classDecl when HoldsEntryPoint(classDecl):
				{
					List<AstNode> inner = WithoutEntryPoints(classDecl.Members, namespaces, moveHere: true, relocated, moved);
					if (moved.Count == before)
					{
						break;
					}

					ClassDeclaration copy = (ClassDeclaration)classDecl.Clone();
					for (int index = 0; index < classDecl.Annotations.Count; index++)
					{
						RememberAnnotationClone(copy.Annotations[index], classDecl.Annotations[index]);
					}

					Refill(copy.Members, inner);
					kept.Add(copy);
					continue;
				}

				case NamespaceDeclaration namespaceDecl when HoldsEntryPoint(namespaceDecl):
				{
					IReadOnlyList<string> inside = [.. namespaces, .. NamespaceDeclaration.Split(namespaceDecl.Name)];
					List<AstNode> inner = WithoutEntryPoints(namespaceDecl.Members, inside, EntryPointsLeaveNamespaces, relocated, moved);
					if (moved.Count == before)
					{
						break;
					}

					NamespaceDeclaration copy = (NamespaceDeclaration)namespaceDecl.Clone();
					Refill(copy.Members, inner);
					kept.Add(copy);
					continue;
				}

				default:
					break;
			}

			kept.Add(member);
		}

		return kept;
	}

	/// <summary>
	/// Replaces what a collection holds.
	/// </summary>
	/// <param name="target">The collection to refill.</param>
	/// <param name="members">What it should hold.</param>
	private static void Refill(Collection<AstNode> target, IEnumerable<AstNode> members)
	{
		target.Clear();
		foreach (AstNode member in members)
		{
			target.Add(member);
		}
	}

	/// <summary>
	/// Emits the program's entry point, and whatever else the language needs in order to run it.
	/// </summary>
	/// <param name="entryPoint">The entry point to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code);

	/// <summary>
	/// Emits the members of a class, one after another.
	/// </summary>
	/// <param name="classDecl">The class whose members to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A member is dispatched like any other node, so a nested class, a method and a field each come
	/// out through the emitter for its own shape and nesting needs no separate code path.
	/// </remarks>
	protected void GenerateClassMembers(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);

		foreach (AstNode member in classDecl.Members)
		{
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Emits a variable declaration as a complete statement.
	/// </summary>
	/// <param name="varDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code);

	/// <summary>
	/// Emits one parameter, without any separator.
	/// </summary>
	/// <param name="parameter">The parameter to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="position">The parameter's position, used to name an unnamed parameter.</param>
	protected abstract void GenerateParameter(Parameter parameter, CodeBlocker code, int position);

	/// <summary>
	/// Appends a parameter's default value, when it has one.
	/// </summary>
	/// <param name="parameter">The parameter whose default to append.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A default value is how every target language expresses <see cref="Parameter.IsOptional"/>;
	/// none of them has separate syntax for it.
	/// </remarks>
	protected void AppendDefaultValue(Parameter parameter, CodeBlocker code)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		if (parameter.IsOptional && parameter.DefaultValue is Expression value)
		{
			code.Write(" = ");
			GenerateInternal(value, code);
		}
	}

	/// <summary>
	/// Appends a parameter's default value as a comment, for a language with no default arguments.
	/// </summary>
	/// <param name="parameter">The parameter whose default to note.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A <c>*/</c> in the value would end the comment early and leave the rest of it as source, so
	/// it is broken apart.
	/// </remarks>
	protected void AppendDefaultValueComment(Parameter parameter, CodeBlocker code)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		if (parameter.IsOptional && parameter.DefaultValue is Expression value)
		{
			using CodeBlocker inline = CodeBlocker.Create(IndentString);
			GenerateInternal(value, inline);
			string spelled = inline.ToString().TrimEnd('\r', '\n').Replace("*/", "* /", StringComparison.Ordinal);
			code.Write($" /* = {spelled} */");
		}
	}

	/// <summary>
	/// Emits a comma-separated parameter list, without the surrounding parentheses.
	/// </summary>
	/// <param name="parameters">The parameters to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateParameterList(IReadOnlyList<Parameter> parameters, CodeBlocker code)
	{
		Ensure.NotNull(parameters);
		Ensure.NotNull(code);

		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
			{
				code.Write(", ");
			}

			GenerateParameter(parameters[i], code, i);
		}
	}

	/// <summary>
	/// Writes a list of elements between delimiters, one per line when the elements are themselves
	/// lists.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="open">What opens the list.</param>
	/// <param name="close">What closes it.</param>
	/// <param name="empty">What to write instead when there are no elements at all.</param>
	/// <remarks>
	/// Every language that writes a list of values writes it the same way — the delimiters differ,
	/// and where a value names the member it is for, so does that spelling. Both are the caller's,
	/// through the parameters and through <see cref="WriteDesignator"/>.
	/// <para>
	/// A list of values is a value and belongs on one line; a list whose elements are themselves
	/// lists is a table, and a table written on one line is a row of a diff nobody can read. The test
	/// is the shape of the data rather than a column count, because a generated file has no idea how
	/// wide anyone's editor is and a rule about that would have to be guessed.
	/// </para>
	/// </remarks>
	protected void WriteElementList(
		ConstructionExpression construction,
		CodeBlocker code,
		string open,
		string close,
		string empty)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		IReadOnlyList<AstNode> elements = WrittenElements(construction);

		if (elements.Count == 0)
		{
			code.Write(empty);
			return;
		}

		if (SpansLines(construction))
		{
			WriteStackedList(elements, code, open, close);
			return;
		}

		code.Write($"{open}{ListPadding}");
		for (int index = 0; index < elements.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			WriteListElement(elements[index], code);
		}

		code.Write($"{ListPadding}{close}");
	}

	/// <summary>
	/// Gets what stands between a list's delimiters and its elements when it is written on one line.
	/// </summary>
	/// <remarks>
	/// A space everywhere but Go, which writes <c>Point{X: 1}</c>. That is not a preference there:
	/// <c>gofmt</c> writes it that way and nobody gets a say, which makes the padding part of the
	/// language's spelling of a list rather than of anyone's taste in them.
	/// </remarks>
	protected virtual string ListPadding => " ";

	/// <summary>
	/// Writes a list one element per line.
	/// </summary>
	/// <param name="elements">The elements to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="open">What opens the list.</param>
	/// <param name="close">What closes it.</param>
	/// <remarks>
	/// A trailing comma after the last element, which every target language allows in a list and
	/// which keeps adding a row to a generated table from touching the row above it in the diff.
	/// </remarks>
	private void WriteStackedList(IReadOnlyList<AstNode> elements, CodeBlocker code, string open, string close)
	{
		code.WriteLine(open);
		code.Indent();

		foreach (AstNode argument in elements)
		{
			WriteListElement(argument, code);
			code.WriteLine(",");
		}

		code.Outdent();
		code.Write(close);
	}

	/// <summary>
	/// Writes one element of a list, which may name the member it is for.
	/// </summary>
	/// <param name="argument">The element to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Overridable for a language with a shorter way to say one of these — Rust's field-init
	/// shorthand is the one that exists here.
	/// </remarks>
	protected virtual void WriteListElement(AstNode argument, CodeBlocker code)
	{
		if (argument is MemberInitialiser designated)
		{
			WriteDesignator(EscapeIdentifier(designated.Name ?? string.Empty), code);
			WriteListValue(designated.Value!, code);
			return;
		}

		WriteListValue(argument, code);
	}

	/// <summary>
	/// Writes the part of an element that says which member it is for.
	/// </summary>
	/// <param name="name">The member's name.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// <c>name: value</c> is what a language whose lists are keyed writes — Rust's struct literal
	/// and JavaScript's object literal both. C and C++ write a designator instead, and override this.
	/// </remarks>
	protected virtual void WriteDesignator(string name, CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.Write($"{name}: ");
	}

	/// <summary>
	/// Writes what one element of a list is.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Ordinary generation, unless a language has something to say about a value that stands inside
	/// a list rather than on its own — which C does, since only there may it leave the type out.
	/// </remarks>
	protected virtual void WriteListValue(AstNode value, CodeBlocker code) => GenerateInternal(value, code);

	/// <summary>
	/// Reports whether a list is worth breaking across lines.
	/// </summary>
	/// <param name="construction">The expression to judge.</param>
	/// <returns><see langword="true"/> when it should be written one element per line.</returns>
	private static bool SpansLines(ConstructionExpression construction) =>
		construction.Arguments.Any(argument =>
			argument is ConstructionExpression or MemberInitialiser { Value: ConstructionExpression });

	/// <summary>
	/// Names every operator the AST can spell, for a language that cannot overload one and so has to
	/// call it something.
	/// </summary>
	/// <param name="spell">How the language writes a name made of words.</param>
	/// <returns>The name for each symbol the AST can spell.</returns>
	/// <remarks>
	/// The names come from the AST's own operator vocabulary rather than from a table written beside
	/// it, so an operator added to <see cref="BinaryOperator"/> is named without anyone remembering
	/// to name it — and one added without a spelling is left out rather than throwing before anything
	/// has run.
	/// <para>
	/// A symbol both kinds of operator share is named for the binary one, which is what a reader of
	/// <c>a - b</c> means; the unary declaration of it is told apart by taking no operand beside the
	/// instance, which is the caller's to notice.
	/// </para>
	/// </remarks>
	protected static Dictionary<string, string> BuildOperatorNames(Func<string, string> spell)
	{
		Ensure.NotNull(spell);

		Dictionary<string, string> names = new(StringComparer.Ordinal);

		foreach (BinaryOperator op in Enum.GetValues<BinaryOperator>().Where(HasSymbol))
		{
			names[OperatorSymbols.GetSymbol(op)] = spell(op.ToString());
		}

		foreach (UnaryOperator op in Enum.GetValues<UnaryOperator>().Where(HasSymbol))
		{
			names.TryAdd(OperatorSymbols.GetSymbol(op), spell(op.ToString()));
		}

		return names;
	}

	/// <summary>
	/// Names the unary operators alone.
	/// </summary>
	/// <param name="spell">How the language writes a name made of words.</param>
	/// <returns>The name for each symbol a unary operator can be spelled with.</returns>
	/// <remarks>
	/// For a language that tells a unary declaration apart by its arity and so must not name it after
	/// the binary operator sharing its symbol. <c>-</c> is the whole of the problem: a type declaring
	/// both would otherwise declare the same name twice, which is the one outcome worse than an odd
	/// name.
	/// </remarks>
	protected static Dictionary<string, string> BuildUnaryOperatorNames(Func<string, string> spell)
	{
		Ensure.NotNull(spell);

		Dictionary<string, string> names = new(StringComparer.Ordinal);

		foreach (UnaryOperator op in Enum.GetValues<UnaryOperator>().Where(HasSymbol))
		{
			names[OperatorSymbols.GetSymbol(op)] = spell(op.ToString());
		}

		return names;
	}

	/// <summary>
	/// Reports whether the AST can spell an operator at all.
	/// </summary>
	/// <param name="op">The operator to test.</param>
	/// <returns>True when it has a symbol.</returns>
	private static bool HasSymbol(BinaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null;

	/// <inheritdoc cref="HasSymbol(BinaryOperator)"/>
	/// <param name="op">The operator to test.</param>
	private static bool HasSymbol(UnaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null;

	/// <summary>
	/// Spells a binary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetBinaryOperator"/>.
	/// </remarks>
	protected virtual string GetOperatorSpelling(BinaryOperator op) => GetBinaryOperator(op);

	/// <summary>
	/// Emits a binary expression in the target language.
	/// </summary>
	/// <param name="binary">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Defaults to the operands either side of the operator's spelling. A language whose operator
	/// means something else for some operands, rather than being spelled differently, overrides this.
	/// </remarks>
	protected virtual void GenerateBinary(BinaryExpression binary, CodeBlocker code)
	{
		Ensure.NotNull(binary);
		Ensure.NotNull(code);
		if (SpellsFloatingRemainderAsCall
			&& binary.Operator == BinaryOperator.Modulo
			&& FloatingRemainderType(binary.Left, binary.Right) is string floatingType)
		{
			WriteFloatingRemainder(binary.Left, binary.Right, floatingType, code);
			return;
		}

		// The operands meet at the wider of their types, which a target that widens nothing on its own
		// has to be told. A shift's count is not an operand of that kind, and a logical operator's
		// operands are booleans.
		if (WidensNumbersExplicitly
			&& binary.Operator is not (BinaryOperator.LeftShift or BinaryOperator.RightShift
				or BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr))
		{
			TypeReference? common = Promoted(StaticType(binary.Left), StaticType(binary.Right));
			code.Write("(");
			GenerateWidenedTo(binary.Left, common, code);
			code.Write($" {GetOperatorSpelling(binary.Operator)} ");
			GenerateWidenedTo(binary.Right, common, code);
			code.Write(")");
			return;
		}

		GenerateBinaryExpression(binary, code, GetOperatorSpelling(binary.Operator));
	}

	/// <summary>
	/// Emits a unary expression in the target language.
	/// </summary>
	/// <param name="unary">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Defaults to the operator's spelling before its operand. A language whose operator means
	/// something else for some operands, rather than being spelled differently, overrides this.
	/// </remarks>
	protected virtual void GenerateUnary(UnaryExpression unary, CodeBlocker code)
	{
		Ensure.NotNull(unary);
		GenerateUnaryExpression(unary, code, GetUnaryOperatorSpelling(unary.Operator));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A floating <c>x %= y</c> is written <c>x = fmod(x, y)</c> or its equivalent, for the target
	/// whose <c>%</c> takes only integers and whose <c>%=</c> therefore does too.
	/// </remarks>
	protected override void GenerateAssignmentStatement(AssignmentStatement assignment, CodeBlocker code)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(code);

		if (SpellsFloatingRemainderAsCall
			&& assignment.Operator == AssignmentOperator.ModuloAssign
			&& FloatingRemainderType(assignment.Target, assignment.Value) is string floatingType)
		{
			GenerateInternal(assignment.Target, code);
			code.Write($" {GetAssignmentOperator(AssignmentOperator.Assign)} ");
			WriteFloatingRemainder(assignment.Target, assignment.Value, floatingType, code);
			EndStatement(code);
			return;
		}

		if (WidensNumbersExplicitly
			&& assignment.Operator is not (AssignmentOperator.LeftShiftAssign or AssignmentOperator.RightShiftAssign)
			&& StaticType(assignment.Target) is TypeReference targetType)
		{
			GenerateInternal(assignment.Target, code);
			code.Write($" {GetAssignmentOperator(assignment.Operator)} ");
			GenerateWidenedTo(assignment.Value, targetType, code);
			EndStatement(code);
			return;
		}

		base.GenerateAssignmentStatement(assignment, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The value is widened to the function's return type where the target does not do it implicitly.
	/// </remarks>
	protected override void GenerateReturnStatement(ReturnStatement returnStmt, CodeBlocker code)
	{
		Ensure.NotNull(returnStmt);
		Ensure.NotNull(code);

		if (!WidensNumbersExplicitly || returnStmt.Expression is null)
		{
			base.GenerateReturnStatement(returnStmt, code);
			return;
		}

		code.Write("return ");
		GenerateWidenedTo(returnStmt.Expression, StaticType(returnStmt), code);
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An argument is widened to its parameter's type where the target does not do it implicitly and
	/// the call names exactly one function declared in what is being generated.
	/// </remarks>
	protected override void GenerateArgument(CallExpression callExpr, int index, CodeBlocker code)
	{
		Ensure.NotNull(callExpr);
		GenerateWidenedTo(callExpr.Arguments[index], WidensNumbersExplicitly ? DeclaredParameterType(callExpr, index) : null, code);
	}

	/// <summary>
	/// Spells a unary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetUnaryOperator"/>. A word operator is
	/// separated from its operand by the emitter, so an override returns the bare word.
	/// </remarks>
	protected virtual string GetUnaryOperatorSpelling(UnaryOperator op) => GetUnaryOperator(op);
}
