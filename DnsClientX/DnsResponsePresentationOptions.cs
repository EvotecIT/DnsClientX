namespace DnsClientX {
    /// <summary>
    /// Defines supported response presentation modes.
    /// </summary>
    public enum DnsResponsePresentationMode {
        /// <summary>
        /// Renders a human-readable multi-section response.
        /// </summary>
        Pretty,
        /// <summary>
        /// Renders a compact list of answer values.
        /// </summary>
        Short,
        /// <summary>
        /// Renders dig-style raw text output.
        /// </summary>
        Raw,
        /// <summary>
        /// Renders the response as formatted JSON.
        /// </summary>
        Json
    }

    /// <summary>
    /// Controls how a DNS response should be presented.
    /// </summary>
    public sealed class DnsResponsePresentationOptions {
        /// <summary>
        /// Gets or sets the requested presentation mode.
        /// </summary>
        public DnsResponsePresentationMode Mode { get; set; } = DnsResponsePresentationMode.Pretty;

        /// <summary>
        /// Gets or sets whether TXT answers should be flattened.
        /// </summary>
        public bool TxtConcat { get; set; }

        /// <summary>
        /// Gets or sets whether the question section should be shown.
        /// </summary>
        public bool ShowQuestions { get; set; }

        /// <summary>
        /// Gets or sets whether the answer section should be shown.
        /// </summary>
        public bool ShowAnswers { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the authority section should be shown.
        /// </summary>
        public bool ShowAuthorities { get; set; }

        /// <summary>
        /// Gets or sets whether the additional section should be shown.
        /// </summary>
        public bool ShowAdditional { get; set; }
    }
}
