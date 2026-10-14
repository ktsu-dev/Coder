// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Finds a registered <see cref="ILanguageGenerator"/> by the name a caller has for its language.
/// </summary>
/// <remarks>
/// A caller has one of two names for a language: the id it was told (<c>--language py</c>, a
/// remembered setting) or the extension of the file it is writing. Both lookups live here so every
/// consumer agrees on case and on the leading dot, rather than each writing its own
/// <c>FirstOrDefault</c>.
/// </remarks>
public interface ILanguageGeneratorResolver
{
	/// <summary>
	/// Gets every generator the resolver can return, in registration order.
	/// </summary>
	public IReadOnlyList<ILanguageGenerator> Generators { get; }

	/// <summary>
	/// Finds the generator whose <see cref="ILanguageGenerator.LanguageId"/> is <paramref name="languageId"/>,
	/// ignoring case.
	/// </summary>
	/// <param name="languageId">The language id, such as <c>python</c>.</param>
	/// <param name="generator">The generator, or null when none has that id.</param>
	/// <returns>True when a generator was found; otherwise, false.</returns>
	public bool TryGetByLanguageId(string languageId, [NotNullWhen(true)] out ILanguageGenerator? generator);

	/// <summary>
	/// Finds the generator whose <see cref="ILanguageGenerator.FileExtension"/> matches
	/// <paramref name="extensionOrPath"/>, ignoring case.
	/// </summary>
	/// <param name="extensionOrPath">
	/// An extension with or without its leading dot (<c>py</c>, <c>.py</c>), or a file path whose
	/// extension is used (<c>out/model.py</c>).
	/// </param>
	/// <param name="generator">The generator, or null when none writes that extension.</param>
	/// <returns>True when a generator was found; otherwise, false.</returns>
	public bool TryGetByFileExtension(string extensionOrPath, [NotNullWhen(true)] out ILanguageGenerator? generator);
}
