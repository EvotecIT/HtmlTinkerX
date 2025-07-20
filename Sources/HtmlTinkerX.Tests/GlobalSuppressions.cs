// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

#if FRAMEWORK
// Suppress CS8981: The type name only contains lower-cased ascii characters
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Required for .NET Framework compatibility")]
#endif