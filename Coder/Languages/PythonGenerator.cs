// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Collections.ObjectModel;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates Python code from AST nodes.
/// </summary>
/// <remarks>
/// Python has neither access modifiers nor constants, so <see cref="Visibility"/> and
/// <see cref="VariableDeclaration.IsConstant"/> are deliberately dropped, the same way JavaScript
/// drops the types the AST carries. The conventions Python does have for both — a leading underscore
/// for a non-public member, an upper-case name for a constant — are spellings of the identifier
/// rather than modifiers on the declaration, and renaming a declaration here would leave every
/// <see cref="VariableReference"/> to it naming something that no longer exists.
/// </remarks>
public class PythonGenerator : StandardLanguageGenerator
{
	private HashSet<FieldDeclaration>? instanceInitialisedFields;

	/// <summary>
	/// The root <see cref="writtenModuleFields"/> was worked out for.
	/// </summary>
	private AstNode? writtenModuleFieldsRoot;

	/// <summary>
	/// The module-level fields something in the document assigns to.
	/// </summary>
	private HashSet<string> writtenModuleFields = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "python";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "Python";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "py";

	/// <summary>
	/// Spells a boolean literal. Python capitalizes them.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <returns>The keyword Python uses.</returns>
	protected override string FormatBoolean(bool value) => value ? "True" : "False";

