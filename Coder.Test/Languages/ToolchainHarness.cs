// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Diagnostics;

/// <summary>
/// Runs a real compiler over generated source.
/// </summary>
/// <remarks>
/// Three of the generators here are checked by compiling what they write, because the rules they
/// have to obey are not visible in the text: C's about linkage and constant expressions, Rust's
/// about receivers, associated items and what a trait implementation owes its trait, and Go's about
/// unused names, which it refuses rather than warns about. Everything those tests share about
/// *running* a compiler is here, so that what is left in each of them is the language.
/// </remarks>
internal static class ToolchainHarness
{
	/// <summary>
	/// Finds the first of several commands that is on the path.
	/// </summary>
	/// <param name="askVersion">The argument that makes the command say what it is and stop.</param>
	/// <param name="commands">The commands to try, in the order a project would.</param>
	/// <returns>The first one that runs, or null when none does.</returns>
	/// <remarks>
	/// Asked by running it rather than by looking for a file, because what matters is whether it
	/// starts — a name on the path that cannot be executed is not a compiler.
	/// <para>
	/// The argument is the caller's because it is not the same everywhere: a compiler takes
	/// <c>--version</c> and the Go toolchain takes a subcommand, and <c>go --version</c> exits
	/// non-zero, which would read as "not installed".
	/// </para>
	/// </remarks>
	public static string? FindOnPath(string askVersion, params string[] commands) =>
		commands.FirstOrDefault(command => Run(command, askVersion, Path.GetTempPath()).ExitCode == 0);

	/// <summary>
	/// Runs a command in a directory of its own, and deletes the directory afterwards.
	/// </summary>
	/// <param name="work">What to do in the directory, given its path.</param>
	public static void InTemporaryDirectory(Action<string> work)
	{
		ArgumentNullException.ThrowIfNull(work);

		string directory = Path.Combine(Path.GetTempPath(), $"coder-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		try
		{
			work(directory);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	/// <summary>
	/// Runs a command, waiting for it to finish.
	/// </summary>
	/// <param name="command">The executable to run.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="workingDirectory">Where to run it.</param>
	/// <returns>What it exited with, and everything it wrote.</returns>
	public static (int ExitCode, string Output) Run(string command, string arguments, string workingDirectory)
	{
		ProcessStartInfo start = new(command, arguments)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		try
		{
			using Process? process = Process.Start(start);
			if (process is null)
			{
				return (-1, string.Empty);
			}

			string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
			process.WaitForExit();
			return (process.ExitCode, output);
		}
		catch (System.ComponentModel.Win32Exception)
		{
			// The command is not on the path, which is the answer rather than a failure.
			return (-1, string.Empty);
		}
	}
}
