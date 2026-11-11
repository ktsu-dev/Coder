// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a <c>float</c> parameter's default stays a <c>float</c> through the text form the YAML and
/// the inspector hold.
/// </summary>
/// <remarks>
/// The text form writes <c>1.5f</c> as <c>1.5</c>, which used to read back as a double, so C# came
/// out as <c>float factor = 1.5d</c> and failed with CS1750 (ktsu-dev/Coder#337).
/// </remarks>
[TestClass]
public class FloatParameterDefaultTests
{
	[TestMethod]
	public void RoundTrip_FloatParameterDefault_StaysAFloat()
	{
		FunctionDeclaration function = Scale();
		string expected = new CSharpGenerator().Generate(function);

		FunctionDeclaration restored = Assert.IsInstanceOfType<FunctionDeclaration>(
			new YamlDeserializer().Deserialize(new YamlSerializer().Serialize(function)));

		LiteralExpression<float> value = Assert.IsInstanceOfType<LiteralExpression<float>>(restored.Parameters[1].DefaultValue);
		Assert.AreEqual(1.5f, value.Value);
		Assert.Contains("float factor = 1.5f", expected);
		Assert.AreEqual(expected, new CSharpGenerator().Generate(restored));
	}

	[TestMethod]
	public void Deserialize_UnsuffixedFloatDefaultInAnOlderDocument_ReadsAsAFloat()
	{
		const string Yaml = """
			parameter:
			  name: factor
			  type: float
			  isOptional: true
			  defaultValue: "1.5"
			""";

		Parameter parameter = Assert.IsInstanceOfType<Parameter>(new YamlDeserializer().Deserialize(Yaml));

		Assert.AreEqual(1.5f, Assert.IsInstanceOfType<LiteralExpression<float>>(parameter.DefaultValue).Value);
	}

	[TestMethod]
	[DataRow("1.5")]
	[DataRow("1.5f")]
	[DataRow("1.5F")]
	public void Inspector_DefaultOnAFloatParameter_IsAFloat(string text)
	{
		Parameter parameter = new("factor", "float") { IsOptional = true };

		Assert.IsTrue(AstFields.TryWrite(parameter, "Default", text));

		Assert.AreEqual(1.5f, Assert.IsInstanceOfType<LiteralExpression<float>>(parameter.DefaultValue).Value);
	}

	[TestMethod]
	public void Inspector_FractionalDefaultOnADoubleParameter_StaysADouble()
	{
		Parameter parameter = new("factor", "double") { IsOptional = true };

		Assert.IsTrue(AstFields.TryWrite(parameter, "Default", "1.5"));

		Assert.AreEqual(1.5, Assert.IsInstanceOfType<LiteralExpression<double>>(parameter.DefaultValue).Value);
	}

	[TestMethod]
	public void ParseDefaultValue_FloatSuffix_ReadsAFloat() =>
		Assert.AreEqual(-0.25f, Assert.IsInstanceOfType<LiteralExpression<float>>(Parameter.ParseDefaultValue("-0.25f")).Value);

	private static FunctionDeclaration Scale()
	{
		FunctionDeclaration function = new("scale") { ReturnType = "float" };
		function.Parameters.Add(new Parameter("x", "float"));
		function.Parameters.Add(new Parameter("factor", "float") { IsOptional = true, DefaultValue = new LiteralExpression<float>(1.5f) });
		return function;
	}
}
