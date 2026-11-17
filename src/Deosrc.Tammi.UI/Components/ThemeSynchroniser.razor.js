// Sets the theme the CSS uses: "light" or "dark", or null to leave it to the browser's own detection.
export function setTheme(theme) {
    if (theme) {
        document.documentElement.dataset.theme = theme;
    } else {
        delete document.documentElement.dataset.theme;
    }
}
