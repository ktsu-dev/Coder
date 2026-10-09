// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder;

using ktsu.Coder.Languages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
		Ensure.NotNull(services);

		// Register all available language generators. TryAddEnumerable skips a generator that is
		// already registered, so calling this twice still resolves one of each.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, PythonGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, CSharpGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, JavaScriptGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, CppGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, CGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, RustGenerator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageGenerator, GoGenerator>());

		return services;
	}

	/// <summary>
	/// Adds serialization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddCoderSerialization(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<Serialization.YamlSerializer>();
		services.TryAddSingleton<Serialization.YamlDeserializer>();

		return services;
	}

	/// <summary>
	/// Adds all Coder services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddCoder(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddLanguageGenerators()
			.AddCoderSerialization();
	}
}
