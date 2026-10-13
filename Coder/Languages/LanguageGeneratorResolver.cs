// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

/// <summary>
/// Resolves a language generator from the generators it is given.
/// </summary>
/// <remarks>
/// When two generators claim the same id or extension, the first one given wins, so the answer
/// does not depend on anything but registration order.
/// </remarks>
public sealed class LanguageGeneratorResolver : ILanguageGeneratorResolver
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LanguageGeneratorResolver"/> class.
	/// </summary>
	/// <param name="generators">The generators to resolve from.</param>
	public LanguageGeneratorResolver(IEnumerable<ILanguageGenerator> generators)
	{
		Ensure.NotNull(generators);
		Generators = [.. generators];
	}

	/// <inheritdoc/>
	public IReadOnlyList<ILanguageGenerator> Generators { get; }

	/// <inheritdoc/>
	public bool TryGetByLanguageId(string languageId, [NotNullWhen(true)] out ILanguageGenerator? generator)
	{
		Ensure.NotNull(languageId);

		generator = Generators.FirstOrDefault(
			g => string.Equals(g.LanguageId, languageId, StringComparison.OrdinalIgnoreCase));
		return generator is not null;
	}

	/// <inheritdoc/>
	public bool TryGetByFileExtension(string extensionOrPath, [NotNullWhen(true)] out ILanguageGenerator? generator)
	{
		Ensure.NotNull(extensionOrPath);

		string extension = ExtensionOf(extensionOrPath);
		generator = extension.Length == 0
			? null
			: Generators.FirstOrDefault(
				g => string.Equals(g.FileExtension.TrimStart('.'), extension, StringComparison.OrdinalIgnoreCase));
		return generator is not null;
	}

	/// <summary>
	/// Reads the extension, without its dot, out of a bare extension or a path.
	/// </summary>
	/// <param name="extensionOrPath">A bare extension (<c>py</c>), a dotted one (<c>.py</c>) or a path.</param>
	/// <returns>The extension without its dot, or empty when there is none.</returns>
	private static string ExtensionOf(string extensionOrPath)
	{
		string trimmed = extensionOrPath.Trim();
		bool isBare = trimmed.IndexOfAny(['.', '/', '\\']) < 0;
		return isBare ? trimmed : Path.GetExtension(trimmed).TrimStart('.');
	}
}
