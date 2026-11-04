// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a member's modifiers are written only where C# allows them.
/// </summary>
/// <remarks>
/// The AST's modifiers are C++'s: a trailing <c>const</c> on any member function, a pure virtual
/// member in an otherwise ordinary class, a destructor with an access specifier. C# allows
/// <c>readonly</c> only on an instance member of a struct (CS0106), no access modifier on a finalizer
/// (CS0106), no finalizer in a struct (CS0575), and an abstract member only in a class that says it is
/// abstract (CS0513).
/// </remarks>
[TestClass]
public class CSharpMemberModifierTests
{
	/// <summary>
	/// Tests that a class method that does not modify its instance says so in a note rather than with
	/// <c>readonly</c>.
	/// </summary>
	[TestMethod]
	public void ReadOnlyClassMethod_IsNotSpelledReadonly()
	{
		ClassDeclaration counter = new("Counter");
		counter.Members.Add(new FieldDeclaration("count", new TypeReference("int")));
		FunctionDeclaration get = new("get") { ReturnType = "int", IsReadOnly = true };
		get.Body.Add(new ReturnStatement(new VariableReference("count")));
		counter.Members.Add(get);

		string code = new CSharpGenerator().Generate(counter);

		Assert.Contains("public int get()", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("readonly int get(", code, StringComparison.Ordinal, code);
		Assert.Contains("// readonly: get does not modify its instance", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an instance member of a struct is still spelled <c>readonly</c>, and a static one is
	/// not.
	/// </summary>
	[TestMethod]
	public void ReadOnlyStructMethod_IsSpelledReadonly()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FieldDeclaration("x", new TypeReference("int")));
		FunctionDeclaration getX = new("getX") { ReturnType = "int", IsReadOnly = true };
		getX.Body.Add(new ReturnStatement(new VariableReference("x")));
		point.Members.Add(getX);
		FunctionDeclaration origin = new("origin") { ReturnType = "int", IsReadOnly = true, IsStatic = true };
		origin.Body.Add(new ReturnStatement(Literal.Number(0)));
		point.Members.Add(origin);

		string code = new CSharpGenerator().Generate(point);

		Assert.Contains("public readonly int getX()", code, StringComparison.Ordinal, code);
		Assert.Contains("public static int origin()", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("readonly:", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an interface member is neither <c>readonly</c> nor written <c>abstract</c>, which an
	/// interface member with no body already is.
	/// </summary>
	[TestMethod]
	public void InterfaceMember_HasNeitherReadonlyNorAbstract()
	{
		string code = new CSharpGenerator().Generate(CompiledExemplar.Shape());

		Assert.Contains("public double area();", code, StringComparison.Ordinal, code);
		Assert.Contains("public void draw(double scale);", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("abstract", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("readonly double", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a class with an abstract member is itself abstract.
	/// </summary>
	[TestMethod]
	public void ClassWithAbstractMember_IsAbstract()
	{
		ClassDeclaration animal = new("Animal");
		animal.Members.Add(new FunctionDeclaration("speak") { ReturnType = "string", IsAbstract = true });

		string code = new CSharpGenerator().Generate(animal);

		Assert.Contains("public abstract class Animal", code, StringComparison.Ordinal, code);
		Assert.Contains("public abstract string speak();", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a class with no abstract member is not marked abstract.
	/// </summary>
	[TestMethod]
	public void ClassWithoutAbstractMember_IsNotAbstract()
	{
		ClassDeclaration animal = new("Animal");
		animal.Members.Add(new FunctionDeclaration("speak") { ReturnType = "string", IsVirtual = true });

		string code = new CSharpGenerator().Generate(animal);

		Assert.Contains("public class Animal", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("abstract", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a class's destructor is a finalizer with no access modifier.
	/// </summary>
	[TestMethod]
	public void ClassDestructor_HasNoAccessModifier()
	{
		ClassDeclaration vec = new("Vec");
		vec.Members.Add(new FunctionDeclaration("Vec") { Kind = FunctionKind.Destructor });

		string code = new CSharpGenerator().Generate(vec);

		Assert.Contains("~Vec()", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("public ~Vec", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a struct's destructor is written down rather than declared.
	/// </summary>
	[TestMethod]
	public void StructDestructor_IsANote()
	{
		ClassDeclaration vec = new("Vec") { Kind = TypeDeclarationKind.Struct };
		vec.Members.Add(new FunctionDeclaration("Vec") { Kind = FunctionKind.Destructor });

		string code = new CSharpGenerator().Generate(vec);

		Assert.Contains("// ~Vec is a destructor, which a C# struct cannot declare.", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("~Vec()", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an abstract operator in a class is written as the public static operator C#
	/// requires, with neither abstract nor its declared visibility, and a note saying so (#260).
	/// </summary>
	[TestMethod]
	public void AbstractProtectedOperatorInAClass_IsPublicStaticWithANote()
	{
		ClassDeclaration shape = new("Shape");
		FunctionDeclaration plus = CompiledExemplar.Plus();
		plus.ReturnType = "Shape";
		plus.Parameters[0].Type = "Shape";
		plus.IsAbstract = true;
		plus.Visibility = Visibility.Protected;
		shape.Members.Add(plus);

		string code = new CSharpGenerator().Generate(shape);

		Assert.Contains("public static Shape operator +(", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("abstract Shape", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("protected static", code, StringComparison.Ordinal, code);
		Assert.Contains("// abstract: operator + is static", code, StringComparison.Ordinal, code);
		Assert.Contains("// protected: operator + is public", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an interface's static abstract and virtual members keep what C# allows there (#412).
	/// </summary>
	[TestMethod]
	public void InterfaceStaticAbstractMember_IsUnchanged()
	{
		ClassDeclaration parser = new("IParser") { Kind = TypeDeclarationKind.Interface };
		parser.Members.Add(new FunctionDeclaration("Create") { ReturnType = "int", IsStatic = true, IsAbstract = true });

		string code = new CSharpGenerator().Generate(parser);

		Assert.Contains("public static int Create();", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("// ", code.Replace("// Copyright", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a class whose only abstract member is static, which is written down rather than
	/// declared, is not marked abstract (#412).
	/// </summary>
	[TestMethod]
	public void ClassWithOnlyAStaticAbstractMember_IsNotAbstract()
	{
		ClassDeclaration factory = new("Factory");
		factory.Members.Add(new FunctionDeclaration("Build") { ReturnType = "int", IsStatic = true, IsAbstract = true });

		string code = new CSharpGenerator().Generate(factory);

		Assert.Contains("public class Factory", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("abstract int", code, StringComparison.Ordinal, code);
	}
}
