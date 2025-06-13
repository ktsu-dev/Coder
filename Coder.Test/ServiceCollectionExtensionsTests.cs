// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test;

using ktsu.Coder.Core;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the ServiceCollectionExtensions class.
/// </summary>
[TestClass]
public class ServiceCollectionExtensionsTests
{
	/// <summary>
	/// Tests that AddLanguageGenerators registers the expected language generators.
	/// </summary>
	[TestMethod]
	public void AddLanguageGenerators_RegistersExpectedGenerators()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLanguageGenerators();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var generators = serviceProvider.GetServices<ILanguageGenerator>().ToList();
		Assert.IsTrue(generators.Count > 0, "At least one language generator should be registered");

		// Verify PythonGenerator is registered
		var pythonGenerator = generators.FirstOrDefault(g => g.LanguageId == "python");
		Assert.IsNotNull(pythonGenerator, "PythonGenerator should be registered");
		Assert.IsInstanceOfType(pythonGenerator, typeof(PythonGenerator));
	}

	/// <summary>
	/// Tests that AddCoderSerialization registers serialization services.
	/// </summary>
	[TestMethod]
	public void AddCoderSerialization_RegistersSerializationServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCoderSerialization();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var serializer = serviceProvider.GetService<YamlSerializer>();
		Assert.IsNotNull(serializer, "YamlSerializer should be registered");

		var deserializer = serviceProvider.GetService<YamlDeserializer>();
		Assert.IsNotNull(deserializer, "YamlDeserializer should be registered");
	}

	/// <summary>
	/// Tests that AddCoder registers all expected services.
	/// </summary>
	[TestMethod]
	public void AddCoder_RegistersAllServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCoder();
		var serviceProvider = services.BuildServiceProvider();

		// Assert - Language generators
		var generators = serviceProvider.GetServices<ILanguageGenerator>().ToList();
		Assert.IsTrue(generators.Count > 0, "Language generators should be registered");

		// Assert - Serialization services
		var serializer = serviceProvider.GetService<YamlSerializer>();
		Assert.IsNotNull(serializer, "YamlSerializer should be registered");

		var deserializer = serviceProvider.GetService<YamlDeserializer>();
		Assert.IsNotNull(deserializer, "YamlDeserializer should be registered");
	}

	/// <summary>
	/// Tests that language generators can be resolved and used correctly.
	/// </summary>
	[TestMethod]
	public void LanguageGenerators_CanBeResolvedAndUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLanguageGenerators();
		var serviceProvider = services.BuildServiceProvider();

		// Act
		var generators = serviceProvider.GetServices<ILanguageGenerator>().ToList();
		var pythonGenerator = generators.FirstOrDefault(g => g.LanguageId == "python");

		// Assert
		Assert.IsNotNull(pythonGenerator, "PythonGenerator should be resolvable");
		Assert.AreEqual("python", pythonGenerator.LanguageId);
		Assert.AreEqual("Python", pythonGenerator.DisplayName);
		Assert.AreEqual("py", pythonGenerator.FileExtension);

		// Test that it can generate (basic smoke test)
		Assert.IsTrue(pythonGenerator.CanGenerate(new Core.Ast.AstLeafNode<string>("test")));
	}

	/// <summary>
	/// Tests that services are registered as singletons.
	/// </summary>
	[TestMethod]
	public void Services_AreRegisteredAsSingletons()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddCoder();
		var serviceProvider = services.BuildServiceProvider();

		// Act & Assert - Language generators should be singletons
		var generator1 = serviceProvider.GetServices<ILanguageGenerator>().First();
		var generator2 = serviceProvider.GetServices<ILanguageGenerator>().First();
		Assert.AreSame(generator1, generator2, "Language generators should be singleton instances");

		// Act & Assert - Serialization services should be singletons
		var serializer1 = serviceProvider.GetService<YamlSerializer>();
		var serializer2 = serviceProvider.GetService<YamlSerializer>();
		Assert.AreSame(serializer1, serializer2, "Serializer should be singleton instance");

		var deserializer1 = serviceProvider.GetService<YamlDeserializer>();
		var deserializer2 = serviceProvider.GetService<YamlDeserializer>();
		Assert.AreSame(deserializer1, deserializer2, "Deserializer should be singleton instance");
	}

	/// <summary>
	/// Tests that null service collection throws ArgumentNullException.
	/// </summary>
	[TestMethod]
	public void Extensions_ThrowOnNullServiceCollection()
	{
		// Arrange
		ServiceCollection? services = null;

		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() => services!.AddLanguageGenerators());
		Assert.ThrowsException<ArgumentNullException>(() => services!.AddCoderSerialization());
		Assert.ThrowsException<ArgumentNullException>(() => services!.AddCoder());
	}
}
