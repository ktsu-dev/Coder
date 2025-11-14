// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test;

using ktsu.Coder;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
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
	public void AddLanguageGenerators_ShouldRegisterLanguageGenerators()
	{
		// Arrange
		ServiceCollection services = new();

		// Act
		services.AddLanguageGenerators();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Assert
		List<ILanguageGenerator> generators = [.. serviceProvider.GetServices<ILanguageGenerator>()];
		Assert.IsTrue(generators.Count > 0, "At least one language generator should be registered");

		// Verify PythonGenerator is registered
		ILanguageGenerator? pythonGenerator = generators.FirstOrDefault(g => g.LanguageId == "python");
		Assert.IsNotNull(pythonGenerator, "PythonGenerator should be registered");
		Assert.IsInstanceOfType<PythonGenerator>(pythonGenerator);
	}

	/// <summary>
	/// Tests that AddCoderSerialization registers the expected serialization services.
	/// </summary>
	[TestMethod]
	public void AddCoderSerialization_ShouldRegisterSerializationServices()
	{
		// Arrange
		ServiceCollection services = new();

		// Act
		services.AddCoderSerialization();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Assert
		YamlSerializer? serializer = serviceProvider.GetService<YamlSerializer>();
		Assert.IsNotNull(serializer, "YamlSerializer should be registered");

		YamlDeserializer? deserializer = serviceProvider.GetService<YamlDeserializer>();
		Assert.IsNotNull(deserializer, "YamlDeserializer should be registered");
	}

	/// <summary>
	/// Tests that AddCoder registers both language generators and serialization services.
	/// </summary>
	[TestMethod]
	public void AddCoder_ShouldRegisterAllServices()
	{
		// Arrange
		ServiceCollection services = new();

		// Act
		services.AddCoder();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Assert - Language generators
		List<ILanguageGenerator> generators = [.. serviceProvider.GetServices<ILanguageGenerator>()];
		Assert.IsTrue(generators.Count > 0, "Language generators should be registered");

		// Assert - Serialization services
		YamlSerializer? serializer = serviceProvider.GetService<YamlSerializer>();
		Assert.IsNotNull(serializer, "YamlSerializer should be registered");

		YamlDeserializer? deserializer = serviceProvider.GetService<YamlDeserializer>();
		Assert.IsNotNull(deserializer, "YamlDeserializer should be registered");
	}

	/// <summary>
	/// Tests that registered services can be resolved and used.
	/// </summary>
	[TestMethod]
	public void RegisteredServices_ShouldBeResolvableAndUsable()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddLanguageGenerators();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Act
		List<ILanguageGenerator> generators = [.. serviceProvider.GetServices<ILanguageGenerator>()];
		ILanguageGenerator? pythonGenerator = generators.FirstOrDefault(g => g.LanguageId == "python");

		// Assert
		Assert.IsNotNull(pythonGenerator, "PythonGenerator should be resolvable");
		Assert.AreEqual("python", pythonGenerator.LanguageId);
		Assert.AreEqual("Python", pythonGenerator.DisplayName);
		Assert.AreEqual("py", pythonGenerator.FileExtension);
	}

	/// <summary>
	/// Tests that services are registered as singletons.
	/// </summary>
	[TestMethod]
	public void Services_ShouldBeSingletons()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddCoder();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Act & Assert - Language generators should be singletons
		ILanguageGenerator generator1 = serviceProvider.GetServices<ILanguageGenerator>().First();
		ILanguageGenerator generator2 = serviceProvider.GetServices<ILanguageGenerator>().First();
		Assert.AreSame(generator1, generator2, "Language generators should be singleton instances");

		// Act & Assert - Serialization services should be singletons
		YamlSerializer? serializer1 = serviceProvider.GetService<YamlSerializer>();
		YamlSerializer? serializer2 = serviceProvider.GetService<YamlSerializer>();
		Assert.AreSame(serializer1, serializer2, "Serializer should be singleton instance");

		YamlDeserializer? deserializer1 = serviceProvider.GetService<YamlDeserializer>();
		YamlDeserializer? deserializer2 = serviceProvider.GetService<YamlDeserializer>();
		Assert.AreSame(deserializer1, deserializer2, "Deserializer should be singleton instance");
	}

	/// <summary>
	/// Tests that AddCoder can be called multiple times without issues.
	/// </summary>
	[TestMethod]
	public void AddCoder_ShouldBeIdempotent()
	{
		// Arrange
		ServiceCollection services = new();

		// Act
		services.AddCoder();
		services.AddCoder();
		services.AddLanguageGenerators();
		services.AddCoderSerialization();
		ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Assert - Should still work correctly
		List<ILanguageGenerator> generators = [.. serviceProvider.GetServices<ILanguageGenerator>()];
		YamlSerializer? serializer = serviceProvider.GetService<YamlSerializer>();
		YamlDeserializer? deserializer = serviceProvider.GetService<YamlDeserializer>();

		Assert.IsTrue(generators.Count > 0, "Generators should still be registered");
		Assert.IsNotNull(serializer, "Serializer should still be registered");
		Assert.IsNotNull(deserializer, "Deserializer should still be registered");
	}
}