	/// <summary>
	/// Ends a statement. Python has no terminator, and the function body emits the line breaks, so
	/// this deliberately writes nothing.
	/// </summary>
	/// <param name="code">The writer, left untouched.</param>
	protected override void EndStatement(CodeBlocker code)
	{
		// Python statements end at the newline the caller writes.
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python puts the chosen values around the condition rather than after it, so the same node
	/// comes out as <c>go if ready else wait</c> where the others write <c>ready ? go : wait</c>.
	/// This is the whole of the difference: the operands are the same three and only their order
	/// changes, which is why the node is worth having rather than the text.
	/// </remarks>
	protected override void GenerateConditionalExpression(ConditionalExpression conditional, CodeBlocker code)
	{
		Ensure.NotNull(conditional);
		Ensure.NotNull(code);

		code.Write("(");
		GenerateInternal(conditional.WhenTrue, code);
		code.Write(" if ");
		GenerateInternal(conditional.Condition, code);
		code.Write(" else ");
		GenerateInternal(conditional.WhenFalse, code);
		code.Write(")");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python's documentation is a docstring rather than a comment, and a docstring belongs inside
	/// the construct it documents — which is a different shape from every other language here. Rather
	/// than move the lines somewhere the other three cannot follow, they are emitted as ordinary
	/// <c>#</c> comments: the reader still gets them, and nothing claims to be a docstring that is
	/// not one.
	/// </remarks>
	protected override string DocumentationPrefix => "#";

	/// <inheritdoc/>
	protected override string CommentPrefix => "#";

	/// <inheritdoc/>
	/// <remarks>
	/// A decorator, which is the one of these that is an ordinary runtime value: <c>@name</c> looks
	/// the name up and calls it on what follows. That makes it the closest of the four to being a
	/// caller's own, and the least likely to be something the language itself reads.
	/// </remarks>
	protected override string? SpellAnnotation(Annotation annotation, PreambleSite site) =>
		site == PreambleSite.Field ? null : $"@{annotation}";

	/// <inheritdoc/>
	/// <remarks>
	/// Python has the thing itself. An automatic property is a plain attribute — Python stores
	/// whatever is assigned to one and there is nothing to declare — so it comes out as the
	/// annotated name, which is what a reader and a type checker both want. One with bodies is a
	/// <c>@property</c> and its <c>@name.setter</c>, which is the language's own spelling and needs
	/// no import.
	/// </remarks>
	protected override void GeneratePropertyDeclaration(PropertyDeclaration declaration, CodeBlocker code)
	{
		Ensure.NotNull(declaration);
		Ensure.NotNull(code);

		string name = declaration.Name ?? "value";
		// The two spellings of a type: an annotation on a name is a colon, and on a function it is
		// an arrow. Same type, and Python is particular about which goes where.
		string annotation = declaration.Type is TypeReference type ? $": {PythonTypeFromGenericType(type)}" : string.Empty;
		string result = declaration.Type is TypeReference answered ? $" -> {PythonTypeFromGenericType(answered)}" : string.Empty;

		WritePreamble(declaration, code, PreambleSite.Property);

		if (declaration.IsAutomatic)
		{
			code.WriteLine($"{name}{annotation} = None");
			return;
		}

		if (declaration.IsStatic)
		{
			GenerateStaticAccessors(declaration, annotation, result, code);
			return;
		}

		if (declaration.CanRead)
		{
			code.WriteLine("@property");
			code.Write($"def {name}(self){result}:");
			WriteAccessorBody(declaration, isSetter: false, code);
		}

		if (declaration.CanWrite && !declaration.CanRead)
		{
			// `@name.setter` is an attribute of the property the getter made, so with no getter there
			// is nothing for the decorator to name and the class statement raises NameError. The
			// property is built from the setter alone instead.
			string setter = $"_set_{name}";
			code.Write($"def {setter}(self, value{annotation}) -> None:");
			WriteAccessorBody(declaration, isSetter: true, code);
			code.WriteLine($"{name} = property(fset={setter})");
			return;
		}

		if (declaration.CanWrite)
		{
			code.WriteLine($"@{name}.setter");
			code.Write($"def {name}(self, value{annotation}) -> None:");
			WriteAccessorBody(declaration, isSetter: true, code);
		}
	}

	/// <summary>
	/// Writes a static property with bodies as a getter and a setter on the class.
	/// </summary>
	/// <param name="declaration">The property to write.</param>
	/// <param name="annotation">The value's type, spelled as a parameter annotation.</param>
	/// <param name="result">The value's type, spelled as a return annotation.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A <c>@property</c> works only on an instance: read from the class, it answers the property
	/// object itself rather than calling the getter. Python has no static property, so this one
	/// becomes the two <c>@staticmethod</c> functions C, Rust and Go already lower it to, named the
	/// way they name them (<c>limit()</c> and <c>set_limit(value)</c>), and reachable from the class.
	/// </remarks>
	private void GenerateStaticAccessors(PropertyDeclaration declaration, string annotation, string result, CodeBlocker code)
	{
		if (declaration.CanRead)
		{
			code.WriteLine("@staticmethod");
			code.Write($"def {declaration.GetterName(NamingStyle.Snake)}(){result}:");
			WriteAccessorBody(declaration, isSetter: false, code);
		}

		if (declaration.CanWrite)
		{
			code.WriteLine("@staticmethod");
			code.Write($"def {declaration.SetterName(NamingStyle.Snake)}(value{annotation}) -> None:");
			WriteAccessorBody(declaration, isSetter: true, code);
		}
	}

	/// <summary>
	/// Writes an accessor's statements, indented under it.
	/// </summary>
	/// <param name="declaration">The property the accessor belongs to.</param>
	/// <param name="isSetter">True to write the setter's body; false to write the getter's.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// An empty one is <c>pass</c>, for the reason an empty class is: Python's body is delimited by
	/// indentation, so there is nothing to write that is nothing.
	/// </remarks>
	private void WriteAccessorBody(PropertyDeclaration declaration, bool isSetter, CodeBlocker code)
	{
		Collection<AstNode> body = isSetter ? declaration.SetterBody : declaration.GetterBody;
		using IDisposable types = OpenStaticTypeScope(declaration, isSetter);
		code.WriteLine();

		using IndentScope statements = new(code);
		WriteGlobalDeclarations(body, [], code);

		if (body.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		foreach (AstNode statement in body)
		{
			GenerateInternal(statement, code);
			code.WriteLine();
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>from __future__ import annotations</c>, which defers every annotation in the module. Python
	/// before 3.14 evaluates an annotation when the <c>def</c> or the class body runs, so a factory
	/// answering its own class, a method taking another instance, or a parameter naming a class
	/// declared further down is a name that does not exist yet and the module raises
	/// <c>NameError</c> on import. Deferring them all is the language's own answer and needs nothing
	/// to know which names are forward; a base list is an expression rather than an annotation, which
	/// is why <see cref="PythonBaseFromGenericType"/> still quotes. A future statement has to come
	/// before any other, so it is written here rather than as an import.
	/// </remarks>
	protected override bool WriteFileDirectives(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);

		if (file.Members.Count == 0)
		{
			return false;
		}

		code.WriteLine("from __future__ import annotations");
		return true;
	}

	/// <inheritdoc/>
	protected override string? SpellImport(string import)
	{
		Ensure.NotNull(import);
		return import.StartsWith("import ", StringComparison.Ordinal) || import.StartsWith("from ", StringComparison.Ordinal)
			? import
			: $"import {import}";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python has no literal for these, and <c>math.nan</c> would need an import that
	/// <c>float("nan")</c> does not.
	/// </remarks>
	protected override string SpellNonFiniteDouble(double value) =>
		double.IsNaN(value) ? "float(\"nan\")" : value > 0 ? "float(\"inf\")" : "float(\"-inf\")";

	/// <inheritdoc/>
	protected override IEnumerable<string> RequiredImports(SourceFile file)
	{
		Ensure.NotNull(file);
		if (ContainsEnum(file.Members))
		{
			yield return "from enum import Enum";
		}

		if (ContainsFloatingRemainder(file))
		{
			yield return MathModule;
		}
	}

	/// <summary>
	/// The module that defines <c>math.fmod</c>.
	/// </summary>
	private const string MathModule = "math";

	/// <inheritdoc/>
	/// <remarks>
	/// Python's <c>%</c> takes floats, but it floors and the result takes the divisor's sign, so
	/// <c>-7.5 % 2.0</c> is <c>0.5</c> where the C family's <c>fmod</c> gives <c>-1.5</c>. An integer
	/// remainder is truncated by <see cref="GenerateBinary"/> before the base class is asked.
	/// </remarks>
	protected override bool SpellsFloatingRemainderAsCall => true;

	/// <inheritdoc/>
	/// <remarks>
	/// <c>math.fmod</c> truncates, matching C's <c>fmod</c>. Python has one floating type, so a
	/// remainder taken in <c>float</c> is written the same as one taken in <c>double</c>.
	/// </remarks>
	protected override void WriteFloatingRemainder(Expression dividend, Expression divisor, string floatingType, CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.Write("math.fmod(");
		GenerateInternal(dividend, code);
		code.Write(", ");
		GenerateInternal(divisor, code);
		code.Write(")");
	}

	private static bool ContainsEnum(IEnumerable<AstNode> nodes)
	{
		foreach (AstNode node in nodes)
		{
			if (node is EnumDeclaration
				|| (node is ClassDeclaration declaration && ContainsEnum(declaration.Members))
				|| (node is NamespaceDeclaration namespaceDeclaration && ContainsEnum(namespaceDeclaration.Members)))
			{
				return true;
			}
		}

		return false;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python has nothing that is checked before the program runs, so what was asserted is written as
	/// a comment. Dropping it would leave a file that looks like one still making the guarantee.
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		WriteInexpressible(code, $"asserted at build time: {assertion.Condition}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An alias is an ordinary assignment in Python, which is what a type alias is there.
	/// </remarks>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		WritePreamble(usingAlias, code, PreambleSite.Type);
		code.WriteLine($"{usingAlias.Name} = {PythonTypeFromGenericType(usingAlias.AliasedType ?? new TypeReference("object"))}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An argument that names the member it is for becomes a keyword argument, which is what Python
	/// has in place of a designated initialiser and reads the same way. A construction with no type
	/// is a literal rather than a call: a dictionary when its arguments are named, a list when they
	/// are not, which is what a language with no aggregate type to name uses instead.
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Type is null)
		{
			WriteLiteral(construction, code);
			return;
		}

		code.Write($"{PythonTypeFromGenericType(construction.Type)}(");
		WriteArguments(construction, code);
		code.Write(")");
	}

	/// <summary>
	/// Writes a typeless construction as the literal Python builds that value with.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteLiteral(ConstructionExpression construction, CodeBlocker code)
	{
		bool named = construction.Arguments.Any(argument => argument is MemberInitialiser);

		code.Write(named ? "{" : "[");

		IReadOnlyList<AstNode> elements = WrittenElements(construction);

		for (int index = 0; index < elements.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			if (elements[index] is MemberInitialiser member)
			{
				code.Write($"\"{member.Name}\": ");
				GenerateInternal(member.Value!, code);
				continue;
			}

			GenerateInternal(elements[index], code);
		}

		code.Write(named ? "}" : "]");
	}

	/// <summary>
	/// Writes a construction's arguments, separated by commas.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteArguments(ConstructionExpression construction, CodeBlocker code)
	{
		IReadOnlyList<AstNode> elements = WrittenElements(construction);

		for (int index = 0; index < elements.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			if (elements[index] is MemberInitialiser member)
			{
				code.Write($"{member.Name}=");
				GenerateInternal(member.Value!, code);
				continue;
			}

			GenerateInternal(elements[index], code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python has no enumeration syntax; <c>enum.Enum</c> is a class. A member with no value of its
	/// own is one more than the member before it, matching what a language with real enumerations
	/// would give it; see <see cref="LanguageGeneratorBase.EnumMemberValues"/>. The <c>from enum import Enum</c> this needs belongs to the file rather than to the
	/// declaration.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		WritePreamble(enumDecl, code, PreambleSite.Enum);
		code.WriteLine($"class {enumDecl.Name ?? "UnnamedEnum"}(Enum):");

		using IndentScope body = new(code);
		if (enumDecl.Members.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		foreach ((EnumMember member, string value) in enumDecl.Members.Zip(EnumMemberValues(enumDecl)))
		{
			code.WriteLine($"{member.Name ?? "UNNAMED"} = {value}");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field is written as an annotated class attribute. One with no initialiser is left as a bare
	/// annotation, which is what a dataclass and a type checker both read as "this field exists and
	/// has this type" without also claiming a value for it.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		WritePreamble(field, code, PreambleSite.Field);
		code.Write(field.Name ?? "unnamed");

		if (field.Type is TypeReference type)
		{
			code.Write($": {PythonTypeFromGenericType(type)}");
		}

		if (field.InitialValue is not null && instanceInitialisedFields?.Contains(field) != true)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}
		else if (field.Type is null && field.InitialValue is null)
		{
			code.Write(" = None");
		}

		code.WriteLine();
	}

	/// <summary>
	/// Declares <c>global</c> the module-level fields a body assigns to.
	/// </summary>
	/// <param name="body">The statements of the body about to be written.</param>
	/// <param name="parameters">The parameters the body can see, which shadow a module field of the same name.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Python decides a name's scope from the whole function before running any of it: one assignment
	/// anywhere makes the name local everywhere in that body, so the read in <c>count += 1</c> finds an
	/// unbound local and raises <c>UnboundLocalError</c>. A <c>global</c> line is what says the
	/// assignment means the module's. Only fields the body itself assigns are listed, because a read
	/// alone already reaches the module and a declaration nothing needs is noise.
	/// </remarks>
	private void WriteGlobalDeclarations(IEnumerable<AstNode> body, IEnumerable<Parameter> parameters, CodeBlocker code)
	{
		if (!ReferenceEquals(writtenModuleFieldsRoot, GenerationRoot))
		{
			writtenModuleFieldsRoot = GenerationRoot;
			writtenModuleFields = WrittenFileScopeFields();
		}

		if (writtenModuleFields.Count == 0)
		{
			return;
		}

		AstNode[] nodes = [.. body.SelectMany(SelfAndDescendants)];
		HashSet<string> shadowed = new(
			parameters.Select(parameter => parameter.Name).OfType<string>()
				.Concat(nodes.OfType<VariableDeclaration>().Select(declaration => declaration.Name)),
			StringComparer.Ordinal);

		List<string> globals = [.. nodes
			.OfType<AssignmentStatement>()
			.Select(assignment => assignment.Target)
			.OfType<VariableReference>()
			.Select(reference => reference.Name.Split('.')[0])
			.Where(name => writtenModuleFields.Contains(name) && !shadowed.Contains(name))
			.Distinct(StringComparer.Ordinal)];

		if (globals.Count > 0)
		{
			code.WriteLine($"global {string.Join(", ", globals)}");
		}
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		using IDisposable types = OpenStaticTypeScope(funcDecl);
		WritePreamble(funcDecl, code, PreambleSite.Function);

		// Function signature
		code.Write($"def {funcDecl.Name ?? "unnamed_function"}(");
		GenerateParameterList(funcDecl.Parameters, code);
		code.Write(")");

		// Add return type hint if available
		if (funcDecl.ReturnType != null)
		{
			code.Write($" -> {PythonTypeFromGenericType(funcDecl.ReturnType)}");
		}

		code.WriteLine(":");

		// Python's body is delimited by indentation alone, so there is no brace scope to open.
		using IndentScope body = new(code);
		WriteGlobalDeclarations(funcDecl.Body, funcDecl.Parameters, code);

		// Function body
		if (funcDecl.Body.Count == 0)
		{
			// Empty function body needs a pass statement
			code.WriteLine("pass");
		}
		else
		{
			// Generate each statement in the body. EndStatement writes nothing for Python, so the
			// line break is this loop's to write.
			foreach (AstNode statement in funcDecl.Body)
			{
				GenerateInternal(statement, code);
				code.WriteLine();
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Python's suite may not be empty, so a class with no members gets a <c>pass</c>, the same way an
	/// empty function body does.
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		// C++ can attach a declaration to a type it does not own, by specialising a template on it.
		// Nothing here can, so the fact is written down rather than lost: what follows is an
		// ordinary declaration, and the comment says which type it was the declaration for.
		if (classDecl.IsSpecialisation)
		{
			WriteInexpressible(code, $"specialised for {string.Join(", ", classDecl.SpecialisationArguments)}");
		}

		// Python's @dataclass is what a record asks for, and it is not written here: the decorator
		// needs an import, and a class is generated on its own as readily as inside a file whose
		// imports the AST carries. Emitting one would be a change to how this generator writes a
		// file rather than to how it writes a class.
		WritePreamble(classDecl, code, PreambleSite.Type);
		WriteTypePromises(classDecl, code);

		// Python has TypeVar and Generic, and both need an import the AST does not carry for a
		// class generated on its own -- the same reason @dataclass is not written above.
		WriteTypeParametersDown(classDecl.TypeParameters, code);

		code.Write($"class {classDecl.Name ?? "UnnamedClass"}");

		// Python inherits from as many things as it is given and has no separate notion of an
		// interface, so the base and the interfaces are one list of bases -- which is what the
		// abstract base classes in the standard library already are.
		string? declaring = classDecl.Name;
		string[] bases =
		[
			.. classDecl.BaseType is TypeReference baseType ? (string[])[PythonBaseFromGenericType(baseType, declaring)] : [],
			.. classDecl.Interfaces.Select(implemented => PythonBaseFromGenericType(implemented, declaring)),
		];

		if (bases.Length > 0)
		{
			code.Write($"({string.Join(", ", bases)})");
		}

		code.WriteLine(":");

		HashSet<FieldDeclaration>? previousFields = instanceInitialisedFields;
		instanceInitialisedFields = new HashSet<FieldDeclaration>(
			InstanceInitialised(classDecl, NeedsConstruction),
			ReferenceEqualityComparer.Instance);
		try
		{
			// Python's body is delimited by indentation alone, so there is no brace scope to open.
			using IndentScope members = new(code);

			bool hasConstructor = classDecl.Members
				.OfType<FunctionDeclaration>()
				.Any(member => member.Kind == FunctionKind.Constructor);
			// Python inherits a constructor, so a class with nothing of its own to initialise writes
			// none and takes whatever its base takes. One written for a subclass would hide the base's
			// and refuse its arguments.
			bool synthesiseConstructor = !hasConstructor && instanceInitialisedFields.Count > 0;

			if (classDecl.Members.Count == 0 && !synthesiseConstructor)
			{
				code.WriteLine("pass");
				return;
			}

			// EndStatement writes nothing for Python, so the line break is this loop's to write.
			foreach (AstNode member in classDecl.Members)
			{
				if (member is FunctionDeclaration method)
				{
					GenerateMethod(method, classDecl, code);
				}
				else
				{
					GenerateInternal(member, code);
				}

				code.WriteLine();
			}

			if (synthesiseConstructor)
			{
				// A subclass's constructor passes on whatever it is given, so it still accepts what the
				// base's constructor does and only adds the initialisers.
				bool forwards = classDecl.BaseType is not null;
				code.WriteLine(forwards ? "def __init__(self, *args, **kwargs):" : "def __init__(self):");
				using IndentScope body = new(code);
				if (forwards)
				{
					code.WriteLine("super().__init__(*args, **kwargs)");
				}

				WriteConstructorPrologue(classDecl, code, NeedsConstruction, WriteInstanceInitialiser);
			}
		}
		finally
		{
			instanceInitialisedFields = previousFields;
		}
	}

	/// <summary>
	/// Emits a function as a method, which in Python means giving it the receiver as its first
	/// parameter.
	/// </summary>
	/// <param name="method">The function to emit as a method.</param>
	/// <param name="declaration">The containing class.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// <c>self</c> is Python's spelling of the receiver a method is called on. It is not carried in
	/// the AST — no other target language has it — so it is supplied here rather than being something
	/// the user has to remember to add as a parameter and then remove for every other language.
	/// <para>
	/// A static method is the one case where that receiver is not supplied, because
	/// <c>@staticmethod</c> is what says there is none. Every other language spells <c>static</c>
	/// alongside the signature; Python spells it by changing the signature.
	/// </para>
	/// </remarks>
	private void GenerateMethod(FunctionDeclaration method, ClassDeclaration declaration, CodeBlocker code)
	{
		using IDisposable types = OpenStaticTypeScope(method);
		WritePreamble(method, code, PreambleSite.Method);

		if (method.Definition != FunctionDefinition.Provided)
		{
			string state = method.Definition == FunctionDefinition.Defaulted ? "supplied by the language" : "deleted";
			WriteInexpressible(code, $"{method.Name} is {state}, which Python has no way to say.");
			return;
		}

		if (method.Kind is FunctionKind.Operator or FunctionKind.ConversionOperator)
		{
			WriteInexpressible(code, $"operator {method.Name} has no Python spelling.");
			return;
		}

		WriteMethodSignature(method, code);

		using IndentScope body = new(code);
		WriteGlobalDeclarations(method.Body, method.Parameters, code);

		// A method a derived class has to supply is a method whose body is a refusal. Python has no
		// declaration without a definition, so the definition says what calling it means.
		if (method.IsAbstract)
		{
			code.WriteLine("raise NotImplementedError");
			return;
		}

		WriteInitialiserAssignments(method, code);
		if (method.Kind == FunctionKind.Constructor)
		{
			WriteConstructorPrologue(declaration, code, NeedsConstruction, WriteInstanceInitialiser);
		}

		if (method.Body.Count == 0 && method.Initialisers.Count == 0)
		{
			code.WriteLine("pass");
			return;
		}

		foreach (AstNode statement in method.Body)
		{
			GenerateInternal(statement, code);
			code.WriteLine();
		}
	}

	private static bool NeedsConstruction(FieldDeclaration field) =>
		field.InitialValue is not null and not (
			LiteralExpression<string>
			or LiteralExpression<int>
			or LiteralExpression<long>
			or LiteralExpression<float>
			or LiteralExpression<double>
			or LiteralExpression<bool>);

	private void WriteInstanceInitialiser(FieldDeclaration field, CodeBlocker code)
	{
		code.Write($"self.{field.Name ?? "unnamed"} = ");
		GenerateInternal(field.InitialValue!, code);
		code.WriteLine();
	}

	/// <summary>
	/// Writes a method's signature, up to and including the colon that opens its suite.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The receiver is supplied here rather than carried in the AST, because no other target language
	/// has one — and it is left out of a static method, which is what <c>@staticmethod</c> means.
	/// </remarks>
	private void WriteMethodSignature(FunctionDeclaration method, CodeBlocker code)
	{
		if (method.IsStatic)
		{
			code.WriteLine("@staticmethod");
		}

		code.Write($"def {SpellMethodName(method)}(");

		bool needsSeparator = !method.IsStatic;
		if (needsSeparator)
		{
			code.Write("self");
		}

		for (int index = 0; index < method.Parameters.Count; index++)
		{
			if (needsSeparator)
			{
				code.Write(", ");
			}

			GenerateParameter(method.Parameters[index], code, index);
			needsSeparator = true;
		}

		code.Write(")");

		if (method.ReturnType is not null && method.Kind == FunctionKind.Method)
		{
			code.Write($" -> {PythonTypeFromGenericType(method.ReturnType)}");
		}

		code.WriteLine(":");
	}

	/// <summary>
	/// Writes what the type's members start at, as assignments at the top of the body.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Python assigns where C++ initialises, in the order declared, which is what the initialiser
	/// means where there is no initialiser list to put it in.
	/// <para>
	/// One with no value value-initialises the member, as C++'s <c>x()</c> does. Python has no type
	/// to take a default from, so the member is created holding <c>None</c>.
	/// </para>
	/// </remarks>
	private void WriteInitialiserAssignments(FunctionDeclaration method, CodeBlocker code)
	{
		foreach (MemberInitialiser initialiser in method.Initialisers)
		{
			code.Write($"self.{initialiser.Name} = ");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}
			else
			{
				code.Write("None");
			}

			code.WriteLine();
		}
	}

	/// <summary>
	/// Spells the name a method is written under.
	/// </summary>
	/// <param name="method">The declaration being emitted.</param>
	/// <returns>The name as Python writes it.</returns>
	/// <remarks>
	/// A constructor and a destructor have fixed names in Python rather than the type's, so whatever
	/// the declaration is called is ignored for those two.
	/// </remarks>
	private static string SpellMethodName(FunctionDeclaration method) => method.Kind switch
	{
		FunctionKind.Constructor => "__init__",
		FunctionKind.Destructor => "__del__",
		_ => method.Name ?? "unnamed_method",
	};

	/// <inheritdoc/>
	/// <remarks>
	/// The function is emitted with the <c>__main__</c> guard that runs it, which is how a Python
	/// file is both a script and an importable module. Arguments and the exit code go through
	/// <c>sys</c>, so the import it needs is emitted with it — the entry point is the top of a file,
	/// which is the one place a generator can put an import without knowing what else the document
	/// holds.
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		bool needsSys = entryPoint.AcceptsArguments || entryPoint.ReturnsExitCode;
		if (needsSys)
		{
			code.WriteLine("import sys");
			code.WriteLine();
			code.WriteLine();
		}

		code.Write("def main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("args");
		}

		code.WriteLine("):");

		// Python's body is delimited by indentation alone, so there is no brace scope to open.
		using (IDisposable types = OpenStaticTypeScope(entryPoint))
		using (IndentScope body = new(code))
		{
			WriteGlobalDeclarations(entryPoint.Body, [], code);
			if (entryPoint.Body.Count == 0)
			{
				code.WriteLine("pass");
			}
			else
			{
				// EndStatement writes nothing for Python, so the line break is this loop's to write.
				foreach (AstNode statement in entryPoint.Body)
				{
					GenerateInternal(statement, code);
					code.WriteLine();
				}
			}
		}

		// PEP 8 puts two blank lines between a top-level definition and what follows it.
		code.WriteLine();
		code.WriteLine();
		code.WriteLine("if __name__ == \"__main__\":");

		using IndentScope guard = new(code);
		string call = entryPoint.AcceptsArguments ? "main(sys.argv[1:])" : "main()";
		code.WriteLine(entryPoint.ReturnsExitCode ? $"sys.exit({call})" : call);
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write(parameter.Name ?? $"param{position}");

		// Type hints are optional in Python, so they are emitted only when the AST carries one.
		if (parameter.Type is not null)
		{
			code.Write($": {PythonTypeFromGenericType(parameter.Type)}");
		}

		AppendDefaultValue(parameter, code);
	}

	/// <summary>
	/// Spells a type in Python.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The Python source for it.</returns>
	/// <remarks>
	/// Python parameterises a type with brackets rather than angle brackets, which is only spellable
	/// now that the arguments are a list rather than part of a name. Read-only-ness and indirection
	/// have no spelling in Python at all, so neither is emitted.
	/// </remarks>
	private static string PythonTypeFromGenericType(TypeReference type)
	{
		string name = PythonTypeName(type);

		string spelled = type.TypeArguments.Count == 0
			? name
			: $"{name}[{string.Join(", ", type.TypeArguments.Select(PythonTypeFromGenericType))}]";

		// Python spells an array as a list of the element type, since the bracketed form after a
		// name is a subscript rather than a declarator here.
		return type.IsArray ? $"list[{spelled}]" : spelled;
	}

	/// <summary>
	/// Maps a type's name to the Python spelling of it.
	/// </summary>
	/// <param name="type">The type whose name to map.</param>
	/// <returns>The name Python knows it by.</returns>
	private static string PythonTypeName(TypeReference type) => type.Name.ToLowerInvariant() switch
	{
		"int" => "int",
		"short" => "int",
		"ushort" => "int",
		"uint" => "int",
		"ulong" => "int",
		"byte" => "int",
		"sbyte" => "int",
		"string" => "str",
		"bool" => "bool",
		"float" => "float",
		"double" => "float",
		"void" => "None",
		_ => type.Name
	};

	/// <summary>
	/// Spells a base of a class, quoting any argument that names the class being declared.
	/// </summary>
	/// <param name="type">The base to spell.</param>
	/// <param name="declaring">The name of the class the base list belongs to.</param>
	/// <returns>The Python source for it.</returns>
	/// <remarks>
	/// A base list is an expression and Python evaluates it as the <c>class</c> statement is run, so a
	/// base naming the class being declared is a name that does not exist yet: the self-type idiom —
	/// <c>IVector0&lt;TSelf, T&gt;</c>, the interface that hands a method the implementing type — makes
	/// the module raise <c>NameError</c> on import rather than misbehave later. Nothing about the
	/// declaration is wrong; Python is the one target that reads a base eagerly.
	/// <para>
	/// A quoted argument is the language's own answer, and the one <c>typing</c> consumers write for
	/// exactly this case: <c>typing</c> takes a string as a forward reference and resolves it when
	/// something asks, by which time the class exists. <c>from __future__ import annotations</c> is
	/// the other half of the idea and does not reach here — it defers *annotations*, and a base is an
	/// expression.
	/// </para>
	/// <para>
	/// Only the arguments are quoted, never the base itself. A class may inherit from a subscripted
	/// generic holding a forward reference and may not inherit from a string, and quoting is at the
	/// outermost argument alone for the same reason: one quoted argument carries every name inside it,
	/// and a second pair of quotes within the first would end the string rather than nest.
	/// </para>
	/// </remarks>
	private static string PythonBaseFromGenericType(TypeReference type, string? declaring)
	{
		if (declaring is null || type.TypeArguments.Count == 0 || !NamesType(type.TypeArguments, declaring))
		{
			return PythonTypeFromGenericType(type);
		}

		IEnumerable<string> arguments = type.TypeArguments.Select(argument =>
			NamesType(argument, declaring)
				? $"\"{PythonTypeFromGenericType(argument)}\""
				: PythonTypeFromGenericType(argument));

		string spelled = $"{PythonTypeName(type)}[{string.Join(", ", arguments)}]";

		return type.IsArray ? $"list[{spelled}]" : spelled;
	}

	/// <summary>
	/// Asks whether a type names another anywhere in it.
	/// </summary>
	/// <param name="type">The type to look through.</param>
	/// <param name="name">The name being looked for.</param>
	/// <returns>True when the name is the type's own or any of its arguments'.</returns>
	/// <remarks>
	/// Through the arguments rather than at the top alone, because a base is as readily written over a
	/// type holding the class — <c>IVector0&lt;Wrapper&lt;Length&lt;T&gt;&gt;, T&gt;</c> — as over the
	/// class itself, and Python evaluates all of it at once either way.
	/// </remarks>
	private static bool NamesType(TypeReference type, string name) =>
		string.Equals(type.Name, name, StringComparison.Ordinal) || NamesType(type.TypeArguments, name);

	/// <summary>
	/// Asks whether any of several types names another anywhere in it.
	/// </summary>
	/// <param name="types">The types to look through.</param>
	/// <param name="name">The name being looked for.</param>
	/// <returns>True when any of them names it.</returns>
	private static bool NamesType(IEnumerable<TypeReference> types, string name) =>
		types.Any(type => NamesType(type, name));

	/// <inheritdoc/>
	/// <remarks>
	/// A constant is emitted as an ordinary assignment: Python has no constant declaration, and the
	/// upper-case naming that stands in for one is a convention about the identifier rather than
	/// something the declaration can say.
	/// </remarks>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);
		RegisterStaticType(varDecl);

		code.Write(varDecl.Name);

		if (varDecl.InitialValue != null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}
		else
		{
			// Python requires initialization, so use None for uninitialized variables
			code.Write(" = None");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The AST means integer <see cref="BinaryOperator.Divide"/> and <see cref="BinaryOperator.Modulo"/>
	/// the way the C family does: the quotient truncates toward zero and the remainder takes the sign
	/// of the dividend, so <c>-7 / 2</c> is <c>-3</c> and <c>-7 % 2</c> is <c>-1</c>. Python's <c>/</c>
	/// is true division and gives <c>-3.5</c>, a float, and its <c>//</c> and <c>%</c> floor, giving
	/// <c>-4</c> and <c>1</c>. So when both operands are known to be integers the quotient is truncated,
	/// and the remainder is what is left once the truncated quotient is taken away.
	/// <para>
	/// The quotient is truncated in integer arithmetic only: <c>a // b</c> when the operands have the
	/// same sign, where flooring and truncating agree, and <c>-(-a // b)</c> when they differ. Going
	/// through <c>/</c> and <c>int()</c> instead would be exact for an <c>int</c> and not for a
	/// <c>long</c>, because <c>/</c> gives a float, which rounds any quotient above 2^53 before it is
	/// truncated. <c>math.fmod</c> is no better, being a float function too.
	/// </para>
	/// <para>
	/// Both forms name each operand more than once. An operand that is a literal or a variable reads
	/// the same every time; anything else is bound once, as the parameters of a lambda called where it
	/// stands, so a call is not made twice.
	/// </para>
	/// </remarks>
	protected override void GenerateBinary(BinaryExpression binary, CodeBlocker code)
	{
		Ensure.NotNull(binary);
		Ensure.NotNull(code);

		if (binary.Operator is not (BinaryOperator.Divide or BinaryOperator.Modulo) || !IsIntegerArithmetic(binary))
		{
			base.GenerateBinary(binary, code);
			return;
		}

		bool divide = binary.Operator == BinaryOperator.Divide;

		if (IsReadTwiceSafely(binary.Left) && IsReadTwiceSafely(binary.Right))
		{
			WriteTruncated(code, divide, () => GenerateInternal(binary.Left, code), () => GenerateInternal(binary.Right, code));
			return;
		}

		code.Write("(lambda a, b: ");
		WriteTruncated(code, divide, () => code.Write("a"), () => code.Write("b"));
		code.Write(")(");
		GenerateInternal(binary.Left, code);
		code.Write(", ");
		GenerateInternal(binary.Right, code);
		code.Write(")");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>x /= y</c> and <c>x %= y</c> are Python's true division and floored remainder, the same as
	/// the binary operators, so on integers they give <c>2.5</c> for <c>5 /= 2</c> and <c>1</c> for
	/// <c>-7 %= 2</c> where the C family gives <c>2</c> and <c>-1</c>. When both sides are known to be
	/// integers the assignment is written out as <c>x = x / y</c>, whose quotient or remainder
	/// <see cref="GenerateBinary"/> truncates. Every other compound assignment, and one on operands
	/// the AST does not know to be integers, is left as it is.
	/// </remarks>
	protected override void GenerateAssignmentStatement(AssignmentStatement assignment, CodeBlocker code)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(code);

		BinaryOperator? op = assignment.Operator switch
		{
			AssignmentOperator.DivideAssign => BinaryOperator.Divide,
			AssignmentOperator.ModuloAssign => BinaryOperator.Modulo,
			_ => null,
		};

		BinaryExpression? binary = op is null ? null : new(assignment.Target, op.Value, assignment.Value);
		if (binary is null || !IsIntegerArithmetic(binary))
		{
			base.GenerateAssignmentStatement(assignment, code);
			return;
		}

		GenerateInternal(assignment.Target, code);
		code.Write(" = ");
		GenerateBinary(binary, code);
		EndStatement(code);
	}

	/// <summary>
	/// Writes the C family's truncating quotient or remainder of two integers in integer arithmetic.
	/// </summary>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="divide">True for the quotient, false for the remainder.</param>
	/// <param name="left">Writes the dividend, which is read more than once.</param>
	/// <param name="right">Writes the divisor, which is read more than once.</param>
	private static void WriteTruncated(CodeBlocker code, bool divide, Action left, Action right)
	{
		if (!divide)
		{
			code.Write("(");
			left();
			code.Write(" - ");
			right();
			code.Write(" * ");
		}

		code.Write("(");
		left();
		code.Write(" // ");
		right();
		code.Write(" if (");
		left();
		code.Write(" < 0) == (");
		right();
		code.Write(" < 0) else -(-");
		left();
		code.Write(" // ");
		right();
		code.Write("))");

		if (!divide)
		{
			code.Write(")");
		}
	}

	private static bool IsReadTwiceSafely(Expression operand) =>
		operand is VariableReference
			or LiteralExpression<int>
			or LiteralExpression<long>;

	/// <summary>
	/// Maps a binary operator to its Python spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>Only the logical operators differ from the C-family set: Python spells them as words.</remarks>
	protected override string GetOperatorSpelling(BinaryOperator op) => op switch
	{
		BinaryOperator.LogicalAnd => "and",
		BinaryOperator.LogicalOr => "or",
		_ => GetBinaryOperator(op)
	};

	/// <summary>
	/// Maps a unary operator to its Python spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Only logical negation differs from the C-family set: Python spells it as a word. The emitter
	/// supplies the separating space, so this returns the bare keyword.
	/// </remarks>
	protected override string GetUnaryOperatorSpelling(UnaryOperator op) => op switch
	{
		UnaryOperator.LogicalNot => "not",
		_ => GetUnaryOperator(op)
	};
}
