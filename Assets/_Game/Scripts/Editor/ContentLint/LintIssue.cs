namespace UtsavNights.EditorTools.ContentLint
{
    public enum LintSeverity
    {
        Warning = 0,
        Error = 1,
    }

    /// <summary>One finding about one content asset. Errors block builds; warnings do not.</summary>
    public readonly struct LintIssue
    {
        public LintIssue(LintSeverity severity, string rule, string assetName, string message)
        {
            Severity = severity;
            Rule = rule;
            AssetName = assetName;
            Message = message;
        }

        public LintSeverity Severity { get; }

        /// <summary>Stable rule id, e.g. "audio.missing", so tests and suppressions can refer to it.</summary>
        public string Rule { get; }

        public string AssetName { get; }

        public string Message { get; }

        public override string ToString() => $"[{Severity}] {AssetName}: {Message} ({Rule})";
    }
}
