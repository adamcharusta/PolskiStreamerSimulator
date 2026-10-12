using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>One explained entry of a weekly report, such as <c>cost:equipment</c> or <c>income:ads</c>.</summary>
public sealed record ReportLine(string Code, long? AmountPln = null, Fixed? Delta = null, string? Detail = null);
