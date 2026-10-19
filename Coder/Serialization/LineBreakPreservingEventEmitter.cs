// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Serialization;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

/// <summary>
/// Writes a string holding a carriage return or a Unicode line break as a double-quoted scalar.
/// </summary>
/// <remarks>
/// Left to choose, YamlDotNet writes a string with a line break in it as a block scalar, and YAML
/// reads every line break inside a block scalar back as <c>\n</c>: <c>"a\r\nb"</c> reloads as
/// <c>"a\nb"</c>. A double-quoted scalar escapes each of them, so the string comes back as it was
/// saved (ktsu-dev/Coder#284).
/// </remarks>
/// <param name="nextEmitter">The emitter this one decorates.</param>
internal sealed class LineBreakPreservingEventEmitter(IEventEmitter nextEmitter) : ChainedEventEmitter(nextEmitter)
{
	private static readonly char[] NormalisedLineBreaks = ['\r', '\u0085', '\u2028', '\u2029'];

	/// <inheritdoc/>
	public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
	{
		if (eventInfo.Source.Value is string text && text.IndexOfAny(NormalisedLineBreaks) >= 0)
		{
			eventInfo.Style = ScalarStyle.DoubleQuoted;
		}

		base.Emit(eventInfo, emitter);
	}
}
