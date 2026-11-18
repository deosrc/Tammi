// Sets the theme the CSS uses: "light" or "dark", or null to leave it to the browser's own detection.
export function setTheme(theme) {
    if (theme) {
        document.documentElement.dataset.theme = theme;
    } else {
        delete document.documentElement.dataset.theme;
    }
}

// The brand palette's theme colours for the current theme, as palette.css defines them, so Fluent's theme is built
// from the same colours. Read after setTheme, as the colours depend on the theme.
export function brandColors() {
    const styles = getComputedStyle(document.documentElement);
    const colour = (name) => styles.getPropertyValue(name).trim();
    return {
        background: colour("--tammi-bg"),
        surface: colour("--tammi-surface"),
        text: colour("--tammi-text"),
        textMuted: colour("--tammi-text-muted"),
        accent: colour("--tammi-accent"),
        primary: colour("--tammi-primary"),
        primaryHover: colour("--tammi-primary-hover"),
        primaryPressed: colour("--tammi-primary-pressed"),
        primarySelected: colour("--tammi-primary-selected"),
        onPrimary: colour("--tammi-on-primary"),
    };
}
