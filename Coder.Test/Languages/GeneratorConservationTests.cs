// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class GeneratorConservationTests
{
	[TestMethod]
	public void EveryGenerator_WritesEveryDeclarationPreamble()
	{
		LanguageGeneratorBase[] generators =
		[
			new CSharpGenerator(),
			new CppGenerator(),
			new CGenerator(),
			new GoGenerator(),
			new JavaScriptGenerator(),
			new PythonGenerator(),
			new RustGenerator(),
		];

		foreach (LanguageGeneratorBase generator in generators)
		{
			generator.EnforceConservation = true;
			string generated = generator.Generate(AnnotatedFile());
			Assert.IsNotEmpty(generated, generator.DisplayName);
		}
	}

	[TestMethod]
	public void PythonMethodAnnotation_RemainsAboveMethod()
	{
		ClassDeclaration declaration = new("Example");
		FunctionDeclaration method = new("Run") { IsStatic = true };
		method.Annotations.Add(new Annotation("staticmethod"));
		declaration.Members.Add(method);

		string generated = new PythonGenerator().Generate(declaration);

		StringAssert.Contains(generated, "@staticmethod");
		StringAssert.Contains(generated, "def Run():");
	}

	private static SourceFile AnnotatedFile()
	{
		SourceFile file = new("conservation");
		FieldDeclaration fileField = AddPreamble(new FieldDeclaration("Global", "int"), "file-field");
		file.Members.Add(fileField);

		FunctionDeclaration function = AddPreamble(new FunctionDeclaration("Run"), "function");
		file.Members.Add(function);

		ClassDeclaration type = AddPreamble(new ClassDeclaration("Example"), "type");
		type.Members.Add(AddPreamble(new FieldDeclaration("Value", "int"), "field"));
		type.Members.Add(AddPreamble(new FunctionDeclaration("Method"), "method"));

		PropertyDeclaration automatic = AddPreamble(new PropertyDeclaration("Automatic", new TypeReference("int"))
		{
			HasSetter = true,
		}, "automatic-property");
		type.Members.Add(automatic);

		PropertyDeclaration bodied = AddPreamble(new PropertyDeclaration("Bodied", new TypeReference("int"))
		{
			HasSetter = true,
		}, "bodied-property");
		bodied.GetterBody.Add(new ReturnStatement(new LiteralExpression<int>(1)));
		bodied.SetterBody.Add(new AssignmentStatement(new VariableReference("value"), new LiteralExpression<int>(2)));
		type.Members.Add(bodied);

		EnumDeclaration enumeration = AddPreamble(new EnumDeclaration("State"), "enum");
		enumeration.Members.Add(new EnumMember("Ready"));
		type.Members.Add(enumeration);

		file.Members.Add(type);
		return file;
	}

	private static T AddPreamble<T>(T declaration, string label)
		where T : AstNode
	{
		switch (declaration)
		{
			case ClassDeclaration classDeclaration:
				classDeclaration.Documentation.Add($"documentation-{label}");
				classDeclaration.Annotations.Add(new Annotation($"annotation-{label}"));
				break;
			case EnumDeclaration enumDeclaration:
				enumDeclaration.Documentation.Add($"documentation-{label}");
				enumDeclaration.Annotations.Add(new Annotation($"annotation-{label}"));
				break;
			case FieldDeclaration fieldDeclaration:
				fieldDeclaration.Documentation.Add($"documentation-{label}");
				fieldDeclaration.Annotations.Add(new Annotation($"annotation-{label}"));
				break;
			case FunctionDeclaration functionDeclaration:
				functionDeclaration.Documentation.Add($"documentation-{label}");
				functionDeclaration.Annotations.Add(new Annotation($"annotation-{label}"));
				break;
			case PropertyDeclaration propertyDeclaration:
				propertyDeclaration.Documentation.Add($"documentation-{label}");
				propertyDeclaration.Annotations.Add(new Annotation($"annotation-{label}"));
				break;
		}

		return declaration;
	}
}
