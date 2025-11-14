// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder;

using ktsu.Coder.Languages;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Coder services in dependency injection containers.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds all available language generators to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddLanguageGenerators(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register all available language generators
		services.AddSingleton<ILanguageGenerator, PythonGenerator>();
		services.AddSingleton<ILanguageGenerator, CSharpGenerator>();

		// TODO: Add more language generators as they are implemented
		// services.AddSingleton<ILanguageGenerator, JavaScriptGenerator>();
		// services.AddSingleton<ILanguageGenerator, CppGenerator>();

		return services;
	}

	/// <summary>
	/// Adds serialization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddCoderSerialization(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<Serialization.YamlSerializer>();
		services.AddSingleton<Serialization.YamlDeserializer>();

		return services;
	}

	/// <summary>
	/// Adds all Coder services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddCoder(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services
			.AddLanguageGenerators()
			.AddCoderSerialization();
	}
}
