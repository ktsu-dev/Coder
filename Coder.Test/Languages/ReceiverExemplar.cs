// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;

/// <summary>
/// A type whose methods are called on a reference parameter, on a pointer parameter, and on the
/// result of another call — the three receivers a member call is not always written the same way for
/// (#170).
/// </summary>
/// <remarks>
/// C lowers a member function to a free function taking a pointer to the instance, and the call site
/// has to meet it: it takes the address of a value, passes a pointer or reference as it is, and has
/// nothing to take the address of when the receiver is a call's result. C++ reaches through a pointer
/// with <c>-&gt;</c>, and Rust does not reach through a raw pointer at all without dereferencing it.
/// None of that shows in the text, so the compile tests are what check it.
/// </remarks>
internal static class ReceiverExemplar
{
	/// <summary>
	/// Builds the file.
	/// </summary>
	/// <param name="callee">Spells a method's name at a call site, which is the caller's business:
	/// C names the free function a method lowers to after its type, and the call site has to say so.</param>
	/// <returns>The file.</returns>
	internal static SourceFile Build(Func<string, string> callee)
	{
		ClassDeclaration vec = new("Vec2") { Kind = TypeDeclarationKind.Struct };
		vec.Members.Add(new VariableDeclaration("x", "double"));

		// double length() const { return 1.0; }
		FunctionDeclaration length = new("length") { ReturnType = "double", IsReadOnly = true };
		length.Body.Add(new ReturnStatement(new LiteralExpression<double>(1.0)));
		vec.Members.Add(length);

		// Vec2 twice() const { return Vec2{ .x = 2.0 }; }
		FunctionDeclaration twice = new("twice") { ReturnType = "Vec2", IsReadOnly = true };
		twice.Body.Add(new ReturnStatement(new ConstructionExpression(new TypeReference("Vec2"))
		{
			Arguments = { new MemberInitialiser("x", new LiteralExpression<double>(2.0)) },
		}));
		vec.Members.Add(twice);

		SourceFile file = new("receivers");
		file.Members.Add(vec);

		// double through_reference(const Vec2& other) { return other.length(); }
		file.Members.Add(CallOn("through_reference", "const Vec2&", "other", callee));

		// double through_pointer(const Vec2* other) { return other->length(); }
		file.Members.Add(CallOn("through_pointer", "const Vec2*", "other", callee));

		// double chained(Vec2 v) { return v.twice().length(); }
		FunctionDeclaration chained = new("chained") { ReturnType = "double" };
		chained.Parameters.Add(new Parameter("v", "Vec2"));
		chained.Body.Add(new ReturnStatement(new CallExpression(
			new CallExpression(new VariableReference("v"), callee("twice")),
			callee("length"))));
		file.Members.Add(chained);

		return file;
	}

	/// <summary>
	/// Builds a free function returning <c>length()</c> called on its one parameter.
	/// </summary>
	/// <param name="name">The function's name.</param>
	/// <param name="type">The parameter's type.</param>
	/// <param name="parameter">The parameter's name.</param>
	/// <param name="callee">Spells a method's name at a call site.</param>
	/// <returns>The function.</returns>
	private static FunctionDeclaration CallOn(string name, string type, string parameter, Func<string, string> callee)
	{
		FunctionDeclaration function = new(name) { ReturnType = "double" };
		function.Parameters.Add(new Parameter(parameter, type));
		function.Body.Add(new ReturnStatement(new CallExpression(new VariableReference(parameter), callee("length"))));
		return function;
	}
}
